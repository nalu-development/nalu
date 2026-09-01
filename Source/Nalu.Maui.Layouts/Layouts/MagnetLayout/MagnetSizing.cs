using System.ComponentModel;
using System.Globalization;

namespace Nalu;

/// <summary>
/// The size of a <see cref="MagnetView" /> along one axis.
/// </summary>
/// <param name="Unit">How the size is resolved.</param>
/// <param name="Value">The fixed size, the percent fraction (0..1) or the ratio, depending on <paramref name="Unit" />.</param>
/// <param name="Min">Minimum size (applied to every unit).</param>
/// <param name="Max">Maximum size (applied to every unit; also used as an upper measure constraint for <see cref="MagnetSizingUnit.Measured" />).</param>
/// <remarks>
/// XAML: <c>"48"</c> (fixed dp), <c>"50%"</c> (fraction of the anchor span), <c>"*"</c> (fills the anchor span); every other
/// unit and the bounds use the markup extension: <c>{nalu:MagnetSizing 1.5, Unit=Ratio}</c>,
/// <c>{nalu:MagnetSizing 0.5, Unit=StagePercent}</c>, <c>{nalu:MagnetSizing Unit=Constraint, Max=320}</c>.
/// </remarks>
[TypeConverter(typeof(MagnetSizingTypeConverter))]
public readonly record struct MagnetSizing(MagnetSizingUnit Unit, double Value = 0, double Min = 0, double Max = double.PositiveInfinity)
{
    /// <summary>
    /// The default size: the measured size of the view.
    /// </summary>
    public static readonly MagnetSizing Measured = new(MagnetSizingUnit.Measured);

    /// <summary>
    /// Fills the space between the two anchors of the axis.
    /// </summary>
    public static readonly MagnetSizing Constraint = new(MagnetSizingUnit.Constraint);

    /// <summary>
    /// A fixed size.
    /// </summary>
    public static MagnetSizing Fixed(double value) => new(MagnetSizingUnit.Fixed, value);

    /// <summary>
    /// A fraction (0..1) of the space between the two anchors of the axis.
    /// </summary>
    public static MagnetSizing Percent(double fraction) => new(MagnetSizingUnit.ConstraintPercent, fraction);

    /// <summary>
    /// A ratio relative to the other axis size.
    /// </summary>
    public static MagnetSizing Ratio(double ratio) => new(MagnetSizingUnit.Ratio, ratio);

    /// <summary>
    /// A fraction (0..1) of the stage size on the same axis, regardless of the anchors.
    /// </summary>
    public static MagnetSizing StagePercent(double fraction) => new(MagnetSizingUnit.StagePercent, fraction);

    /// <summary>
    /// The measured size multiplied by <paramref name="scale" />.
    /// </summary>
    public static MagnetSizing Scaled(double scale) => new(MagnetSizingUnit.Measured, scale);

    /// <summary>
    /// Returns a copy with the given bounds.
    /// </summary>
    public MagnetSizing WithBounds(double min = 0, double max = double.PositiveInfinity) => this with { Min = min, Max = max };

    /// <summary>
    /// Classifies the difference between this size and <paramref name="other" />.
    /// </summary>
    public MagnetChange DiffWith(in MagnetSizing other)
    {
        if (Unit != other.Unit)
        {
            return MagnetChange.Structure;
        }

        // Min/Max switching between "unbounded" and bounded changes the emitted instructions.
        var boundedMin = Min > 0;
        var boundedMax = !double.IsPositiveInfinity(Max);
        var otherBoundedMin = other.Min > 0;
        var otherBoundedMax = !double.IsPositiveInfinity(other.Max);

        if (boundedMin != otherBoundedMin || boundedMax != otherBoundedMax)
        {
            return MagnetChange.Structure;
        }

        return Value != other.Value || Min != other.Min || Max != other.Max ? MagnetChange.Values : MagnetChange.None;
    }

    /// <summary>
    /// Gets whether the size has an explicit minimum or maximum.
    /// </summary>
    public bool HasBounds => Min > 0 || !double.IsPositiveInfinity(Max);

    /// <summary>
    /// Parses the string representation of a size: <c>"48"</c> (fixed dp), <c>"50%"</c> (fraction of the anchor span) or
    /// <c>"*"</c> (fills the anchor span). Every other unit is expressed with the struct (<c>{nalu:MagnetSizing 1.5, Unit=Ratio}</c>).
    /// </summary>
    public static MagnetSizing Parse(string value)
    {
        var main = value.Trim();

        if (main.Length == 0)
        {
            return Measured;
        }

        if (main == "*")
        {
            return Constraint;
        }

        if (main.EndsWith('%'))
        {
            return Percent(double.Parse(main.AsSpan(0, main.Length - 1), CultureInfo.InvariantCulture) / 100);
        }

        if (double.TryParse(main, NumberStyles.Float, CultureInfo.InvariantCulture, out var fixedValue))
        {
            return Fixed(fixedValue);
        }

        throw new FormatException($"Invalid MagnetSizing '{value}': expected a number (dp), 'N%' or '*'; use {{nalu:MagnetSizing ...}} for the other units.");
    }

    /// <summary>
    /// Implicitly parses the XAML representation of a size.
    /// </summary>
    public static implicit operator MagnetSizing(string value) => Parse(value);

    /// <summary>
    /// Implicitly converts a number to a fixed size.
    /// </summary>
    public static implicit operator MagnetSizing(double value) => Fixed(value);

    /// <inheritdoc />
    public override string ToString()
    {
        var bounds = HasBounds ? $" [{Min.ToString(CultureInfo.InvariantCulture)}..{Max.ToString(CultureInfo.InvariantCulture)}]" : "";

        return Unit switch
        {
            MagnetSizingUnit.Fixed when !HasBounds => Value.ToString(CultureInfo.InvariantCulture),
            MagnetSizingUnit.Constraint when !HasBounds => "*",
            MagnetSizingUnit.ConstraintPercent when !HasBounds => $"{(Value * 100).ToString(CultureInfo.InvariantCulture)}%",
            MagnetSizingUnit.Measured when Value <= 0 || Value == 1 => $"Measured{bounds}",
            _ => $"{Unit} {Value.ToString(CultureInfo.InvariantCulture)}{bounds}"
        };
    }
}

/// <summary>
/// Type converter for <see cref="MagnetSizing" />.
/// </summary>
public class MagnetSizingTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string s ? MagnetSizing.Parse(s) : throw new NotSupportedException();

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is MagnetSizing s ? s.ToString() : throw new NotSupportedException();
}
