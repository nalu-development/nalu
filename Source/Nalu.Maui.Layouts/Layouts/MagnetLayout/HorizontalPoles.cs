namespace Nalu.MagnetLayout;

/// <summary>
/// The horizontal pole of a magnet element.
/// </summary>
public enum HorizontalPoles : byte
{
    /// <summary>
    /// The start pole of the element.
    /// </summary>
    /// <remarks>
    /// This is the left pole in a left-to-right layout and the right pole in a right-to-left layout.
    /// </remarks>
    Left = 1,

    /// <summary>
    /// The end pole of the element.
    /// </summary>
    /// <remarks>
    /// This is the right pole in a left-to-right layout and the left pole in a right-to-left layout.
    /// </remarks>
    Right = 2
}
