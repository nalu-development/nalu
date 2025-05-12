namespace Nalu.Cassowary;

/// <summary>
/// The internal interface of a tag value used by the solver.
/// </summary>
internal readonly struct Tag
{
    /// <summary>
    /// The marker symbol.
    /// </summary>
    public Symbol Marker { get; }

    /// <summary>
    /// The other symbol.
    /// </summary>
    public Symbol Other { get; }

    /// <summary>
    /// Construct a new Tag.
    /// </summary>
    /// <param name="marker">The marker symbol.</param>
    /// <param name="other">The other symbol.</param>
    internal Tag(Symbol marker, Symbol other)
    {
        Marker = marker;
        Other = other;
    }
}
