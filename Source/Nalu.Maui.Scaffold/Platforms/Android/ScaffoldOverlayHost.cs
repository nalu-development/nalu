using Android.Content;
using Android.Views;
using Android.Widget;
using AndroidX.Core.View;
using Insets = AndroidX.Core.Graphics.Insets;

namespace Nalu;

/// <summary>
/// IME isolation for keyboard-aware overlay subtrees (bottom sheets, popups): the presenter
/// positions the overlay ABOVE the keyboard, so nothing inside it must react to the IME insets
/// on its own. MAUI's net10 inset listener would otherwise pad the overlay's content by the
/// keyboard's overlap with the view's on-screen position (<c>SafeAreaEdges</c> defaults include
/// SoftInput) — evaluated at dispatch time, i.e. BEFORE the overlay has moved out of the way, and
/// kept until the next dispatch. Same isolation pattern as the VirtualScroll safe-area layer.
/// </summary>
internal static class ScaffoldOverlayImeIsolation
{
    /// <summary>Returns the insets with the IME zeroed and hidden; the same instance when there is no IME.</summary>
    public static WindowInsets? StripIme(Android.Views.View host, WindowInsets? insets)
    {
        if (insets is null)
        {
            return null;
        }

        var compat = WindowInsetsCompat.ToWindowInsetsCompat(insets, host);
        var imeType = WindowInsetsCompat.Type.Ime();

        if (compat is null || (!compat.IsVisible(imeType) && (compat.GetInsets(imeType)?.Bottom ?? 0) == 0))
        {
            return insets;
        }

        return new WindowInsetsCompat.Builder(compat)
               .SetInsets(imeType, Insets.None)!
               .SetVisible(imeType, false)!
               .Build()?
               .ToWindowInsets()
               ?? insets;
    }
}

/// <summary>
/// Android host of a popup's platform view: a match-parent slot the presenter positions with
/// layout params, and the INSET boundary of the popup subtree: nothing reaches the content —
/// neither the IME (<see cref="ScaffoldOverlayImeIsolation"/>) nor the system bars / cutout.
/// The presenter places the popup INSIDE the safe area already, so any self-padding by MAUI's
/// net10 inset listener would double it — and that listener evaluates a layout against its
/// window position at dispatch time, i.e. possibly before the popup is where it will end up
/// (a root layout that "sees" itself at the window's top edge pads by the status bar and keeps
/// it until the next dispatch: content pushed down, bottom cut off — a race that used to show
/// randomly on presentation).
/// </summary>
internal sealed class ScaffoldPopupHost(Context context) : FrameLayout(context), IScaffoldOverlayPanelHost
{
    /// <inheritdoc />
    public bool PanelDirty { get; set; }

    /// <inheritdoc />
    public override WindowInsets? DispatchApplyWindowInsets(WindowInsets? insets)
        => base.DispatchApplyWindowInsets(insets is null ? null : WindowInsetsCompat.Consumed?.ToWindowInsets() ?? insets);

    /// <summary>
    /// A descendant's <c>requestLayout()</c> bubbles here natively (an image that finished
    /// loading, an expanded section): mark only — the presenter re-measures and re-places the
    /// popup in the container's measure pass (<see cref="ScaffoldLayout.OverlayMeasurePass"/>).
    /// </summary>
    public override void RequestLayout()
    {
        PanelDirty = true;
        base.RequestLayout();
    }
}

/// <summary>
/// The Android hosts of popup and sheet panels: <c>requestLayout()</c> bubbling from the hosted
/// content (the native invalidation channel) marks the panel dirty; the presenter consumes the
/// flag in the container's measure pass — measuring inside the invalidation is never done.
/// </summary>
internal interface IScaffoldOverlayPanelHost
{
    /// <summary>Set by <c>requestLayout()</c> (host or any descendant); cleared by the presenter once re-placed.</summary>
    bool PanelDirty { get; set; }
}
