namespace Nalu.MagnetLayout;

/// <summary>
/// Represents a horizontal traction target in a magnet layout.
/// </summary>
/// <param name="Id">The magnet element identifier.</param>
/// <param name="Pole">The traction pole.</param>
/// <param name="Traction">The traction level.</param>
public readonly record struct HorizontalPullTarget(string Id, HorizontalPoles Pole, Traction Traction = Traction.Default)
{
    /// <summary>
    /// Implicitly converts a string representation (e.g., "elementId.PoleName") into a HorizontalPullTarget.
    /// </summary>
    public static implicit operator HorizontalPullTarget(string inputString)
    {
        if (string.IsNullOrWhiteSpace(inputString))
        {
            throw new ArgumentException("Input string cannot be null or whitespace for HorizontalPullTarget.", nameof(inputString));
        }

        string[] parts;
        Traction traction;
        if (inputString[^1] == '!') // Traction is not supported yet
        {
            traction = Traction.Strong;
            parts = inputString[..^1].Split('.');
        }
        else
        {
            traction = Traction.Default;
            parts = inputString.Split('.');
        }

        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid format for HorizontalPullTarget string '{inputString}'. Expected format 'elementId.PoleName'.", nameof(inputString));
        }

        var elementId = parts[0];
        var poleName = parts[1];

        if (string.IsNullOrWhiteSpace(elementId))
        {
            throw new ArgumentException($"Element ID cannot be empty in HorizontalPullTarget string '{inputString}'.", nameof(inputString));
        }

        if (!Enum.TryParse<HorizontalPoles>(poleName, true, out var horizontalPole))
        {
            throw new ArgumentException($"Invalid HorizontalPole name '{poleName}' in string '{inputString}'.", nameof(inputString));
        }

        return new HorizontalPullTarget(elementId, horizontalPole, traction);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Id}.{Pole}";
}
