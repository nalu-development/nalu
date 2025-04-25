namespace Nalu.Cassowary;

internal record Row(RowData Cells, double Constant)
{
    public Row(double constant)
        : this([], constant) { }

    public Row(int capacity, double constant)
        : this(new RowData(capacity), constant) { }

    public double Constant { get; private set; } = Constant;

    public double Add(double value)
    {
        Constant += value;

        return Constant;
    }

    public void Add(Symbol symbol, double coefficient) => Cells.Add(symbol, coefficient);

    public bool Add(Row other, double coefficient)
    {
        var diff = other.Constant * coefficient;
        Constant += diff;

        foreach (var (symbol, value) in other.Cells)
        {
            Cells.Add(symbol, value * coefficient);
        }

        return diff != 0;
    }

    public void Remove(Symbol symbol) => Cells.Remove(symbol);

    public void ReverseSign()
    {
        Constant = -Constant;
        Cells.Multiply(-1);
    }

    public void SolveForSymbol(Symbol symbol)
    {
        Cells.Remove(symbol, out var symbolValue);
        var coefficient = -1 / symbolValue;

        Constant *= coefficient;
        Cells.Multiply(coefficient);
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
