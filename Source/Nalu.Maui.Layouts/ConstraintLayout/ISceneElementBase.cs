namespace Nalu;

using Cassowary;

/// <summary>
/// Represents a scene element.
/// </summary>
public interface ISceneElementBase
{
    /// <summary>
    /// Gets the identifier of the scene element.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the variable representing the left edge of the scene element.
    /// </summary>
    Variable Left { get; }

    /// <summary>
    /// Gets the variable representing the right edge of the scene element.
    /// </summary>
    Variable Right { get; }

    /// <summary>
    /// Gets the variable representing the top edge of the scene element.
    /// </summary>
    Variable Top { get; }

    /// <summary>
    /// Gets the variable representing the bottom edge of the scene element.
    /// </summary>
    Variable Bottom { get; }
}
