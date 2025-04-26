using System.Runtime.InteropServices;
using Nalu.Cassowary.Extensions;

namespace Nalu.Cassowary;

internal record Row(Dictionary<Symbol, double> Cells, double Constant)
{
    public Row(double constant)
        : this([], constant) { }

    public Row(int capacity, double constant)
        : this(new Dictionary<Symbol, double>(capacity), constant) { }

    public double Constant { get; private set; } = Constant;

    public double Add(double value)
    {
        Constant += value;

        return Constant;
    }

    public void Add(Symbol symbol, double coefficient)
    {
        if (coefficient == 0)
        {
            return;
        }

        if (!coefficient.IsNearZero())
        {
            QuickAdd(symbol, coefficient);

            return;
        }

        if (Cells.TryGetValue(symbol, out var entry))
        {
            entry += coefficient;
            Cells[symbol] = entry;

            if (entry.IsNearZero())
            {
                Cells.Remove(symbol);
            }
        }
    }

    private void QuickAdd(Symbol symbol, double coefficient)
    {
        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(Cells, symbol, out var exists);

        if (exists)
        {
            entry += coefficient;

            if (entry.IsNearZero())
            {
                Cells.Remove(symbol);
            }
        }
        else
        {
            entry = coefficient;
        }
    }

    public bool Add(Row other, double coefficient)
    {
        if (coefficient == 0)
        {
            return false;
        }

        var diff = other.Constant * coefficient;
        Constant += diff;

        foreach (var (symbol, value) in other.Cells)
        {
            Add(symbol, value * coefficient);
        }

        return diff != 0;
    }

    public void Remove(Symbol symbol) => Cells.Remove(symbol);

    public void ReverseSign()
    {
        Constant = -Constant;

        foreach (var (symbol, value) in Cells)
        {
            Cells[symbol] = -value;
        }
    }

    public void SolveForSymbol(Symbol symbol)
    {
        Cells.Remove(symbol, out var symbolCoefficient);
        var coefficient = -1 / symbolCoefficient;

        Constant *= coefficient;

        foreach (var (key, value) in Cells)
        {
            Cells[key] = value * coefficient;
        }
    }

    public void SolveForSymbols(Symbol lhs, Symbol rhs)
    {
        Add(lhs, -1);
        SolveForSymbol(rhs);
    }

    public double CoefficientFor(Symbol symbol) => Cells.GetValueOrDefault(symbol);

    public bool Substitute(Symbol symbol, Row row)
    {
        if (Cells.Remove(symbol, out var coefficient))
        {
            return Add(row, coefficient);
        }

        return false;
    }
}
