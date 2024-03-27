namespace Nalu.Cassowary;

internal readonly record struct Symbol(int Size, SymbolType Type)
{
    public static Symbol Invalid => new(0, SymbolType.Invalid);
}
