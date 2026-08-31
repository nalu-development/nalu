using System.ComponentModel;
using System.Globalization;

namespace Nalu;

/// <summary>
/// A target node with an optional margin, used by the relative shortcuts (<c>After</c>, <c>Below</c>, <c>AlignLeft</c>, …).
/// </summary>
/// <param name="Target">The <see cref="MagnetNode.MagnetId" /> of the target node, or <see cref="MagnetAnchor.Parent" />.</param>
/// <param name="Margin">The margin between the anchored side and the target.</param>
/// <param name="GoneMargin">The margin used when the target view is collapsed (defaults to <paramref name="Margin" />).</param>
/// <remarks>
/// String form (implicit conversion): <c>"avatar"</c>, <c>"avatar,12"</c>, <c>"avatar,12,0"</c> or <c>"avatar,12,gone:0"</c>.
/// </remarks>
[TypeConverter(typeof(MagnetTargetTypeConverter))]
public readonly record struct MagnetTarget(string Target, double Margin = 0, double? GoneMargin = null)
{
    /// <summary>
    /// Parses the string representation of a target.
    /// </summary>
    public static MagnetTarget Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("A MagnetTarget cannot be empty. Expected 'target[,margin[,goneMargin]]'.");
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        var target = parts[0];

        if (target.Length == 0)
        {
            throw new FormatException($"Invalid MagnetTarget '{value}': the target id is empty.");
        }

        var margin = parts.Length > 1 ? double.Parse(parts[1], CultureInfo.InvariantCulture) : 0;
        double? gone = null;

        if (parts.Length > 2)
        {
            var third = parts[2].StartsWith("gone:", StringComparison.OrdinalIgnoreCase) ? parts[2][5..] : parts[2];
            gone = double.Parse(third, CultureInfo.InvariantCulture);
        }

        if (parts.Length > 3)
        {
            throw new FormatException($"Invalid MagnetTarget '{value}': too many tokens.");
        }

        return new MagnetTarget(target, margin, gone);
    }

    /// <summary>
    /// Implicitly parses the string representation of a target.
    /// </summary>
    public static implicit operator MagnetTarget(string value) => Parse(value);

    /// <summary>
    /// Builds the anchor to the given pole of this target.
    /// </summary>
    public MagnetAnchor To(MagnetPole pole) => new(Target, pole, Margin, GoneMargin);

    /// <inheritdoc />
    public override string ToString()
    {
        var s = Target;

        if (Margin != 0 || GoneMargin is not null)
        {
            s += $",{Margin.ToString(CultureInfo.InvariantCulture)}";
        }

        if (GoneMargin is { } gone)
        {
            s += $",gone:{gone.ToString(CultureInfo.InvariantCulture)}";
        }

        return s;
    }
}

/// <summary>
/// Type converter for <see cref="MagnetTarget" />.
/// </summary>
public class MagnetTargetTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string s ? MagnetTarget.Parse(s) : throw new NotSupportedException();

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is MagnetTarget t ? t.ToString() : throw new NotSupportedException();
}
