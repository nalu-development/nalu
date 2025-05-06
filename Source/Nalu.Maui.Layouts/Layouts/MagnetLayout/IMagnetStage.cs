using System.Diagnostics.CodeAnalysis;
using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// Represent the stage of the magnet layout on which elements can be positioned.
/// </summary>
public interface IMagnetStage : IVerticalPoles, IHorizontalPoles, IMagnetElementBase
{
    /// <summary>
    /// The reserved unique identifier for the stage.
    /// </summary>
    const string StageId = "Stage";

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

    /// <summary>
    /// Gets the element associated with the given identifier.
    /// </summary>
    IMagnetElementBase GetElement(string identifier);

    /// <summary>
    /// Tries to get the element associated with the given identifier.
    /// </summary>
    bool TryGetElement(string identifier, [NotNullWhen(true)] out IMagnetElementBase? element);

    /// <summary>
    /// Adds an editable variable to the stage.
    /// </summary>
    /// <param name="variable">The editable variable.</param>
    /// <param name="strength">The strength, defaults to <see cref="Strength.Strong"/></param>
    void AddEditVariable(Variable variable, double strength);

    /// <summary>
    /// Removes an editable variable from the stage.
    /// </summary>
    /// <param name="variable"></param>
    void RemoveEditVariable(Variable variable);

    /// <summary>
    /// Suggests a value for the given editable variable based on the measured width and the size width.
    /// </summary>
    void SuggestValue(Variable variable, double value);

    /// <summary>
    /// Applies the constraints and solves the layout.
    /// </summary>
    void PrepareForMeasure(double width, double height);

    /// <summary>
    /// Arranges the elements on the stage based on the solved layout.
    /// </summary>
    void PrepareForArrange(double width, double height);
}
