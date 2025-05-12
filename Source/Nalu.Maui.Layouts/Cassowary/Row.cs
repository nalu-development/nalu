using Nalu.Internals;

namespace Nalu.Cassowary;

/// <summary>
/// An internal row class used by the solver.
/// </summary>
internal class Row
{
    private double _constant;

    /// <summary>
    /// Construct a new Row.
    /// </summary>
    internal Row(double constant = 0.0)
    {
        _constant = constant;
        Cells = new RefDictionary<Symbol, double>(SymbolDictionaryComparer.Instance);
    }

    /// <summary>
    /// Construct a new Row with the given cells and constant.
    /// </summary>
    /// <remarks>
    /// Cells will be cloned.
    /// </remarks>
    private Row(RefDictionary<Symbol, double> cells, double constant = 0.0)
    {
        _constant = constant;
        Cells = new RefDictionary<Symbol, double>(cells);
    }

    /// <summary>
    /// Returns the mapping of symbols to coefficients.
    /// </summary>
    public RefDictionary<Symbol, double> Cells { get; }

    /// <summary>
    /// Returns the constant for the row.
    /// </summary>
    public double Constant() => _constant;

    /// <summary>
    /// Returns true if the row is a constant value.
    /// </summary>
    public bool IsConstant() => Cells.Count == 0;

    /// <summary>
    /// Returns true if the Row has all dummy symbols.
    /// </summary>
    public bool AllDummies()
    {
        foreach (ref var pair in Cells)
        {
            if (pair.Key.Type is not SymbolType.Dummy)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Create a copy of the row.
    /// </summary>
    public Row Copy() => new(Cells, _constant);

    /// <summary>
    /// Add a constant value to the row constant.
    /// Returns the new value of the constant.
    /// </summary>
    public double Add(double value) => _constant += value;

    /// <summary>
    /// Insert the symbol into the row with the given coefficient.
    /// If the symbol already exists in the row, the coefficient
    /// will be added to the existing coefficient. If the resulting
    /// coefficient is zero, the symbol will be removed from the row.
    /// </summary>
    public void InsertSymbol(Symbol symbol, double coefficient = 1.0)
    {
        ref var value = ref Cells.GetOrAddDefaultRef(symbol, out _);
        value += coefficient;

        if (NearZero(value))
        {
            Cells.Remove(symbol);
        }
    }

    /// <summary>
    /// Insert a row into this row with a given coefficient.
    /// The constant and the cells of the other row will be
    /// multiplied by the coefficient and added to this row. Any
    /// cell with a resulting coefficient of zero will be removed
    /// from the row.
    /// </summary>
    public void InsertRow(Row other, double coefficient = 1.0)
    {
        _constant += other._constant * coefficient;

        foreach (ref var pair in other.Cells)
        {
            InsertSymbol(pair.Key, pair.Value * coefficient);
        }
    }

    /// <summary>
    /// Remove a symbol from the row.
    /// </summary>
    public void RemoveSymbol(Symbol symbol) => Cells.Remove(symbol);

    /// <summary>
    /// Reverse the sign of the constant and cells in the row.
    /// </summary>
    public void ReverseSign()
    {
        _constant = -_constant;

        foreach (ref var entry in Cells)
        {
            entry.Value *= -1;
        }
    }

    /// <summary>
    /// Solve the row for the given symbol.
    /// This method assumes the row is of the form
    /// a * x + b * y + c = 0 and (assuming solve for x) will modify
    /// the row to represent the right hand side of
    /// x = -b/a * y - c / a. The target symbol will be removed from
    /// the row, and the constant and other cells will be multiplied
    /// by the negative inverse of the target coefficient.
    /// The given symbol *must* exist in the row.
    /// </summary>
    public void SolveFor(Symbol symbol)
    {
        if (!Cells.Remove(symbol, out var coefficient))
        {
            // This should not happen if the algorithm is correct
            throw new InvalidOperationException($"Symbol {symbol} not found in row for solving.");
        }

        var inverseCoeff = -1.0 / coefficient;
        _constant *= inverseCoeff;

        foreach (ref var entry in Cells)
        {
            entry.Value *= inverseCoeff;
        }
    }

    /// <summary>
    /// Solve the row for the given symbols.
    /// This method assumes the row is of the form
    /// x = b * y + c and will solve the row such that
    /// y = x / b - c / b. The rhs symbol will be removed from the
    /// row, the lhs added, and the result divided by the negative
    /// inverse of the rhs coefficient.
    /// The lhs symbol *must not* exist in the row, and the rhs
    /// symbol must* exist in the row.
    /// </summary>
    public void SolveForEx(Symbol lhs, Symbol rhs)
    {
        InsertSymbol(lhs, -1.0);
        SolveFor(rhs);
    }

    /// <summary>
    /// Returns the coefficient for the given symbol.
    /// </summary>
    public double CoefficientFor(Symbol symbol) => Cells.TryGetValue(symbol, out var coefficient) ? coefficient : 0.0;

    /// <summary>
    /// Substitute a symbol with the data from another row.
    /// Given a row of the form a * x + b and a substitution of the
    /// form x = 3 * y + c the row will be updated to reflect the
    /// expression 3 * a * y + a * c + b.
    /// If the symbol does not exist in the row, this is a no-op.
    /// </summary>
    public void Substitute(Symbol symbol, Row row)
    {
        if (Cells.Remove(symbol, out var coefficient))
        {
            InsertRow(row, coefficient);
        }
    }

    /// <summary>
    /// Test whether a value is approximately zero.
    /// </summary>
    private static bool NearZero(double value)
    {
        const double eps = 1.0e-8;
        const double neps = 1.0e-8;

        return value < 0.0 ? value > neps : value < eps;
    }
}
