namespace Nalu;

/// <summary>
/// Describes in relation to what the size should be calculated.
/// </summary>
public enum SizeUnit
{
    /// <summary>
    /// Size matches the measured or desired size of the content.
    /// </summary>
    /// <remarks>
    /// Measures are constrained by the parent size.
    /// </remarks>
    Measured,

    /// <summary>
    /// Size matches the measured or desired size of the content.
    /// </summary>
    /// <remarks>
    /// Measures are unconstrained.
    /// </remarks>
    MeasuredUnconstrained,

    /// <summary>
    /// Size matches the size of the parent.
    /// </summary>
    Parent,

    /// <summary>
    /// Size fills the constraint.
    /// </summary>
    Constraint,

    /// <summary>
    /// Sizes proportionally to the other axis.
    /// </summary>
    Ratio,
}
