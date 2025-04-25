namespace Nalu.MagnetLayout;

/// <summary>
/// The horizontal pole of a magnet element.
/// </summary>
public enum HorizontalPole
{
    /// <summary>
    /// The start pole of the element.
    /// </summary>
    /// <remarks>
    /// This is the left pole in a left-to-right layout and the right pole in a right-to-left layout.
    /// </remarks>
    Start,

    /// <summary>
    /// The end pole of the element.
    /// </summary>
    /// <remarks>
    /// This is the right pole in a left-to-right layout and the left pole in a right-to-left layout.
    /// </remarks>
    End
}
