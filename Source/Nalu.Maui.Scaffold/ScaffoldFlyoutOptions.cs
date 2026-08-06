namespace Nalu;

/// <summary>
/// Styling of a scaffold drawer side (<see cref="Scaffold.FlyoutStartOptions"/> /
/// <see cref="Scaffold.FlyoutEndOptions"/>). Scaffold-level only by design — drawer styling is
/// chrome, not page content. Values apply when the drawer is next opened; the defaults
/// reproduce the presenters' original metrics.
/// </summary>
public sealed class ScaffoldFlyoutOptions : BindableObject
{
    /// <summary>The defaults used when a side has no options configured.</summary>
    internal static readonly ScaffoldFlyoutOptions Default = new();

    /// <summary>Bindable property for <see cref="Width"/>.</summary>
    public static readonly BindableProperty WidthProperty =
        BindableProperty.Create(nameof(Width), typeof(double), typeof(ScaffoldFlyoutOptions), -1d);

    /// <summary>Bindable property for <see cref="WidthRatio"/>.</summary>
    public static readonly BindableProperty WidthRatioProperty =
        BindableProperty.Create(nameof(WidthRatio), typeof(double), typeof(ScaffoldFlyoutOptions), 0.85);

    /// <summary>Bindable property for <see cref="MaximumWidth"/>.</summary>
    public static readonly BindableProperty MaximumWidthProperty =
        BindableProperty.Create(nameof(MaximumWidth), typeof(double), typeof(ScaffoldFlyoutOptions), 360d);

    /// <summary>Bindable property for <see cref="Scrim"/>.</summary>
    public static readonly BindableProperty ScrimProperty =
        BindableProperty.Create(nameof(Scrim), typeof(Brush), typeof(ScaffoldFlyoutOptions), null);

    /// <summary>
    /// Gets or sets the explicit drawer width in device-independent units; it wins over the
    /// ratio-based sizing when zero or positive. Defaults to -1 (ratio-based).
    /// </summary>
    public double Width
    {
        get => (double)GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the drawer width as a fraction of the window width, used while
    /// <see cref="Width"/> is negative. Defaults to 0.85.
    /// </summary>
    public double WidthRatio
    {
        get => (double)GetValue(WidthRatioProperty);
        set => SetValue(WidthRatioProperty, value);
    }

    /// <summary>
    /// Gets or sets the cap applied to the ratio-based width, in device-independent units.
    /// Defaults to 360.
    /// </summary>
    public double MaximumWidth
    {
        get => (double)GetValue(MaximumWidthProperty);
        set => SetValue(MaximumWidthProperty, value);
    }

    /// <summary>
    /// Gets or sets the scrim brush behind the open drawer (gradients supported). Null (the
    /// default) uses the built-in translucent black.
    /// </summary>
    public Brush? Scrim
    {
        get => (Brush?)GetValue(ScrimProperty);
        set => SetValue(ScrimProperty, value);
    }

    // EdgeSwipeEnabled is reserved for this object: it arrives with the P2 gesture work.

    /// <summary>Resolves the effective drawer width for the given container width.</summary>
    internal double ComputeWidth(double containerWidth)
        => Width >= 0 ? Width : Math.Min(containerWidth * WidthRatio, MaximumWidth);

    /// <summary>Resolves the effective scrim brush.</summary>
    internal Brush ComputeScrim() => Scrim ?? new SolidColorBrush(Colors.Black.WithAlpha(0.4f));
}
