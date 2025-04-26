using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// An interface for elements that have horizontal poles.
/// </summary>
public interface IHorizontalPoles
{
    /// <summary>
    /// Start pole of the element.
    /// </summary>
    Variable Left { get; }

    /// <summary>
    /// End pole of the element.
    /// </summary>
    Variable Right { get; }
}
