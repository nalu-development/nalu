using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// An interface for elements that have vertical poles.
/// </summary>
public interface IVerticalPoles
{
    /// <summary>
    /// Top pole of the element.
    /// </summary>
    Variable Top { get; }

    /// <summary>
    /// Bottom pole of the element.
    /// </summary>
    Variable Bottom { get; }
}
