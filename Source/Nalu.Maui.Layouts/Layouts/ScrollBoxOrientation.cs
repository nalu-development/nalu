namespace Nalu;

/// <summary>
/// The scrolling axis of a <see cref="ScrollBox" />.
/// </summary>
/// <remarks>
/// There is deliberately no <c>Both</c> value: bi-directional scrolling is the single most
/// bug-prone area of platform scroll containers and is rarely a legitimate UI. A future version
/// may add it without breaking existing values.
/// </remarks>
public enum ScrollBoxOrientation
{
    /// <summary>
    /// The content scrolls vertically (the default).
    /// </summary>
    Vertical,

    /// <summary>
    /// The content scrolls horizontally.
    /// </summary>
    Horizontal
}
