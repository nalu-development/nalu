using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// Represent the stage of the magnet layout on which elements can be positioned.
/// </summary>
public interface IMagnetStage : IVerticalPoles, IHorizontalPoles
{
    /// <summary>
    /// Invalidates the scene due to changed constraints.
    /// </summary>
    void Invalidate();

    /// <summary>
    /// Adds a constraint to the stage.
    /// </summary>
    void AddConstraint(Constraint constraint);

    /// <summary>
    /// Removes a constraint from the stage.
    /// </summary>
    void RemoveConstraint(Constraint constraint);
}
