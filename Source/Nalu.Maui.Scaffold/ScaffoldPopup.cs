namespace Nalu;

/// <summary>
/// The lifetime handle of a presented popup or bottom sheet: close it programmatically, observe
/// when it closes (for ANY reason — <see cref="CloseAsync"/>, scrim tap, system back, pull-down,
/// or a navigation dismissing all overlays), or bind its lifetime to a scope with
/// <c>await using</c>.
/// </summary>
/// <remarks>
/// This is the low-level presentation contract: it shows a view and reports its lifetime.
/// Result passing, view-model wiring and similar MVVM concerns are abstractions to build on
/// top of it.
/// </remarks>
public interface IScaffoldPopup : IAsyncDisposable
{
    /// <summary>Gets whether the popup is still presented.</summary>
    bool IsOpen { get; }

    /// <summary>Gets a task completing when the popup has closed, whatever the close path.</summary>
    Task Closed { get; }

    /// <summary>Closes the popup. Idempotent — a no-op when already closed.</summary>
    Task CloseAsync();
}

/// <summary>Where a popup is placed within the safe presentation area.</summary>
public enum ScaffoldPopupPlacement
{
    /// <summary>Centered in the presentation area.</summary>
    Center,

    /// <summary>
    /// Below the <see cref="ScaffoldPopupOptions.Anchor"/>, start-aligned with it (dropdown);
    /// flips above when it doesn't fit.
    /// </summary>
    AnchorBelow,

    /// <summary>Above the anchor, start-aligned; flips below when it doesn't fit.</summary>
    AnchorAbove,

    /// <summary>At the start side of the anchor, top-aligned; flips to the end side when it doesn't fit.</summary>
    AnchorStart,

    /// <summary>At the end side of the anchor, top-aligned; flips to the start side when it doesn't fit.</summary>
    AnchorEnd
}

/// <summary>
/// Full custom popup placement: receives the safe presentation area, the measured content size
/// and the anchor bounds (when an anchor was provided), all in scaffold device-independent
/// coordinates, and returns the popup rectangle.
/// </summary>
public interface IScaffoldPopupPlacer
{
    /// <summary>Computes the popup rectangle.</summary>
    /// <param name="area">The safe presentation area (system insets excluded; chrome ignored).</param>
    /// <param name="contentSize">The measured content size (already constrained to the area).</param>
    /// <param name="anchorBounds">The anchor bounds when <see cref="ScaffoldPopupOptions.Anchor"/> is set.</param>
    Rect Place(Rect area, Size contentSize, Rect? anchorBounds);
}

/// <summary>
/// Presentation options of <see cref="Scaffold.ShowPopupAsync"/>. Popups render in the top
/// overlay layer — above all chrome and previously presented overlays (popup-over-popup
/// stacks) — and are unaffected by the tab bar's safe-area footprint: only SYSTEM insets shape
/// the presentation area.
/// </summary>
public sealed class ScaffoldPopupOptions
{
    /// <summary>
    /// Gets or sets the scrim brush behind the popup (gradients supported). Defaults to a
    /// theme-aware translucent black; use a fully transparent brush for dropdown-style popups —
    /// the scrim always blocks interaction with the content below either way.
    /// </summary>
    public Brush? Scrim { get; init; }

    /// <summary>Gets or sets whether tapping the scrim closes the popup. Defaults to true.</summary>
    public bool CloseOnScrimTap { get; init; } = true;

    /// <summary>
    /// Gets or sets whether the system back gesture closes the popup. Defaults to true; when
    /// false, back is consumed without closing while this popup is topmost.
    /// </summary>
    public bool CloseOnBack { get; init; } = true;

    /// <summary>
    /// Gets or sets the minimum gap kept between the popup and the safe-area edges (the
    /// placement area shrinks by it). Defaults to 16. To cap the popup's own size, set
    /// <see cref="VisualElement.MaximumWidthRequest"/> /
    /// <see cref="VisualElement.MaximumHeightRequest"/> on the content — both participate in
    /// the popup measure.
    /// </summary>
    public Thickness Margin { get; init; } = new(16);

    /// <summary>Gets or sets the placement. Anchor placements require <see cref="Anchor"/>.</summary>
    public ScaffoldPopupPlacement Placement { get; init; } = ScaffoldPopupPlacement.Center;

    /// <summary>Gets or sets the view the anchor placements position relative to.</summary>
    public View? Anchor { get; init; }

    /// <summary>Gets or sets an extra offset applied by the anchor placements.</summary>
    public Point AnchorOffset { get; init; }

    /// <summary>Gets or sets a fully custom placement, overriding <see cref="Placement"/>.</summary>
    public IScaffoldPopupPlacer? CustomPlacer { get; init; }
}
