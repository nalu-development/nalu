using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// An interface for elements that have vertical chainable poles.
/// </summary>
public interface IVerticalChainPoles
{
    /// <summary>
    /// Top pole of the chain element.
    /// </summary>
    Variable ChainTop { get; }

    /// <summary>
    /// Bottom pole of the chain element.
    /// </summary>
    Variable ChainBottom { get; }
}
