namespace Nalu;

using Cassowary;

/// <summary>
/// Represents a constraint layout scene.
/// </summary>
public interface IConstraintLayoutScene : ISceneElementBase, IEnumerable<ISceneElement>
{
    /// <summary>
    /// Gets the solver for the scene.
    /// </summary>
    Solver Solver { get; }

    /// <summary>
    /// Gets a view by its scene id.
    /// </summary>
    /// <param name="id">The scene identifier for the view.</param>
    IView? GetView(string id);

    /// <summary>
    /// Gets an element by its scene id.
    /// </summary>
    /// <param name="id">The scene identifier for the element.</param>
    ISceneElementBase? GetElement(string id);

    /// <summary>
    /// Notifies the scene that the layout has changed.
    /// </summary>
    void InvalidateScene();

    /// <summary>
    /// Applies the layout size to the scene.
    /// </summary>
    /// <param name="left">Left coordinate, usually zero.</param>
    /// <param name="top">Top coordinate, usually zero.</param>
    /// <param name="right">Right coordinate, usually width.</param>
    /// <param name="bottom">Bottom coordinate, usually height.</param>
    void Apply(double left, double top, double right, double bottom);

    /// <summary>
    /// Sets the layout for the scene.
    /// </summary>
    /// <param name="layout">The constraint layout.</param>
    void SetLayout(IConstraintLayout? layout);
}
