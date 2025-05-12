namespace Nalu.Cassowary;

/// <summary>
/// The internal interface of an edit info object used by the solver.
/// </summary>
internal class EditInfo
{
    /// <summary>
    /// The tag associated with the edit variable constraint.
    /// </summary>
    public Tag Tag { get; }

    /// <summary>
    /// The constraint associated with the edit variable.
    /// </summary>
    public Constraint Constraint { get; }

    /// <summary>
    /// The constant value of the edit variable.
    /// </summary>
    public double Constant { get; set; }

    /// <summary>
    /// Construct a new EditInfo.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <param name="constraint">The constraint.</param>
    /// <param name="constant">The constant value.</param>
    internal EditInfo(Tag tag, Constraint constraint, double constant)
    {
        Tag = tag;
        Constraint = constraint;
        Constant = constant;
    }
}