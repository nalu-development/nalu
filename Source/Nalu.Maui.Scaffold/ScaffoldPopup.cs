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
    public bool? CloseOnScrimTap { get; init; }

    /// <summary>
    /// Gets or sets whether the system back gesture closes the popup. Defaults to true; when
    /// false, back is consumed without closing while this popup is topmost.
    /// </summary>
    public bool? CloseOnBack { get; init; }

    /// <summary>
    /// Gets or sets the minimum gap kept between the popup and the safe-area edges (the
    /// placement area shrinks by it). Defaults to 16. To cap the popup's own size, set
    /// <see cref="VisualElement.MaximumWidthRequest"/> /
    /// <see cref="VisualElement.MaximumHeightRequest"/> on the content — both participate in
    /// the popup measure.
    /// </summary>
    public Thickness? Margin { get; init; }

    /// <summary>Gets or sets the placement. Anchor placements require <see cref="Anchor"/>. Defaults to Center.</summary>
    public ScaffoldPopupPlacement? Placement { get; init; }

    /// <summary>Gets or sets the view the anchor placements position relative to.</summary>
    public View? Anchor { get; init; }

    /// <summary>Gets or sets an extra offset applied by the anchor placements.</summary>
    public Point AnchorOffset { get; init; }

    /// <summary>Gets or sets a fully custom placement, overriding <see cref="Placement"/>.</summary>
    public IScaffoldPopupPlacer? CustomPlacer { get; init; }
}

/// <summary>
/// Attached presentation properties declared on a popup CONTENT view — the view states how it
/// prefers to be presented, right where it is defined (XAML-friendly, styleable). Call-site
/// <see cref="ScaffoldPopupOptions"/> override per property: a set option wins over the
/// attached value, which wins over the built-in default.
/// </summary>
public static class ScaffoldPopup
{
    /// <summary>Attached counterpart of <see cref="ScaffoldPopupOptions.Placement"/>.</summary>
    public static readonly BindableProperty PlacementProperty =
        BindableProperty.CreateAttached("Placement", typeof(ScaffoldPopupPlacement?), typeof(ScaffoldPopup), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldPopupOptions.Scrim"/>.</summary>
    public static readonly BindableProperty ScrimProperty =
        BindableProperty.CreateAttached("Scrim", typeof(Brush), typeof(ScaffoldPopup), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldPopupOptions.Margin"/>.</summary>
    public static readonly BindableProperty MarginProperty =
        BindableProperty.CreateAttached("Margin", typeof(Thickness?), typeof(ScaffoldPopup), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldPopupOptions.CloseOnScrimTap"/>.</summary>
    public static readonly BindableProperty CloseOnScrimTapProperty =
        BindableProperty.CreateAttached("CloseOnScrimTap", typeof(bool?), typeof(ScaffoldPopup), null);

    /// <summary>Attached counterpart of <see cref="ScaffoldPopupOptions.CloseOnBack"/>.</summary>
    public static readonly BindableProperty CloseOnBackProperty =
        BindableProperty.CreateAttached("CloseOnBack", typeof(bool?), typeof(ScaffoldPopup), null);

    /// <summary>Gets the attached placement.</summary>
    public static ScaffoldPopupPlacement? GetPlacement(BindableObject view) => (ScaffoldPopupPlacement?)view.GetValue(PlacementProperty);

    /// <summary>Sets the attached placement.</summary>
    public static void SetPlacement(BindableObject view, ScaffoldPopupPlacement? value) => view.SetValue(PlacementProperty, value);

    /// <summary>Gets the attached scrim brush.</summary>
    public static Brush? GetScrim(BindableObject view) => (Brush?)view.GetValue(ScrimProperty);

    /// <summary>Sets the attached scrim brush.</summary>
    public static void SetScrim(BindableObject view, Brush? value) => view.SetValue(ScrimProperty, value);

    /// <summary>Gets the attached margin.</summary>
    public static Thickness? GetMargin(BindableObject view) => (Thickness?)view.GetValue(MarginProperty);

    /// <summary>Sets the attached margin.</summary>
    public static void SetMargin(BindableObject view, Thickness? value) => view.SetValue(MarginProperty, value);

    /// <summary>Gets the attached scrim-tap dismissal policy.</summary>
    public static bool? GetCloseOnScrimTap(BindableObject view) => (bool?)view.GetValue(CloseOnScrimTapProperty);

    /// <summary>Sets the attached scrim-tap dismissal policy.</summary>
    public static void SetCloseOnScrimTap(BindableObject view, bool? value) => view.SetValue(CloseOnScrimTapProperty, value);

    /// <summary>Gets the attached back dismissal policy.</summary>
    public static bool? GetCloseOnBack(BindableObject view) => (bool?)view.GetValue(CloseOnBackProperty);

    /// <summary>Sets the attached back dismissal policy.</summary>
    public static void SetCloseOnBack(BindableObject view, bool? value) => view.SetValue(CloseOnBackProperty, value);
}
