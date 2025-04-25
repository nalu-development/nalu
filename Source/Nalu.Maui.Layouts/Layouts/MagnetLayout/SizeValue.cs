namespace Nalu.MagnetLayout;

/// <summary>
/// Defines the size mode for a <see cref="SizeValue"/>.
/// </summary>
public enum SizeMode
{
    /// <summary>
    /// The dimension is specified as a fixed value in layout units.
    /// </summary>
    Fixed,

    /// <summary>
    /// The dimension is specified as a percentage (0.0 to 1.0) of the container size.
    /// </summary>
    Percentage
}

/// <summary>
/// Represents a fixed or percentage size value.
/// </summary>
/// <param name="Value"></param>
/// <param name="Mode"></param>
public readonly record struct SizeValue(double Value, SizeMode Mode = SizeMode.Fixed)
{
    /// <summary>
    /// Gets a zero-size.
    /// </summary>
    public static readonly SizeValue Zero = new(0);

    /// <summary>
    /// Implicitly converts a double to a <see cref="SizeValue"/> with the fixed mode.
    /// </summary>
    public static SizeValue Fixed(double value) => new SizeValue(value, SizeMode.Fixed);

    /// <summary>
    /// Implicitly converts a double to a <see cref="SizeValue"/> with the percentage mode.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static SizeValue Percentage(double value) => new SizeValue(value, SizeMode.Percentage);

    /// <summary>
    /// Implicitly converts a string representation (e.g., "100" or "50%") into a SizeValue.
    /// </summary>
    public static implicit operator SizeValue(string inputString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputString);

        var percentageIndex = inputString.Length - 1;
        if (inputString[percentageIndex] == '%')
        {
            return new SizeValue(double.Parse(inputString[..percentageIndex]) / 100.0, SizeMode.Percentage);
        }
        
        return new SizeValue(double.Parse(inputString));
    }
}
