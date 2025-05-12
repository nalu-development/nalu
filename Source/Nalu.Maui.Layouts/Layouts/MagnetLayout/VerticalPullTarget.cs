namespace Nalu.MagnetLayout;

/// <summary>
/// Represents a vertical traction target in a magnet layout.
/// </summary>
/// <param name="Id">The magnet element identifier.</param>
/// <param name="Pole">The traction pole.</param>
/// <param name="Traction">The traction level.</param>
public record VerticalPullTarget(string Id, VerticalPoles Pole, Traction Traction = Traction.Default)
{
    /// <summary>
    /// Implicitly converts a string representation (e.g., "elementId.PoleName") into a VerticalPullTarget.
    /// </summary>
    public static implicit operator VerticalPullTarget(string inputString)
    {
        if (string.IsNullOrWhiteSpace(inputString))
        {
            throw new ArgumentException("Input string cannot be null or whitespace for VerticalPullTarget.", nameof(inputString));
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
            throw new ArgumentException($"Invalid format for VerticalPullTarget string '{inputString}'. Expected format 'elementId.PoleName'.", nameof(inputString));
        }

        var elementId = parts[0];
        var poleName = parts[1];

        if (string.IsNullOrWhiteSpace(elementId))
        {
            throw new ArgumentException($"Element ID cannot be empty in VerticalPullTarget string '{inputString}'.", nameof(inputString));
        }

        if (!Enum.TryParse<VerticalPoles>(poleName, true, out var horizontalPole))
        {
            throw new ArgumentException($"Invalid VerticalPole name '{poleName}' in string '{inputString}'.", nameof(inputString));
        }

        return new VerticalPullTarget(elementId, horizontalPole, traction);
    }

    /// <inheritdoc />
    public override string ToString() => $"{Id}.{Pole}";
}
