namespace Nalu.MagnetLayout;

/// <summary>
/// Defines the traction level of a pull target.
/// </summary>
public enum Traction
{
    /// <summary>
    /// Default traction level.
    /// </summary>
    Default,

    /// <summary>
    /// Implies the pull traction is so strong that the target will be pulled to be in contact with the element.
    /// </summary>
    Strong
}
