namespace Nalu.Cassowary;

/// <summary>
/// A dictionary comparer for the Symbol class which checks only the Id.
/// </summary>
internal class SymbolDictionaryComparer : IEqualityComparer<Symbol>
{
    public static readonly SymbolDictionaryComparer Instance = new();
    
    public bool Equals(Symbol x, Symbol y) => x.Id == y.Id;

    public int GetHashCode(Symbol obj) => obj.Id;
}
