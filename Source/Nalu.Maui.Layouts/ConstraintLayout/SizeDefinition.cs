namespace Nalu;

using System.Globalization;

/// <summary>
/// Describes how a size should be calculated proportionally.
/// </summary>
/// <param name="Match">Describes in relation to what the size should be calculated.</param>
/// <param name="Multiplier">The multiplier for the size.</param>
public record SizeDefinition(SizeUnit Match, double Multiplier = 1d)
{
    /// <summary>
    /// Converts a string to a size definition.
    /// </summary>
    /// <param name="def">The size definition.</param>
    public static implicit operator SizeDefinition(string? def)
    {
        if (string.IsNullOrWhiteSpace(def))
        {
            return Auto;
        }

        if (_presets.TryGetValue(def, out var preset))
        {
            return preset;
        }

        def = def.Trim().ToUpperInvariant();
        var last = def[^1];
        SizeUnit? match = last switch
        {
            'M' => SizeUnit.Measured,
            'U' => SizeUnit.MeasuredUnconstrained,
            'P' => SizeUnit.Parent,
            'C' => SizeUnit.Constraint,
            'R' => SizeUnit.Ratio,
            _ => null,
        };

        if (match is not null)
        {
            def = def[..^1];
        }
        else
        {
            match = SizeUnit.Measured;
        }

        var multiplier = def.Length > 0 && double.TryParse(def, NumberStyles.Any, CultureInfo.InvariantCulture, out var m)
            ? m
            : 1d;

        return new SizeDefinition(match.Value, multiplier);
    }

    /// <summary>
    /// Converts a size definition to a string.
    /// </summary>
    /// <param name="def">The side definition.</param>
    public static implicit operator string(SizeDefinition def)
        => def.Match switch
        {
            SizeUnit.Measured => $"{def.Multiplier}M",
            SizeUnit.MeasuredUnconstrained => $"{def.Multiplier}U",
            SizeUnit.Parent => $"{def.Multiplier}P",
            SizeUnit.Constraint => $"{def.Multiplier}C",
            SizeUnit.Ratio => $"{def.Multiplier}R",
            _ => throw new InvalidOperationException(),
        };

    /// <summary>
    /// Size matches the measured or desired size of the content.
    /// </summary>
    /// <remarks>
    /// Size is constrained by the parent size.
    /// </remarks>
    public static readonly SizeDefinition Auto = new(SizeUnit.Measured);

    /// <summary>
    /// Size matches the unconstrained measure of the content.
    /// </summary>
    public static readonly SizeDefinition Unconstrained = new(SizeUnit.MeasuredUnconstrained);

    /// <summary>
    /// Size matches the size of the constraints.
    /// </summary>
    public static readonly SizeDefinition Fill = new(SizeUnit.Constraint);

    /// <summary>
    /// Size matches the size of the parent.
    /// </summary>
    public static readonly SizeDefinition Full = new(SizeUnit.Parent);

    private static readonly Dictionary<string, SizeDefinition> _presets
        = new(StringComparer.OrdinalIgnoreCase)
        {
            ["auto"] = Auto,
            ["unconstrained"] = Unconstrained,
            ["fill"] = Fill,
            ["full"] = Full,
        };

    /// <inheritdoc/>
    public override string ToString() => this;
}
