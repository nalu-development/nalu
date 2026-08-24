using CoreGraphics;
using Foundation;
using UIKit;

namespace Nalu;

/// <summary>
/// The system window controls of iPadOS 26 — the "traffic lights" — and how far a surface must
/// keep its content from them.
/// </summary>
/// <remarks>
/// <para>
/// While an app IS a window, the system draws those controls over the window's top-leading corner,
/// on top of whatever the app draws there: a nav bar's leading buttons, the first entry of a
/// start-edge drawer.
/// </para>
/// <para>
/// Nothing reports where they are. UIKit publishes no inset for them — a windowed scene reports a
/// plain <c>L0 T32 R0 B20</c> with the controls plainly on screen, and they sit BELOW that top
/// inset — they are hosted outside the app's window, so walking the window's own subtree finds
/// only the app's views, and iOS 26's <c>UISceneWindowingControlStyle</c>, the one API about them,
/// selects a style and never a frame. Their footprint is therefore a MEASURED constant.
/// </para>
/// </remarks>
internal static class ScaffoldWindowControls
{
    /// <summary>
    /// The controls' rect in WINDOW coordinates, measured on an iPad Pro 11" running iPadOS 26 by
    /// tracing the pixel transitions around the capsule. Identical windowed and full-screen, and
    /// it does not scale with the window: the system draws one fixed cluster in the corner.
    /// </summary>
    private static readonly CGRect _inWindow = new(21, 43, 41, 22);

    /// <summary>
    /// Whether the app opted OUT of the iOS 26 design (<c>UIDesignRequiresCompatibility</c> in its
    /// Info.plist). Such an app gets the compatibility window chrome instead: the system reserves
    /// a band at the top of the window — measured on iPadOS 26.5, safe area top 32 rather than 10
    /// — and draws the controls INSIDE it, so they never reach the app's content and insetting
    /// for them would only open a gap where nothing is drawn.
    /// </summary>
    private static readonly bool _designCompatibility =
        NSBundle.MainBundle.ObjectForInfoDictionary("UIDesignRequiresCompatibility") is NSNumber flag && flag.BoolValue;

    /// <summary>
    /// Whether the controls are permanently on screen and over the app's own content: only for a
    /// WINDOWED iPad scene, in an app that did not opt out of the iOS 26 design. A full-screen app
    /// gets them transiently instead — they appear near the corner and hide again — and holding a
    /// band open forever for something usually absent would cost every full-screen iPad app that
    /// space.
    /// </summary>
    private static bool IsActive(UIView view)
    {
        // iPadOS 26 is where windowing (and these controls) begins, and only on iPad.
        if (!OperatingSystem.IsIOSVersionAtLeast(26) || UIDevice.CurrentDevice.UserInterfaceIdiom != UIUserInterfaceIdiom.Pad)
        {
            return false;
        }

        if (_designCompatibility)
        {
            return false;
        }

        if (view.Window is not { } window || window.WindowScene?.Screen is not { } screen)
        {
            return false;
        }

        // The window's frame is expressed in its own space, so the only way to learn it is a
        // smaller rectangle on the screen is to convert through the screen's coordinate space.
        var onScreen = window.ConvertRectToCoordinateSpace(window.Bounds, screen.CoordinateSpace);

        return onScreen.Size != screen.Bounds.Size;
    }

    /// <summary>
    /// The view's RESTING rect in window coordinates. Center and bounds are both untouched by the
    /// transform, so a strip parked offscreen by a hide animation, or a drawer mid-slide, still
    /// reports where it actually lives — the inset must not flip while a slide is in flight.
    /// </summary>
    private static CGRect RestingRectInWindow(UIView view)
    {
        var bounds = view.Bounds;
        var center = view.Center;
        var resting = new CGRect(center.X - (bounds.Width / 2), center.Y - (bounds.Height / 2), bounds.Width, bounds.Height);

        return view.Superview?.ConvertRectToView(resting, null) ?? resting;
    }

    /// <summary>
    /// How much LEADING inset the view still needs for its content to clear the controls, given
    /// what it already inherits. A surface that does not actually reach under them — a sheet, a
    /// bar starting past them — needs nothing.
    /// </summary>
    public static nfloat LeadingInsetFor(UIView view, nfloat inherited)
        => InsetFor(view, inherited, leading: true);

    /// <summary>
    /// How much TOP inset the view still needs, given what it already inherits. This is what a
    /// full-height start drawer takes: insetting its leading edge instead would waste the panel's
    /// width down its whole height for controls that only cover its corner.
    /// </summary>
    public static nfloat TopInsetFor(UIView view, nfloat inherited)
        => InsetFor(view, inherited, leading: false);

    private static nfloat InsetFor(UIView view, nfloat inherited, bool leading)
    {
        if (!IsActive(view))
        {
            return 0;
        }

        var rect = RestingRectInWindow(view);

        if (CGRect.Intersect(rect, _inWindow).IsEmpty)
        {
            return 0;
        }

        // Distance from THIS view's own edge to the controls' far edge...
        var needed = leading
            ? _inWindow.Right - rect.Left
            : _inWindow.Bottom - rect.Top;

        // ...minus what the view is already inset by. A drawer panel inherits the status bar's
        // 32pt top; adding the controls' full 65 on top of it double-counts, and the gap before
        // the first menu entry then reads as a layout mistake — because it is one.
        var remaining = needed - inherited;

        return remaining > 0 ? remaining : 0;
    }
}
