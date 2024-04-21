namespace Nalu;

using Cassowary;

/// <summary>
/// Defines a scene element relative to a concrete view.
/// </summary>
public interface ISceneViewConstraint : ISceneElement
{
    /// <summary>
    /// Gets the left edge of the view.
    /// </summary>
    Variable ViewLeft { get; }

    /// <summary>
    /// Gets the right edge of the view.
    /// </summary>
    Variable ViewRight { get; }

    /// <summary>
    /// Gets the top edge of the view.
    /// </summary>
    Variable ViewTop { get; }

    /// <summary>
    /// Gets the bottom edge of the view.
    /// </summary>
    Variable ViewBottom { get; }

    /// <summary>
    /// Updates the measurements of the view based on applied constraints.
    /// </summary>
    void FinalizeConstraints();
}
