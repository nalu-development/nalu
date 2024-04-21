namespace Nalu;

using System.Globalization;
using System.Text.RegularExpressions;

/// <summary>
/// Describes how a size should be calculated proportionally.
/// </summary>
public readonly partial struct SizeDefinition
{
    /// <inheritdoc cref="SizeUnit"/>
    public SizeUnit Unit { get; }

    /// <summary>
    /// Gets the multiplier.
    /// </summary>
    public double Multiplier { get; }

    /// <summary>
    /// Gets the size definition used upon creation.
    /// </summary>
    public string Definition { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SizeDefinition"/> struct.
    /// </summary>
    /// <param name="unit">Size unit.</param>
    /// <param name="multiplier">The multiplier.</param>
    public SizeDefinition(SizeUnit unit, double multiplier = 1d)
    {
        Unit = unit;
        Multiplier = multiplier;

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        var multiplierString = multiplier == 1d ? string.Empty : multiplier.ToString(CultureInfo.InvariantCulture);
        Definition = unit switch
        {
            SizeUnit.Measured => $"{multiplierString}m",
            SizeUnit.Parent => $"{multiplierString}p",
            SizeUnit.Constraint => $"{multiplierString}*",
            SizeUnit.Ratio => $"{multiplierString}r",
            _ => throw new InvalidOperationException(),
        };
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SizeDefinition"/> struct.
    /// </summary>
    /// <param name="definition">The size definition in string format.</param>
    /// <exception cref="FormatException">Invalid size definition.</exception>
    public SizeDefinition(string definition)
    {
        var match = _sizeDefinitionRegex.Match(definition);
        if (!match.Success)
        {
            throw new FormatException("Invalid size definition.");
        }

        Definition = definition;

        var groups = match.Groups;
        if (groups[2].Success)
        {
            Multiplier = string.IsNullOrEmpty(groups[1].Value) ? 1d : double.Parse(groups[1].Value, CultureInfo.InvariantCulture);
            Unit = char.ToLowerInvariant(groups[2].Value[0]) switch
            {
                'm' => SizeUnit.Measured,
                'p' => SizeUnit.Parent,
                '*' => SizeUnit.Constraint,
                'r' => SizeUnit.Ratio,
                _ => throw new InvalidOperationException(),
            };
        }
        else
        {
            Multiplier = double.Parse(groups[4].Value, CultureInfo.InvariantCulture) / double.Parse(groups[5].Value, CultureInfo.InvariantCulture);
            Unit = SizeUnit.Ratio;
        }
    }

    /// <summary>
    /// Converts a string to a size definition.
    /// </summary>
    /// <param name="def">The size definition.</param>
    public static implicit operator SizeDefinition(string? def)
        => def is null ? Measured : new SizeDefinition(def);

    /// <summary>
    /// Converts a size definition to a string.
    /// </summary>
    /// <param name="def">The side definition.</param>
    public static implicit operator string(SizeDefinition def)
        => def.Definition;

    /// <summary>
    /// Size matches the measured or desired size of the content.
    /// </summary>
    /// <remarks>
    /// Size is constrained by the parent size.
    /// </remarks>
    public static readonly SizeDefinition Measured = new(SizeUnit.Measured);

    /// <summary>
    /// Size matches the size of the constraints.
    /// </summary>
    public static readonly SizeDefinition Constraint = new(SizeUnit.Constraint);

    /// <summary>
    /// Size matches the size of the parent.
    /// </summary>
    public static readonly SizeDefinition Parent = new(SizeUnit.Parent);

    /// <inheritdoc/>
    public override string ToString() => this;

    private static readonly Regex _sizeDefinitionRegex = MyRegex();

    [GeneratedRegex(@"^(?:(\d+|\d+\.\d*|\.\d+)?(\*|p(?:arent)?|m(?:easured)?|r(?:atio)?)|(\d+):(\d+))$", RegexOptions.IgnoreCase)]
    private static partial Regex MyRegex();
}
