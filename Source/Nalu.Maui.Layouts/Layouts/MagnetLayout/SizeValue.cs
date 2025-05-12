namespace Nalu.MagnetLayout;

/// <summary>
/// Represents a fixed or percentage size value.
/// </summary>
public readonly record struct SizeValue(double Value, SizeUnit Unit = SizeUnit.Measured, SizeBehavior Behavior = SizeBehavior.Required)
{
    /// <summary>
    /// Matches the measured size.
    /// </summary>
    public static readonly SizeValue Default = new(1);

    /// <summary>
    /// Matches the measured size, but can shrink if needed.
    /// </summary>
    public static readonly SizeValue Shrink = new(1, Behavior: SizeBehavior.Shrink);

    /// <summary>
    /// Implicitly converts a <see cref="SizeUnit.Measured" /> coefficient <see cref="SizeValue" />.
    /// </summary>
    public static SizeValue Measured(double value, SizeBehavior behavior = SizeBehavior.Required) => new(value, SizeUnit.Measured, behavior);

    /// <summary>
    /// Implicitly converts a <see cref="SizeUnit.Stage" /> percentage to a <see cref="SizeValue" /> coefficient.
    /// </summary>
    public static SizeValue StagePercent(double percent, SizeBehavior behavior = SizeBehavior.Required) => new(percent / 100, SizeUnit.Stage, behavior);

    /// <summary>
    /// Implicitly converts a <see cref="SizeUnit.Constraint" /> coefficient to a <see cref="SizeValue" />.
    /// </summary>
    public static SizeValue Constraint(double value, SizeBehavior behavior = SizeBehavior.Required) => new(value, SizeUnit.Constraint, behavior);

    /// <summary>
    /// Implicitly converts a <see cref="SizeUnit.Ratio" /> coefficient to a <see cref="SizeValue" />.
    /// </summary>
    public static SizeValue Ratio(double value, SizeBehavior behavior = SizeBehavior.Required) => new(value, SizeUnit.Ratio, behavior);

    /// <summary>
    /// Implicitly converts a string representation (e.g., "50%" or "*" or "1-") into a <see cref="SizeValue" />.
    /// </summary>
    public static implicit operator SizeValue(string inputString)
    {
        if (string.IsNullOrEmpty(inputString))
        {
            return Default;
        }

        var inputChars = inputString.AsSpan();
        var behavior = SizeBehavior.Required;

        if (inputChars[^1] == '~')
        {
            inputChars = inputChars[..^1];
            behavior = SizeBehavior.Shrink;
        }

        var unitIndex = inputChars.Length - 1;

        if (unitIndex < 0)
        {
            return behavior == SizeBehavior.Shrink ? Shrink : Default;
        }

        switch (inputChars[unitIndex])
        {
            case '%':
                var percentage = unitIndex == 0 ? 100.0 : double.Parse(inputChars[..unitIndex]);
                percentage /= 100;

                return new SizeValue(percentage, SizeUnit.Stage, behavior);
            case '*':
                var constraintRatio = unitIndex == 0 ? 1.0 : double.Parse(inputChars[..unitIndex]);

                return new SizeValue(constraintRatio, SizeUnit.Constraint, behavior);
            case 'r':
                var axisRatio = unitIndex == 0 ? 1.0 : double.Parse(inputChars[..unitIndex]);

                return new SizeValue(axisRatio, SizeUnit.Ratio, behavior);
            case 'M':
            case 'm':
                var measuredRatio = unitIndex == 0 ? 1.0 : double.Parse(inputChars[..unitIndex]);

                return new SizeValue(measuredRatio, SizeUnit.Measured, behavior);
            default:
                var ratio = double.Parse(inputChars);

                return new SizeValue(ratio, SizeUnit.Measured, behavior);
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (Behavior == SizeBehavior.Shrink)
        {
            return Unit switch
            {
                SizeUnit.Measured => $"{Value}~",
                SizeUnit.Stage => $"{Value * 100}%~",
                SizeUnit.Constraint => $"{Value}*~",
                SizeUnit.Ratio => $"{Value}r~",
                _ => throw new FormatException($"Invalid size value: {Value}")
            };
        }

        return Unit switch
        {
            SizeUnit.Measured => $"{Value}",
            SizeUnit.Stage => $"{Value * 100}%",
            SizeUnit.Constraint => $"{Value}*",
            SizeUnit.Ratio => $"{Value}r",
            _ => throw new FormatException($"Invalid size value: {Value}")
        };
    }
}
