namespace Nalu.Cassowary;

public partial class Solver
{
    /// <summary>
    /// Optimize the system for the given objective function.
    /// This method performs iterations of Phase 2 of the simplex method
    /// until the objective function reaches a minimum.
    /// </summary>
    private void Optimize(Row objective)
    {
        var iterations = 0;

        while (iterations < MaxIterations)
        {
            var entering = objective.GetEnteringSymbol();

            if (entering.Type == SymbolType.Invalid)
            {
                return;
            }

            var leaving = GetLeavingSymbol(entering);

            if (leaving.Type == SymbolType.Invalid)
            {
                throw new InvalidOperationException("the objective is unbounded");
            }

            // pivot the entering symbol into the basis
            if (!_rowMap.Remove(leaving, out var row))
            {
                // This should not happen if _getLeavingSymbol returned a valid symbol
                throw new InvalidOperationException("Failed to remove leaving row.");
            }

            row.SolveForEx(leaving, entering);
            Substitute(entering, row);
            _rowMap[entering] = row;
            row.Variable = _symbolMap.TryGetValue(entering, out var enteringVariable) ? enteringVariable : null;

            iterations++;
        }

        throw new InvalidOperationException("solver iterations exceeded");
    }

    /// <summary>
    /// Optimize the system using the dual of the simplex method.
    /// The current state of the system should be such that the objective
    /// function is optimal, but not feasible. This method will perform
    /// an iteration of the dual simplex method to make the solution both
    /// optimal and feasible.
    /// </summary>
    private void DualOptimize()
    {
        // The current state of the system should be such that the objective
        // function is optimal, but not feasible. This method will perform
        // an iteration of the dual simplex method to make the solution both
        // optimal and feasible.

        var infeasible = _infeasibleRows;

        while (infeasible.Count != 0)
        {
            var leaving = infeasible.Last(); // Use Last() to match pop() behavior
            infeasible.RemoveAt(infeasible.Count - 1); // Remove the last element

            if (_rowMap.TryGetValue(leaving, out var row) && row.Constant() < 0.0)
            {
                var entering = row.GetDualEnteringSymbol(_objective);

                if (entering.Type == SymbolType.Invalid)
                {
                    throw new InvalidOperationException("dual optimize failed");
                }

                // pivot the entering symbol into the basis
                _rowMap.Remove(leaving);
                row.SolveForEx(leaving, entering);
                Substitute(entering, row);
                _rowMap[entering] = row;
                row.Variable = _symbolMap.TryGetValue(entering, out var enteringVariable) ? enteringVariable : null;
            }
        }
    }

    /// <summary>
    /// Substitute the parametric symbol with the given row.
    /// This method will substitute all instances of the parametric symbol
    /// in the tableau and the objective function with the given row.
    /// </summary>
    private void Substitute(Symbol symbol, Row row)
    {
        // This method will substitute all instances of the parametric symbol
        // in the tableau and the objective function with the given row.
        foreach (ref var entry in _rowMap)
        {
            var basicRow = entry.Value;
            var rowSymbol = entry.Key;

            basicRow.Substitute(symbol, row);

            if (basicRow.Constant() < 0.0 && rowSymbol.Type != SymbolType.External)
            {
                _infeasibleRows.Add(rowSymbol);
            }
        }

        _objective.Substitute(symbol, row);
        _artificial?.Substitute(symbol, row);
    }

    /// <summary>
    /// Compute the symbol for pivot exit row.
    /// This method will return the symbol for the exit row in the row
    /// map. If no appropriate exit symbol is found, an invalid symbol
    /// will be returned. This indicates that the objective function is
    /// unbounded.
    /// </summary>
    private Symbol GetLeavingSymbol(Symbol entering)
    {
        // This method will return the symbol for the exit row in the row
        // map. If no appropriate exit symbol is found, an invalid symbol
        // will be returned. This indicates that the objective function is
        // unbounded.

        var ratio = double.MaxValue;
        var found = Symbol.InvalidSymbol;

        foreach (ref var pair in _rowMap)
        {
            var symbol = pair.Key;

            if (symbol.Type != SymbolType.External)
            {
                var row = pair.Value;
                var temp = row.CoefficientFor(entering);

                if (temp < 0.0)
                {
                    var tempRatio = -row.Constant() / temp;

                    if (tempRatio < ratio)
                    {
                        ratio = tempRatio;
                        found = symbol;
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Compute the leaving symbol for a marker variable.
    /// This method will return a symbol corresponding to a basic row
    /// which holds the given marker variable. The row will be chosen
    /// according to the following precedence:
    /// 1) The row with a restricted basic varible and a negative coefficient
    /// for the marker with the smallest ratio of -constant / coefficient.
    /// 2) The row with a restricted basic variable and the smallest ratio
    /// of constant / coefficient.
    /// 3) The last unrestricted row which contains the marker.
    /// If the marker does not exist in any row, an invalid symbol will be
    /// returned. This indicates an internal solver error since the marker
    /// *should* exist somewhere in the tableau.
    /// </summary>
    private Symbol GetMarkerLeavingSymbol(Symbol marker)
    {
        // This method will return a symbol corresponding to a basic row
        // which holds the given marker variable. The row will be chosen
        // according to the following precedence:
        //
        // 1) The row with a restricted basic varible and a negative coefficient
        //    for the marker with the smallest ratio of -constant / coefficient.
        //
        // 2) The row with a restricted basic variable and the smallest ratio
        //    of constant / coefficient.
        //
        // 3) The last unrestricted row which contains the marker.
        //
        // If the marker does not exist in any row, an invalid symbol will be
        // returned. This indicates an internal solver error since the marker
        // *should* exist somewhere in the tableau.

        var dmax = double.MaxValue;
        var r1 = dmax;
        var r2 = dmax;
        var invalid = Symbol.InvalidSymbol;
        var first = invalid;
        var second = invalid;
        var third = invalid;

        foreach (ref var pair in _rowMap)
        {
            var row = pair.Value;
            var c = row.CoefficientFor(marker);

            if (NearZero(c))
            {
                continue;
            }

            var symbol = pair.Key;

            if (symbol.Type == SymbolType.External)
            {
                third = symbol;
            }
            else if (c < 0.0)
            {
                var r = -row.Constant() / c;

                if (r < r1)
                {
                    r1 = r;
                    first = symbol;
                }
            }
            else
            {
                var r = row.Constant() / c;

                if (r < r2)
                {
                    r2 = r;
                    second = symbol;
                }
            }
        }

        if (first != invalid)
        {
            return first;
        }

        if (second != invalid)
        {
            return second;
        }

        return third;
    }

    /// <summary>
    /// Remove the effects of a constraint on the objective function.
    /// </summary>
    private void RemoveConstraintEffects(Constraint cn, Tag tag)
    {
        if (tag.Marker.Type == SymbolType.Error)
        {
            RemoveMarkerEffects(tag.Marker, cn.Strength);
        }

        if (tag.Other.Type == SymbolType.Error)
        {
            RemoveMarkerEffects(tag.Other, cn.Strength);
        }
    }

    /// <summary>
    /// Remove the effects of an error marker on the objective function.
    /// </summary>
    private void RemoveMarkerEffects(Symbol marker, double strength)
    {
        if (_rowMap.TryGetValue(marker, out var row))
        {
            _objective.InsertRow(row, -strength);
        }
        else
        {
            _objective.InsertSymbol(marker, -strength);
        }
    }
}
