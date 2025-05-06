namespace Nalu.Cassowary;

internal readonly struct Symbol : IEquatable<Symbol>
{
    public static readonly Symbol Invalid = new(0, SymbolType.Invalid);

    public readonly int Id;
    public readonly SymbolType Type;

    public Symbol(int id, SymbolType type)
    {
        Id = id;
        Type = type;
    }

    public bool Equals(Symbol other) => Id == other.Id;

    public override bool Equals(object? obj) =>
        obj is Symbol other && Equals(other);

    public override int GetHashCode() => Id;

    public static bool operator ==(Symbol left, Symbol right) => left.Id == right.Id;
    public static bool operator !=(Symbol left, Symbol right) => left.Id != right.Id;
}
