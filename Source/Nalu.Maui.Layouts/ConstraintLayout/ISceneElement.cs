namespace Nalu;

/// <summary>
/// Represents a scene element.
/// </summary>
public interface ISceneElement : ISceneElementBase
{
    /// <summary>
    /// Sets the scene for the element.
    /// </summary>
    /// <remarks>
    /// This method is called when the element is added or removed to/from the constraint layout scene.
    /// </remarks>
    /// <param name="scene">The constraint layout scene.</param>
    void SetScene(IConstraintLayoutScene? scene);

    /// <summary>
    /// Adds or removes constraints to/from the solver.
    /// </summary>
    void ApplyConstraints();
}
