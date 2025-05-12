namespace Nalu.Cassowary;

/// <summary>
/// An internal class representing a symbol in the solver.
/// </summary>
internal struct Symbol : IEquatable<Symbol>
{
    /// <summary>
    /// Returns the unique id number of the symbol.
    /// </summary>
    public readonly int Id;

    /// <summary>
    /// Returns the type of the symbol.
    /// </summary>
    public readonly SymbolType Type;

    /// <summary>
    /// Construct a new Symbol
    /// </summary>
    /// <param name="type">The type of the symbol.</param>
    /// <param name="id">The unique id number of the symbol.</param>
    internal Symbol(SymbolType type, int id)
    {
        Id = id;
        Type = type;
    }

    /// <summary>
    /// A static invalid symbol
    /// </summary>
    public static readonly Symbol InvalidSymbol = new(SymbolType.Invalid, 0);

    /// <inheritdoc />
    public override string ToString() => $"{Type}:{Id}";

    public bool Equals(Symbol other) => Id == other.Id;

    public override bool Equals(object? obj) => obj is Symbol other && Equals(other);

    public readonly override int GetHashCode() => Id;

    public static bool operator ==(Symbol left, Symbol right) => left.Id == right.Id;
    public static bool operator !=(Symbol left, Symbol right) => left.Id != right.Id;
}
