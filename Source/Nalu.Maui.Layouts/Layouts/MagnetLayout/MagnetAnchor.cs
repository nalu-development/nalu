using System.ComponentModel;
using System.Globalization;

namespace Nalu;

/// <summary>
/// An anchor of a <see cref="MagnetView" /> side to a pole of another node (or of the <c>parent</c> stage).
/// </summary>
/// <param name="Target">The <see cref="MagnetNode.MagnetId" /> of the target node, or <see cref="Parent" />.</param>
/// <param name="Pole">The target pole.</param>
/// <param name="Margin">The margin between the anchored side and the target pole.</param>
/// <param name="GoneMargin">The margin used when the target view is collapsed (defaults to <paramref name="Margin" />).</param>
/// <remarks>
/// XAML syntax: <c>"target.Pole"</c>, <c>"target.Pole,margin"</c> or <c>"target.Pole,margin,gone:goneMargin"</c>,
/// e.g. <c>"parent.Left"</c>, <c>"avatar.Right,12"</c>, <c>"avatar.Right,12,gone:0"</c>.
/// </remarks>
[TypeConverter(typeof(MagnetAnchorTypeConverter))]
public readonly record struct MagnetAnchor(string Target, MagnetPole Pole, double Margin = 0, double? GoneMargin = null)
{
    /// <summary>
    /// The reserved identifier of the stage (the <see cref="Magnet" /> layout content area).
    /// </summary>
    public const string Parent = "parent";

    /// <summary>
    /// Gets the effective margin used when the target is collapsed.
    /// </summary>
    public double EffectiveGoneMargin => GoneMargin ?? Margin;

    /// <summary>
    /// Gets whether this anchor targets the stage.
    /// </summary>
    public bool TargetsParent => string.Equals(Target, Parent, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets whether the pole belongs to the horizontal axis.
    /// </summary>
    public bool IsHorizontal => Pole is MagnetPole.Left or MagnetPole.Right;

    /// <summary>
    /// Classifies the difference between this anchor and <paramref name="other" />.
    /// </summary>
    public MagnetChange DiffWith(in MagnetAnchor other)
    {
        if (!string.Equals(Target, other.Target, StringComparison.Ordinal) || Pole != other.Pole)
        {
            return MagnetChange.Structure;
        }

        return Margin != other.Margin || EffectiveGoneMargin != other.EffectiveGoneMargin ? MagnetChange.Values : MagnetChange.None;
    }

    /// <summary>
    /// Classifies the difference between two optional anchors.
    /// </summary>
    public static MagnetChange Diff(MagnetAnchor? a, MagnetAnchor? b)
    {
        if (a is null && b is null)
        {
            return MagnetChange.None;
        }

        if (a is null || b is null)
        {
            return MagnetChange.Structure;
        }

        return a.Value.DiffWith(b.Value);
    }

    /// <summary>
    /// Parses the XAML representation of an anchor.
    /// </summary>
    public static MagnetAnchor Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException("A MagnetAnchor cannot be empty. Expected 'target.Pole[,margin[,gone:goneMargin]]'.");
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        var dot = parts[0].LastIndexOf('.');

        if (dot <= 0 || dot == parts[0].Length - 1)
        {
            throw new FormatException($"Invalid MagnetAnchor '{value}': expected 'target.Pole[,margin[,gone:goneMargin]]'.");
        }

        var target = parts[0][..dot];
        var poleName = parts[0][(dot + 1)..];

        if (!Enum.TryParse<MagnetPole>(poleName, true, out var pole))
        {
            throw new FormatException($"Invalid MagnetAnchor '{value}': unknown pole '{poleName}'.");
        }

        var margin = 0d;
        double? gone = null;

        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];

            if (part.StartsWith("gone:", StringComparison.OrdinalIgnoreCase))
            {
                gone = double.Parse(part.AsSpan(5), CultureInfo.InvariantCulture);
            }
            else if (i == 1)
            {
                margin = double.Parse(part, CultureInfo.InvariantCulture);
            }
            else
            {
                throw new FormatException($"Invalid MagnetAnchor '{value}': unexpected token '{part}'.");
            }
        }

        return new MagnetAnchor(target, pole, margin, gone);
    }

    /// <summary>
    /// Implicitly parses the XAML representation of an anchor.
    /// </summary>
    public static implicit operator MagnetAnchor(string value) => Parse(value);

    /// <inheritdoc />
    public override string ToString()
    {
        var s = $"{Target}.{Pole}";

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
/// Type converter for <see cref="MagnetAnchor" />.
/// </summary>
public class MagnetAnchorTypeConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) => sourceType == typeof(string);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType) => destinationType == typeof(string);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        => value is string s ? MagnetAnchor.Parse(s) : throw new NotSupportedException();

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is MagnetAnchor a ? a.ToString() : throw new NotSupportedException();
}
