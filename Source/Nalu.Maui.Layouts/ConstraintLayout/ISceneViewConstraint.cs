namespace Nalu;

/// <summary>
/// Defines a scene element relative to a concrete view.
/// </summary>
public interface ISceneViewConstraint : ISceneElement
{
    /// <summary>
    /// Updates the measurements of the view based on applied constraints.
    /// </summary>
    void FinalizeConstraints();
}
