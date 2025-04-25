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
    Variable Start { get; }

    /// <summary>
    /// End pole of the element.
    /// </summary>
    Variable End { get; }
}
