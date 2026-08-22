using Nalu.Internals;

namespace Nalu;

/// <summary>
/// Reserves the space the SYSTEM WINDOW CONTROLS take over a window's top-leading corner, and
/// nothing at all where there are none — zero size on every platform and every state that has no
/// such controls, so it can sit in a layout unconditionally.
/// </summary>
/// <remarks>
/// <para>
/// iPadOS 26 makes every app a resizable window and, while it IS a window, draws the system
/// windowing controls (the "traffic lights") over its top-leading corner, on top of whatever the
/// app draws there. Anything an app puts in that corner — a nav bar's leading buttons, the first
/// entry of a start-edge drawer, a custom header — is covered.
/// </para>
/// <para>
/// Nothing reports where the controls are: UIKit publishes no inset for them (a windowed scene
/// reports a plain <c>L0 T32 R0 B20</c> while they are on screen, and they sit BELOW that top
/// inset), they are hosted outside the app's window so the app cannot find them in its own view
/// tree, and iOS 26's <c>UISceneWindowingControlStyle</c> — the only API about them — selects a
/// style, never a frame. Their footprint is therefore a measured constant, taken from an
/// iPad Pro 11" running iPadOS 26 and identical windowed or full-screen.
/// </para>
/// <para>
/// Place it FLUSH in the corner it protects — as the first child of a leading row, or the first
/// child of a top-anchored column — and it reserves exactly up to the controls' far edge. Padding
/// around it adds to that clearance rather than replacing it.
/// </para>
/// <para>
/// FULL-SCREEN windows reserve nothing: there the controls are transient (they appear near the
/// corner and hide again), and holding the band open permanently for something usually absent
/// would cost every full-screen iPad app its leading space.
/// </para>
/// </remarks>
/// <example>
/// <code language="xml">
/// &lt;HorizontalStackLayout&gt;
///     &lt;nalu:ScaffoldSystemWindowControlsSpacer /&gt;
///     &lt;Button Text="Menu" /&gt;
/// &lt;/HorizontalStackLayout&gt;
/// </code>
/// </example>
public sealed class ScaffoldSystemWindowControlsSpacer : BoxView
{
    /// <summary>
    /// Distance from the window's LEADING edge to the far edge of the controls, and from its TOP
    /// edge to their bottom: the controls occupy x 21..62, y 43..65 in window coordinates.
    /// </summary>
    private const double _controlsTrailingEdge = 62;
    private const double _controlsBottomEdge = 65;

    /// <summary>Bindable property for <see cref="Orientation"/>.</summary>
    public static readonly BindableProperty OrientationProperty =
        GenericBindableProperty<ScaffoldSystemWindowControlsSpacer>.Create(
            nameof(Orientation),
            StackOrientation.Horizontal,
            propertyChanged: static spacer => (_, _) => spacer.Update()
        );

    /// <summary>
    /// Gets or sets which dimension is reserved: <see cref="StackOrientation.Horizontal"/> (the
    /// default) pushes content to the RIGHT of the controls — what a nav bar wants, its height
    /// being the bar's business — and <see cref="StackOrientation.Vertical"/> pushes content
    /// BELOW them, which is what a full-height start drawer wants, its width being the panel's.
    /// </summary>
    public StackOrientation Orientation
    {
        get => (StackOrientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Initializes a new <see cref="ScaffoldSystemWindowControlsSpacer"/>.</summary>
    public ScaffoldSystemWindowControlsSpacer()
    {
        // It reserves space and never paints or takes a touch: the controls are drawn over it by
        // the system, and everything else there belongs to whatever is underneath.
        Color = Colors.Transparent;
        InputTransparent = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        Update();
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        // The window's size is what says "windowed", so its changes are what re-evaluate this:
        // resizing, and moving between windowed and full-screen, both land here. The display's
        // own change covers a rotation, which redefines what "full-screen" measures.
        if (Window is { } window)
        {
            window.SizeChanged += OnGeometryChanged;
        }

        DeviceDisplay.MainDisplayInfoChanged += OnDisplayInfoChanged;

        Update();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        if (Window is { } window)
        {
            window.SizeChanged -= OnGeometryChanged;
        }

        DeviceDisplay.MainDisplayInfoChanged -= OnDisplayInfoChanged;
    }

    private void OnGeometryChanged(object? sender, EventArgs e) => Update();

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e) => Update();

    private void Update()
    {
        var reserved = ReservedSize();
        var horizontal = Orientation == StackOrientation.Horizontal;

        // Hidden rather than zero-sized: a stack layout skips an invisible child entirely, so an
        // inactive spacer cannot contribute spacing of its own.
        IsVisible = reserved > 0;
        WidthRequest = horizontal ? reserved : 0;
        HeightRequest = horizontal ? 0 : reserved;
    }

    private double ReservedSize()
    {
        // Idiom and version gate first: no other platform draws controls over the app's corner,
        // and the iOS check is false everywhere else, so this costs nothing off iPad.
        if (!OperatingSystem.IsIOSVersionAtLeast(26) || DeviceInfo.Idiom != DeviceIdiom.Tablet)
        {
            return 0;
        }

        if (Window is not { } window)
        {
            return 0;
        }

        var display = DeviceDisplay.MainDisplayInfo;

        if (display.Density <= 0)
        {
            return 0;
        }

        // A window smaller than the screen IS a window — the state whose controls are permanent.
        // Both sides are compared in device-independent units; the display reports pixels.
        var screenWidth = display.Width / display.Density;
        var screenHeight = display.Height / display.Density;
        var windowed = window.Width < screenWidth - 1 || window.Height < screenHeight - 1;

        if (!windowed)
        {
            return 0;
        }

        return Orientation == StackOrientation.Horizontal ? _controlsTrailingEdge : _controlsBottomEdge;
    }
}
