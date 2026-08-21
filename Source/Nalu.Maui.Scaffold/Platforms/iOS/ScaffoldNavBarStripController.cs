using CoreGraphics;
using UIKit;

namespace Nalu;

/// <summary>
/// Owns ONE page's nav bar strip as a view controller, so the bar's subtree can carry a safe area
/// of its own — which is the only way to keep the system windowing controls off the bar's content
/// without moving the page with it.
/// </summary>
/// <remarks>
/// <para>
/// iPadOS 26 makes every app a resizable window and draws the system windowing controls (the
/// "traffic lights") over its top-leading corner, on top of whatever the app draws there — which
/// for a scaffold is the nav bar's leading buttons and the start of its title.
/// </para>
/// <para>
/// Nothing reports where they are. UIKit publishes no leading inset for them (a windowed scene
/// reports <c>L0 T32 R0 B20</c> with the controls plainly on screen), they are hosted outside the
/// app's window (walking the window's subtree finds only the app's own views), and
/// <see cref="UISceneWindowingControlStyle"/> — the one iOS 26 API about them — chooses a style,
/// never a frame. So their geometry is a MEASURED CONSTANT, see <see cref="_controlsInWindow"/>.
/// </para>
/// <para>
/// The strip becomes a controller's view because <see cref="UIViewController.AdditionalSafeAreaInsets"/>
/// applies to one controller's subtree: the inset reaches the bar (which consumes the container
/// safe area already, so the default bar and any custom bar that respects safe areas both shift
/// their content and keep painting their background full width) and NOT the page, which is a
/// sibling child controller. The controller's view IS the strip — no extra level, so the strip's
/// measure-invalidation contract with MAUI is unchanged.
/// </para>
/// </remarks>
internal sealed class ScaffoldNavBarStripController(ScaffoldNavBarStrip strip) : UIViewController
{
    /// <summary>
    /// The controls' rect in WINDOW coordinates, measured on an iPad Pro 11" running iPadOS 26 by
    /// tracing the pixel transitions around the capsule: x 21→62, y 43→65 (41x22 at 21,43). It is
    /// the same rect windowed and full-screen, and it does not scale with the window — the system
    /// draws one fixed control cluster in the window's top-leading corner.
    /// </summary>
    private static readonly CGRect _controlsInWindow = new(21, 43, 41, 22);

    public ScaffoldNavBarStrip Strip { get; } = strip;

    /// <summary>The strip IS this controller's view: containment adds a controller, not a view level.</summary>
    public override void LoadView() => View = Strip;

    /// <summary>
    /// Re-reads the inset the windowing controls impose on this strip. Called from the container's
    /// layout pass — the geometry it depends on (the window's size on screen, the strip's own
    /// position) only changes with a layout.
    /// </summary>
    /// <returns>Whether the inset changed, in which case the bar needs re-measuring.</returns>
    public bool UpdateWindowControlsInset()
    {
        var inset = ComputeLeadingInset();

        // Writing AdditionalSafeAreaInsets re-dirties the whole subtree, so only on a real change.
        if (Math.Abs(AdditionalSafeAreaInsets.Left - inset) < 0.5)
        {
            return false;
        }

        AdditionalSafeAreaInsets = new UIEdgeInsets(0, inset, 0, 0);

        return true;
    }

    private nfloat ComputeLeadingInset()
    {
        if (!OperatingSystem.IsIOSVersionAtLeast(26) || UIDevice.CurrentDevice.UserInterfaceIdiom != UIUserInterfaceIdiom.Pad)
        {
            return 0;
        }

        if (Strip.Window is not { } window || window.WindowScene?.Screen is not { } screen)
        {
            return 0;
        }

        // WINDOWED is the case the controls are permanently on screen for. A full-screen app gets
        // them transiently instead (they appear near the corner and hide again), and reserving the
        // band forever for something usually absent would cost every full-screen iPad app its
        // leading bar space. The window's own frame is expressed in its own space, so the only way
        // to learn it is a smaller rectangle on the screen is to convert through the screen's
        // coordinate space.
        var onScreen = window.ConvertRectToCoordinateSpace(window.Bounds, screen.CoordinateSpace);

        if (onScreen.Size == screen.Bounds.Size)
        {
            return 0;
        }

        // The strip's UNTRANSFORMED rect: a hidden bar rests translated by its own height, and the
        // inset must not flip while a show/hide slide is in flight. Center and bounds are both
        // untouched by the transform, so they describe where the strip actually lives.
        var bounds = Strip.Bounds;
        var center = Strip.Center;
        var resting = new CGRect(center.X - (bounds.Width / 2), center.Y - (bounds.Height / 2), bounds.Width, bounds.Height);
        var inWindow = Strip.Superview?.ConvertRectToView(resting, null) ?? resting;

        // Geometric rather than assumed: a bar only pays for the controls it actually reaches
        // under. A page presented as a sheet, or any strip that starts below or right of them,
        // gets nothing.
        if (CGRect.Intersect(inWindow, _controlsInWindow).IsEmpty)
        {
            return 0;
        }

        var inset = _controlsInWindow.Right - inWindow.Left;

        return inset > 0 ? inset : 0;
    }
}
