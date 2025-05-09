namespace Nalu.Cassowary;

public partial class Solver
{
    /// <summary>
    /// Get the symbol for the given variable.
    ///
    /// If a symbol does not exist for the variable, one will be created.
    /// </summary>
    private Symbol GetVarSymbol(Variable variable)
    {
        if (_varMap.TryGetValue(variable, out var symbol))
        {
            return symbol;
        }
        else
        {
            var newSymbol = MakeSymbol(SymbolType.External);
            _varMap[variable] = newSymbol;
            return newSymbol;
        }
    }

    /// <summary>
    /// Create a new Row object for the given constraint.
    ///
    /// The terms in the constraint will be converted to cells in the row.
    /// Any term in the constraint with a coefficient of zero is ignored.
    /// This method uses the `_getVarSymbol` method to get the symbol for
    /// the variables added to the row. If the symbol for a given cell
    /// variable is basic, the cell variable will be substituted with the
    /// basic row.
    ///
    /// The necessary slack and error variables will be added to the row.
    /// If the constant for the row is negative, the sign for the row
    /// will be inverted so the constant becomes positive.
    ///
    /// Returns the created Row and the tag for tracking the constraint.
    /// </summary>
    private (Row row, Tag tag) CreateRow(Constraint constraint)
    {
        var expr = constraint.Expression;
        var row = new Row(expr.Constant);

        // Substitute the current basic variables into the row.
        var terms = expr.Terms;
        foreach (var term in terms)
        {
            if (!NearZero(term.Coefficient))
            {
                var symbol = GetVarSymbol(term.Variable);
                if (_rowMap.TryGetValue(symbol, out var basicRow))
                {
                    row.InsertRow(basicRow, term.Coefficient);
                }
                else
                {
                    row.InsertSymbol(symbol, term.Coefficient);
                }
            }
        }

        // Add the necessary slack, error, and dummy variables.
        var objective = _objective;
        var strength = constraint.Strength;
        var tag = new Tag(Symbol.InvalidSymbol, Symbol.InvalidSymbol);

        switch (constraint.Operator)
        {
            case RelationalOperator.LessThanOrEqual:
            case RelationalOperator.GreaterThanOrEqual:
            {
                var coeff = constraint.Operator == RelationalOperator.LessThanOrEqual ? 1.0 : -1.0;
                var slack = MakeSymbol(SymbolType.Slack);
                tag = new Tag(slack, Symbol.InvalidSymbol); // Update tag with marker
                row.InsertSymbol(slack, coeff);
                if (strength < Strength.Required)
                {
                    var error = MakeSymbol(SymbolType.Error);
                    tag = new Tag(tag.Marker, error); // Update tag with other
                    row.InsertSymbol(error, -coeff);
                    objective.InsertSymbol(error, strength);
                }
                break;
            }
            case RelationalOperator.Equal:
            {
                if (strength < Strength.Required)
                {
                    var errplus = MakeSymbol(SymbolType.Error);
                    var errminus = MakeSymbol(SymbolType.Error);
                    tag = new Tag(errplus, errminus); // Update tag with marker and other
                    row.InsertSymbol(errplus, -1.0); // v = eplus - eminus
                    row.InsertSymbol(errminus, 1.0); // v - eplus + eminus = 0
                    objective.InsertSymbol(errplus, strength);
                    objective.InsertSymbol(errminus, strength);
                }
                else
                {
                    var dummy = MakeSymbol(SymbolType.Dummy);
                    tag = new Tag(dummy, Symbol.InvalidSymbol); // Update tag with marker
                    row.InsertSymbol(dummy);
                }
                break;
            }
        }

        // Ensure the row has a positive constant.
        if (row.Constant() < 0.0)
        {
            row.ReverseSign();
        }

        return (row, tag);
    }

    /// <summary>
    /// Choose the subject for solving for the row.
    ///
    /// This method will choose the best subject for using as the solve
    /// target for the row. An invalid symbol will be returned if there
    /// is no valid target.
    ///
    /// The symbols are chosen according to the following precedence:
    ///
    /// 1) The first symbol representing an external variable.
    /// 2) A negative slack or error tag variable.
    ///
    /// If a subject cannot be found, an invalid symbol will be returned.
    /// </summary>
    private Symbol ChooseSubject(Row row, Tag tag)
    {
        // The symbols are chosen according to the following precedence:
        // 1) The first symbol representing an external variable.
        // 2) A negative slack or error tag variable.
        // If a subject cannot be found, an invalid symbol will be returned.

        foreach (var pair in row.Cells)
        {
            if (pair.Key.Type == SymbolType.External)
            {
                return pair.Key;
            }
        }

        if ((tag.Marker.Type == SymbolType.Slack || tag.Marker.Type == SymbolType.Error) && row.CoefficientFor(tag.Marker) < 0.0)
        {
            return tag.Marker;
        }

        if ((tag.Other.Type == SymbolType.Slack || tag.Other.Type == SymbolType.Error) && row.CoefficientFor(tag.Other) < 0.0)
        {
            return tag.Other;
        }

        return Symbol.InvalidSymbol;
    }

    /// <summary>
    /// Add the row to the tableau using an artificial variable.
    ///
    /// This will return false if the constraint cannot be satisfied.
    /// </summary>
    private bool AddWithArtificialVariable(Row row)
    {
        // Create and add the artificial variable to the tableau.
        var art = MakeSymbol(SymbolType.Slack);
        _rowMap[art] = row.Copy();
        _artificial = row.Copy();

        // Optimize the artificial objective. This is successful
        // only if the artificial objective is optimized to zero.
        Optimize(_artificial);
        var success = NearZero(_artificial.Constant());
        _artificial = null;

        // If the artificial variable is basic, pivot the row so that
        // it becomes non-basic. If the row is constant, exit early.
        if (_rowMap.Remove(art, out var basicRow))
        {
            if (basicRow.IsConstant())
            {
                return success;
            }
            var entering = AnyPivotableSymbol(basicRow);
            if (entering.Type == SymbolType.Invalid)
            {
                return false; // unsatisfiable (will this ever happen?)
            }
            basicRow.SolveForEx(art, entering);
            Substitute(entering, basicRow);
            _rowMap[entering] = basicRow;
        }

        // Remove the artificial variable from the tableau.
        // Need to iterate over a copy of the keys to avoid modifying the collection during iteration
        var rowKeys = _rowMap.Keys.ToList();
        foreach (var rowSymbol in rowKeys)
        {
            _rowMap[rowSymbol].RemoveSymbol(art);
        }
        _objective.RemoveSymbol(art);
        return success;
    }

    /// <summary>
    /// Get the first Slack or Error symbol in the row.
    ///
    /// If no such symbol is present, an invalid symbol will be returned.
    /// </summary>
    private Symbol AnyPivotableSymbol(Row row)
    {
        foreach (var pair in row.Cells)
        {
            var type = pair.Key.Type;
            if (type is SymbolType.Slack or SymbolType.Error)
            {
                return pair.Key;
            }
        }
        return Symbol.InvalidSymbol;
    }

    /// <summary>
    /// Returns a new Symbol of the given type.
    /// </summary>
    private Symbol MakeSymbol(SymbolType type) => new(type, ++_symbolIdTick);
}
