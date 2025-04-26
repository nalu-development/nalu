namespace Nalu.MagnetLayout;

/// <summary>
/// Defines the size unit for a <see cref="SizeValue" />.
/// </summary>
public enum SizeUnit
{
    /// <summary>
    /// Size matches the content's desired size.
    /// </summary>
    Measured,

    /// <summary>
    /// Size matches the size of the <see cref="IMagnetStage"/>.
    /// </summary>
    Stage,

    /// <summary>
    /// Size fills the constraint.
    /// </summary>
    Constraint,

    /// <summary>
    /// Sizes proportionally to the other axis.
    /// </summary>
    Ratio
}
