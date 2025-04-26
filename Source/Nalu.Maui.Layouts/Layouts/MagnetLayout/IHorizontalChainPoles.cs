using Nalu.Cassowary;

namespace Nalu.MagnetLayout;

/// <summary>
/// An interface for elements that have horizontal chainable poles.
/// </summary>
public interface IHorizontalChainPoles
{
    /// <summary>
    /// Left pole of the chain element.
    /// </summary>
    Variable ChainLeft { get; }

    /// <summary>
    /// Right pole of the chain element.
    /// </summary>
    Variable ChainRight { get; }
}
