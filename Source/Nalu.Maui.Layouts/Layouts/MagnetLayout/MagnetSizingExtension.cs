namespace Nalu;

/// <summary>
/// XAML markup extension producing a <see cref="MagnetSizing" />: <c>{nalu:MagnetSizing 48}</c> (fixed),
/// <c>{nalu:MagnetSizing 1.5, Unit=Ratio}</c>, <c>{nalu:MagnetSizing 0.5, Unit=StagePercent}</c>,
/// <c>{nalu:MagnetSizing Unit=Constraint, Max=320}</c>, <c>{nalu:MagnetSizing 1.5, Unit=Measured}</c> (1.5 × measured).
/// </summary>
[ContentProperty(nameof(Value))]
[AcceptEmptyServiceProvider]
public sealed class MagnetSizingExtension : IMarkupExtension<MagnetSizing>
{
    /// <summary>Gets or sets the value: dp for <see cref="MagnetSizingUnit.Fixed" />, a fraction for the percent units, the ratio, or the measured scale.</summary>
    public double Value { get; set; }

    /// <summary>Gets or sets the unit (defaults to <see cref="MagnetSizingUnit.Fixed" />).</summary>
    public MagnetSizingUnit Unit { get; set; } = MagnetSizingUnit.Fixed;

    /// <summary>Gets or sets the minimum size.</summary>
    public double Min { get; set; }

    /// <summary>Gets or sets the maximum size.</summary>
    public double Max { get; set; } = double.PositiveInfinity;

    /// <inheritdoc />
    public MagnetSizing ProvideValue(IServiceProvider serviceProvider) => new(Unit, Value, Min, Max);

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider) => ProvideValue(serviceProvider);
}
