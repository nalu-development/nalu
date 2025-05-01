namespace Nalu.MagnetLayout;

/// <summary>
/// Defines the behavior of the size when constraints cannot be satisfied.
/// </summary>
public enum SizeBehavior
{
    /// <summary>
    /// The desired size must be satisfied.
    /// </summary>
    Required,

    /// <summary>
    /// The desired size is preferred, and can eventually shrink.
    /// </summary>
    Shrink,
}
