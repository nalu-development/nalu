using Nalu.Cassowary.Extensions;

namespace Nalu.Cassowary;

/// <summary>
/// A constraint solver using the Cassowary algorithm. For proper usage please see the top level crate documentation.
/// </summary>
public class Solver
{
    private readonly Dictionary<Constraint, Tag> _cns;
    private readonly Dictionary<Variable, (double, Symbol, double)> _varData;
    private readonly Dictionary<Symbol, Variable> _varForSymbol;
    private readonly List<(Variable, double)> _publicChanges;
    private readonly HashSet<Variable> _changed;
    private readonly Dictionary<Symbol, Row> _rows;
    private readonly Dictionary<Variable, EditInfo> _edits;
    private readonly List<Symbol> _infeasibleRows;
    private bool _shouldClearChanges;
    private Row _objective;
    private Row? _artificial;
    private int _idTick;

    /// <summary>
    /// Initializes a new instance of the <see cref="Solver" /> class.
    /// </summary>
    public Solver()
    {
        _cns = [];
        _varData = [];
        _varForSymbol = [];
        _publicChanges = [];
        _changed = [];
        _shouldClearChanges = false;
        _rows = [];
        _edits = [];
        _infeasibleRows = [];
        _objective = new Row(0);
        _artificial = null;
        _idTick = 1;
    }

    /// <summary>
    /// Add range of <see cref="Constraint" />.
    /// </summary>
    /// <param name="constraints"></param>
    public void AddConstraints(IEnumerable<Constraint> constraints)
    {
        foreach (var constraint in constraints)
        {
            AddConstraint(constraint);
        }
    }

    /// <summary>
    /// Add range of <see cref="Constraint" />.
    /// </summary>
    /// <param name="constraints"></param>
    public void AddConstraints(params Constraint[] constraints)
    {
        foreach (var constraint in constraints)
        {
            AddConstraint(constraint);
        }
    }

    /// <summary>
    /// Add a <see cref="Constraint" /> to the solver.
    /// </summary>
    /// <param name="constraint"></param>
    /// <exception cref="ArgumentException">Duplicate constraint.</exception>
    public void AddConstraint(Constraint constraint)
    {
        if (_cns.ContainsKey(constraint))
        {
            throw new ArgumentException("Duplicate constraint");
        }

        // Creating a row causes symbols to reserved for the variables
        // in the constraint. If this method exits with an exception,
        // then its possible those variables will linger in the var map.
        // Since its likely that those variables will be used in other
        // constraints and since exceptional conditions are uncommon,
        // I'm not too worried about aggressive cleanup of the var map.
        var (row, tag) = CreateRow(constraint);
        var subject = ChooseSubject(row, tag, out var allDummies);

        // If chooseSubject could find a valid entering symbol, one
        // last option is available if the entire row is composed of
        // dummy variables. If the constant of the row is zero, then
        // this represents redundant constraints and the new dummy
        // marker can enter the basis. If the constant is non-zero,
        // then it represents an unsatisfiable constraint.
        if (subject.Type == SymbolType.Invalid && allDummies)
        {
            if (!row.Constant.IsNearZero())
            {
                throw new ArgumentException("Unsatisfiable constraint");
            }

            subject = tag.Marker;
        }

        // If an entering symbol still isn't found, then the row must
        // be added using an artificial variable. If that fails, then
        // the row represents an unsatisfiable constraint.
        if (subject.Type == SymbolType.Invalid)
        {
            if (!AddWithArtificialVariable(row))
            {
                throw new ArgumentException("Unsatisfiable constraint");
            }
        }
        else
        {
            row.SolveForSymbol(subject);
            Substitute(subject, row);

            if (subject.Type == SymbolType.External && row.Constant != 0)
            {
                var variable = _varForSymbol[subject];
                VarChanged(variable);
            }

            _rows.Add(subject, row);
        }

        _cns.Add(constraint, tag);

        // Optimizing after each constraint is added performs less
        // aggregate work due to a smaller average system size. It
        // also ensures the solver remains in a consistent state.
        Optimise(_objective);
    }

    /// <summary>
    /// Remove a constraint from the solver.
    /// </summary>
    /// <param name="constraint">The <see cref="Constraint" />.</param>
    public void RemoveConstraint(Constraint constraint)
    {
        if (!_cns.Remove(constraint, out var tag))
        {
            return;
        }

        // Remove the error effects from the objective function
        // *before* pivoting, or substitutions into the objective
        // will lead to incorrect solver results.
        RemoveConstraintEffects(constraint, tag);

        // If the marker is basic, simply drop the row. Otherwise,
        // pivot the marker into the basis and then drop the row.
        if (!_rows.Remove(tag.Marker))
        {
            var tmp = GetMarkerLeavingRow(tag.Marker)
                      ?? throw new ArgumentException("Failed to find leaving row.");

            var (leaving, row) = tmp;
            row.SolveForSymbols(leaving, tag.Marker);
            Substitute(tag.Marker, row);
        }

        // Optimizing after each constraint is removed ensures that the
        // solver remains consistent. It makes the solver api easier to
        // use at a small tradeoff for speed.
        Optimise(_objective);

        // Check for and decrease the reference count for variables referenced by the constraint
        // If the reference count is zero remove the variable from the variable map
        foreach (var term in constraint.Expression.Terms)
        {
            if (term.Coefficient.IsNearZero())
            {
                var shouldRemove = false;

                if (_varData.TryGetValue(term.Variable, out var data))
                {
                    if (--data.Item3 == 0)
                    {
                        shouldRemove = true;
                    }
                    else
                    {
                        _varData[term.Variable] = data;
                    }
                }

                if (shouldRemove)
                {
                    _varForSymbol.Remove(_varData[term.Variable].Item2);
                    _varData.Remove(term.Variable);
                }
            }
        }
    }

    /// <summary>
    /// Test whether a constraint has been added to the solver.
    /// </summary>
    /// <param name="constraint">The <see cref="Constraint" />.</param>
    /// <returns>True if it has the <paramref name="constraint" />; otherwise it returns false.</returns>
    public bool HasConstraint(Constraint constraint)
        => _cns.ContainsKey(constraint);

    /// <summary>
    /// Add an edit variable to the solver.
    /// </summary>
    /// <param name="variable">The <see cref="Variable" />.</param>
    /// <param name="strength">The strength.</param>
    /// <remarks>
    /// This method should be called before the `suggest_value` method is
    /// used to supply a suggested value for the given edit variable.
    /// </remarks>
    public void AddEditVariable(Variable variable, double strength)
    {
        if (_edits.ContainsKey(variable))
        {
            throw new ArgumentException("duplicate edit variable");
        }

        strength = Strength.Clip(strength);

        if (strength == Strength.Required)
        {
            throw new ArgumentException("bad required strength");
        }

        var cn = new Constraint(
            Expression.From(new Term(variable, 1)),
            RelationalOperator.Equal,
            strength
        );

        AddConstraint(cn);
        _edits.Add(variable, new EditInfo(_cns[cn], cn, 0));
    }

    /// <summary>
    /// Remove an edit variable from the solver.
    /// </summary>
    /// <param name="variable"></param>
    /// <exception cref="ArgumentException">Unknown edit variable</exception>
    public void RemoveEditVariable(Variable variable)
    {
        if (!_edits.Remove(variable, out var edit))
        {
            throw new ArgumentException("Unknown edit variable");
        }

        RemoveConstraint(edit.Constraint);
    }

    /// <summary>
    /// Test whether an edit variable has been added to the solver.
    /// </summary>
    /// <param name="variable">The <see cref="Variable" />.</param>
    /// <returns>True is has the <paramref name="variable" />; otherwise it return false.</returns>
    public bool HasEditVariable(Variable variable)
        => _edits.ContainsKey(variable);

    /// <summary>
    /// Suggest a value for the given edit variable.
    /// </summary>
    /// <param name="variable"></param>
    /// <param name="value"></param>
    /// <remarks>
    /// This method should be used after an edit variable has been added to
    /// the solver in order to suggest the value for that variable.
    /// </remarks>
    public void SuggestValue(Variable variable, double value)
    {
        var delta = value - _edits[variable].Constant;
        var info = _edits[variable] = _edits[variable] with { Constant = value };
        var infoTagMarker = info.Tag.Marker;
        var infoTagOther = info.Tag.Other;

        // tag.marker and tag.other are never external symbols

        // The nice version of the following code runs into non-lexical borrow issues.
        // Ideally the `if row...` code would be in the body of the if block. Pretend that it is.
        if (_rows.TryGetValue(infoTagMarker, out var row) && row.Add(-delta) < 0)
        {
            _infeasibleRows.Add(infoTagMarker);
        }
        else if (_rows.TryGetValue(infoTagOther, out row) && row.Add(delta) < 0)
        {
            _infeasibleRows.Add(infoTagOther);
        }
        else
        {
            foreach (var (symbol, otherRow) in _rows)
            {
                var coeff = otherRow.CoefficientFor(infoTagMarker);
                var diff = delta * coeff;

                if (diff != 0 && symbol.Type == SymbolType.External)
                {
                    var v = _varForSymbol[symbol];
                    VarChanged(v);
                }

                if (coeff != 0 && otherRow.Add(diff) < 0 && symbol.Type == SymbolType.External)
                {
                    _infeasibleRows.Add(symbol);
                }
            }
        }

        DualOptimise();
    }

    /// <summary>
    /// Fetches all changes to the values of variables since the last call to this function.
    /// </summary>
    /// <remarks>
    /// The list of changes returned is not in a specific order. Each change comprises the variable changed and
    /// the new value of that variable.
    /// </remarks>
    public List<(Variable Variable, double Value)> FetchChanges()
    {
        if (_shouldClearChanges)
        {
            _changed.Clear();
            _shouldClearChanges = false;
        }
        else
        {
            _shouldClearChanges = true;
        }

        _publicChanges.Clear();

        foreach (var variable in _changed)
        {
            if (_varData.TryGetValue(variable, out var data))
            {
                var newValue = 0d;

                if (_rows.TryGetValue(data.Item2, out var row))
                {
                    newValue = row.Constant;
                }

                var oldValue = data.Item1;

                if (oldValue != newValue)
                {
                    _publicChanges.Add((variable, newValue));
                    variable.CurrentValue = newValue;
                    data.Item1 = newValue;
                    _varData[variable] = data;
                }
            }
        }

        return _publicChanges;
    }

    /// <summary>
    /// Reset the solver to the empty starting condition.
    /// </summary>
    /// <remarks>
    /// This method resets the internal solver state to the empty starting
    /// condition, as if no constraints or edit variables have been added.
    /// This can be faster than deleting the solver and creating a new one
    /// when the entire system must change, since it can avoid unnecessary
    /// heap (de)allocations.
    /// </remarks>
    public void Reset()
    {
        _rows.Clear();
        _cns.Clear();
        _varData.Clear();
        _varForSymbol.Clear();
        _changed.Clear();
        _edits.Clear();
        _infeasibleRows.Clear();
        _objective = new Row(0);
        _shouldClearChanges = false;
        _artificial = null;
        _idTick = 1;
    }

    /// <summary>
    /// Remove the effects of a constraint on the objective function.
    /// </summary>
    /// <param name="constraint"></param>
    /// <param name="tag"></param>
    private void RemoveConstraintEffects(Constraint constraint, Tag tag)
    {
        if (tag.Marker.Type == SymbolType.Error)
        {
            RemoveMarkerEffects(tag.Marker, constraint.Strength);
        }
        else if (tag.Other.Type == SymbolType.Error)
        {
            RemoveMarkerEffects(tag.Other, constraint.Strength);
        }
    }

    /// <summary>
    /// Remove the effects of an error marker on the objective function.
    /// </summary>
    /// <param name="marker"></param>
    /// <param name="strength"></param>
    private void RemoveMarkerEffects(Symbol marker, double strength)
    {
        if (_rows.TryGetValue(marker, out var row))
        {
            _objective.Add(row, -strength);
        }
        else
        {
            _objective.Add(marker, -strength);
        }
    }

    /// <summary>
    /// Add the row to the tableau using an artificial variable.
    /// </summary>
    /// <returns>This will return false if the constraint cannot be satisfied.</returns>
    private bool AddWithArtificialVariable(Row row)
    {
        // Create and add the artificial variable to the tableau
        var art = new Symbol(_idTick, SymbolType.Slack);
        _idTick++;
        _rows.Add(art, row with { Cells = new RowData(row.Cells) });
        _artificial = row with { Cells = new RowData(row.Cells) };

        // Optimize the artificial objective. This is successful
        // only if the artificial objective is optimized to zero.
        Optimise(_artificial);

        var success = _artificial.Constant.IsNearZero();
        _artificial = null;

        // If the artificial variable is basic, pivot the row so that
        // it becomes basic. If the row is constant, exit early.
        if (_rows.Remove(art, out var tmp))
        {
            if (tmp.Cells.Count == 0)
            {
                return success;
            }

            var entering = AnyPivotableSymbol(tmp); // never External

            if (entering.Type == SymbolType.Invalid)
            {
                return false;
            }

            tmp.SolveForSymbols(art, entering);
            Substitute(entering, tmp);
            _rows.Add(entering, tmp);
        }

        // Remove the artificial row from the tableau
        foreach (var (_, otherRow) in _rows)
        {
            otherRow.Remove(art);
        }

        _objective.Remove(art);

        return success;
    }

    /// <summary>
    /// Optimize the system for the given objective function.
    /// </summary>
    /// <param name="objective"></param>
    /// <remarks>
    /// This method performs iterations of Phase 2 of the simplex method
    /// until the objective function reaches a minimum.
    /// </remarks>
    private void Optimise(Row objective)
    {
        while (true)
        {
            var entering = GetEnteringSymbol(objective);

            if (entering.Type == SymbolType.Invalid)
            {
                return;
            }

            var leaving = GetLeavingRow(entering)
                          ?? throw new InvalidOperationException("Objective function is unbounded");

            var (symbol, row) = leaving;

            // pivot the entering symbol into the basis
            row.SolveForSymbols(symbol, entering);
            Substitute(entering, row);

            if (entering.Type == SymbolType.External && row.Constant != 0)
            {
                var variable = _varForSymbol[entering];
                VarChanged(variable);
            }

            _rows.Add(entering, row);
        }
    }

    /// <summary>
    /// Optimize the system using the dual of the simplex method.
    /// </summary>
    /// <remarks>
    /// The current state of the system should be such that the objective
    /// function is optimal, but not feasible. This method will perform
    /// an iteration of the dual simplex method to make the solution both
    /// optimal and feasible.
    /// </remarks>
    private void DualOptimise()
    {
        while (_infeasibleRows.Count != 0)
        {
            var leaving = _infeasibleRows.Pop();

            if (_rows.TryGetValue(leaving, out var row) && row.Constant < 0)
            {
                _rows.Remove(leaving);

                var entering = GetDualEnteringSymbol(row);

                if (entering.Type == SymbolType.Invalid)
                {
                    throw new InvalidOperationException("Objective function is unbounded");
                }

                row.SolveForSymbols(leaving, entering);
                Substitute(entering, row);

                if (entering.Type == SymbolType.External && row.Constant != 0)
                {
                    var variable = _varForSymbol[entering];
                    VarChanged(variable);
                }

                _rows.Add(entering, row);
            }
        }
    }

    /// <summary>
    /// Compute the row which holds the exit symbol for a pivot.
    /// </summary>
    /// <param name="entering"></param>
    /// <remarks>
    /// This method will return an iterator to the row in the row map
    /// which holds the exit symbol. If no appropriate exit symbol is
    /// found, the end() iterator will be returned. This indicates that
    /// the objective function is unbounded.
    /// Never returns a row for an External symbol
    /// </remarks>
    private (Symbol Symbol, Row Row)? GetLeavingRow(Symbol entering)
    {
        var ratio = double.PositiveInfinity;
        Symbol? found = null;

        foreach (var (symbol, row) in _rows)
        {
            if (symbol.Type != SymbolType.External)
            {
                var temp = row.CoefficientFor(entering);

                if (temp < 0)
                {
                    var tempRatio = -row.Constant / temp;

                    if (tempRatio < ratio)
                    {
                        ratio = tempRatio;
                        found = symbol;
                    }
                }
            }
        }

        if (found != null)
        {
            var row = _rows[found.Value];
            _rows.Remove(found.Value);

            return (found.Value, row);
        }

        return null;
    }

    /// <summary>
    /// Substitute the parametric symbol with the given row.
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="row"></param>
    /// <remarks>
    /// This method will substitute all instances of the parametric symbol
    /// in the tableau and the objective function with the given row.
    /// </remarks>
    private void Substitute(Symbol symbol, Row row)
    {
        foreach (var (otherSymbol, otherRow) in _rows)
        {
            var constantChanged = otherRow.Substitute(symbol, row);

            if (otherSymbol.Type == SymbolType.External && constantChanged)
            {
                var variable = _varForSymbol[otherSymbol];
                VarChanged(variable);
            }

            if (otherSymbol.Type != SymbolType.External && otherRow.Constant < 0)
            {
                _infeasibleRows.Add(otherSymbol);
            }
        }

        _objective.Substitute(symbol, row);
        _artificial?.Substitute(symbol, row);
    }

    private void VarChanged(Variable variable)
    {
        if (_shouldClearChanges)
        {
            _changed.Clear();
            _shouldClearChanges = false;
        }

        _changed.Add(variable);
    }

    /// <summary>
    /// Create a new Row object for the given constraint.
    /// </summary>
    /// <param name="constraint">The <see cref="Constraint" />.</param>
    /// <remarks>
    ///     <para>
    ///     The terms in the constraint will be converted to cells in the row.
    ///     Any term in the constraint with a coefficient of zero is ignored.
    ///     This method uses the `getVarSymbol` method to get the symbol for
    ///     the variables added to the row. If the symbol for a given cell
    ///     variable is basic, the cell variable will be substituted with the
    ///     basic row.
    ///     </para>
    ///     <para>
    ///     The necessary slack and error variables will be added to the row.
    ///     If the constant for the row is negative, the sign for the row
    ///     will be inverted so the constant becomes positive.
    ///     </para>
    ///     <para>
    ///     The tag will be updated with the marker and error symbols to use
    ///     for tracking the movement of the constraint in the tableau.
    ///     </para>
    /// </remarks>
    private (Row Row, Tag Tag) CreateRow(Constraint constraint)
    {
        var expression = constraint.Expression;

        var row = new Row(expression.Terms.Length, expression.Constant);

        // Substitute the current basic variables into the row.
        foreach (var term in expression.Terms)
        {
            // TODO: near-zero terms could be excluded at the origin inside expression
            if (!term.Coefficient.IsNearZero())
            {
                var symbol = GetVarSymbol(term.Variable);

                if (_rows.TryGetValue(symbol, out var otherRow))
                {
                    row.Add(otherRow, term.Coefficient);
                }
                else
                {
                    row.Add(symbol, term.Coefficient);
                }
            }
        }

        Tag tag;

        if (constraint.Operator is RelationalOperator.GreaterThanOrEqual or RelationalOperator.LessThanOrEqual)
        {
            var coeff = constraint.Operator == RelationalOperator.LessThanOrEqual ? 1f : -1f;

            var slack = new Symbol(_idTick, SymbolType.Slack);
            row.Add(slack, coeff);

            _idTick++;

            if (constraint.Strength < Strength.Required)
            {
                var error = new Symbol(_idTick, SymbolType.Error);
                _idTick++;
                row.Add(error, -coeff);
                _objective.Add(error, constraint.Strength);
                tag = new Tag(slack, error);
            }
            else
            {
                tag = new Tag(slack, Symbol.Invalid);
            }
        }
        else
        {
            if (constraint.Strength < Strength.Required)
            {
                var errPlus = new Symbol(_idTick, SymbolType.Error);
                _idTick++;

                var errMinus = new Symbol(_idTick, SymbolType.Error);
                _idTick++;

                row.Add(errPlus, -1);
                row.Add(errMinus, 1);
                _objective.Add(errPlus, constraint.Strength);
                _objective.Add(errMinus, constraint.Strength);
                tag = new Tag(errPlus, errMinus);
            }
            else
            {
                var dummy = new Symbol(_idTick, SymbolType.Dummy);
                _idTick++;
                row.Add(dummy, 1);
                tag = new Tag(dummy, Symbol.Invalid);
            }
        }

        // Ensure the row has a positive constant.
        if (row.Constant < 0)
        {
            row.ReverseSign();
        }

        return (row, tag);
    }

    /// <summary>
    /// Get the symbol for the given variable.
    /// </summary>
    /// <param name="variable"></param>
    /// <remarks>
    /// If a symbol does not exist for the variable, one will be created.
    /// </remarks>
    private Symbol GetVarSymbol(Variable variable)
    {
        if (!_varData.TryGetValue(variable, out var data))
        {
            var symbol = new Symbol(_idTick++, SymbolType.External);
            _varForSymbol.Add(symbol, variable);

            data = (float.NaN, symbol, 1);
            _varData.Add(variable, data);
            return symbol;
        }

        _varData[variable] = (data.Item1, data.Item2, data.Item3 + 1);

        return data.Item2;
    }

    /// <summary>
    /// Compute the leaving row for a marker variable.
    /// </summary>
    /// <param name="marker"></param>
    /// <remarks>
    /// This method will return an iterator to the row in the row map
    /// which holds the given marker variable. The row will be chosen
    /// according to the following precedence:
    /// 1) The row with a restricted basic varible and a negative coefficient
    /// for the marker with the smallest ratio of -constant / coefficient.
    /// 2) The row with a restricted basic variable and the smallest ratio
    /// of constant / coefficient.
    /// 3) The last unrestricted row which contains the marker.
    /// If the marker does not exist in any row, the row map end() iterator
    /// will be returned. This indicates an internal solver error since
    /// the marker *should* exist somewhere in the tableau.
    /// </remarks>
    private (Symbol, Row)? GetMarkerLeavingRow(Symbol marker)
    {
        var r1 = double.PositiveInfinity;
        var r2 = r1;
        Symbol? first = null;
        Symbol? second = null;
        Symbol? third = null;

        foreach (var (symbol, row) in _rows)
        {
            var c = row.CoefficientFor(marker);

            if (c == 0)
            {
                continue;
            }

            if (symbol.Type == SymbolType.External)
            {
                third = symbol;
            }
            else if (c < 0)
            {
                var r = -row.Constant / c;

                if (r < r1)
                {
                    r1 = r;
                    first = symbol;
                }
            }
            else
            {
                var r = row.Constant / c;

                if (r < r2)
                {
                    r2 = r;
                    second = symbol;
                }
            }
        }

        var tmp = first;

        if (tmp == null && second != null)
        {
            tmp = second;
        }

        if (tmp == null && third != null)
        {
            tmp = third;
        }

        if (tmp == null)
        {
            return null;
        }

        var s = tmp.Value;

        if (s.Type == SymbolType.External && _rows[s].Constant != 0)
        {
            var variable = _varForSymbol[s];
            VarChanged(variable);
        }

        _rows.TryGetValue(s, out var otherRow);
        _rows.Remove(s);

        return (s, otherRow!);
    }

    /// <summary>
    /// Compute the entering symbol for the dual optimize operation.
    /// </summary>
    /// <param name="row"></param>
    /// <remarks>
    /// This method will return the symbol in the row which has a positive
    /// coefficient and yields the minimum ratio for its respective symbol
    /// in the objective function. The provided row *must* be infeasible.
    /// If no symbol is found which meats the criteria, an invalid symbol
    /// is returned.
    /// Could return an External symbol
    /// </remarks>
    private Symbol GetDualEnteringSymbol(Row row)
    {
        var entering = Symbol.Invalid;
        var ratio = double.PositiveInfinity;
        var objective = _objective;

        foreach (var (symbol, value) in row.Cells)
        {
            if (value > 0 && symbol.Type != SymbolType.Dummy)
            {
                var coeff = objective.CoefficientFor(symbol);
                var r = coeff / value;

                if (r < ratio)
                {
                    ratio = r;
                    entering = symbol;
                }
            }
        }

        return entering;
    }

    /// <summary>
    /// Choose the subject for solving for the row.
    /// </summary>
    /// <param name="row"></param>
    /// <param name="tag"></param>
    /// <param name="allDummies"></param>
    /// <remarks>
    ///     <para>
    ///     This method will choose the best subject for using as the solve
    ///     target for the row. An invalid symbol will be returned if there
    ///     is no valid target.
    ///     </para>
    ///     <para>
    ///     The symbols are chosen according to the following precedence:
    ///     </para>
    ///     <para>
    ///     1) The first symbol representing an external variable.
    ///     </para>
    ///     <para>
    ///     2) A negative slack or error tag variable.
    ///     </para>
    ///     <para>
    ///     If a subject cannot be found, an invalid symbol will be returned.
    ///     </para>
    /// </remarks>
    private static Symbol ChooseSubject(Row row, Tag tag, out bool allDummies)
    {
        allDummies = true;

        foreach (var symbol in row.Cells.Keys)
        {
            if (allDummies && symbol.Type != SymbolType.Dummy)
            {
                allDummies = false;
            }

            if (symbol.Type == SymbolType.External)
            {
                return symbol;
            }
        }

        if (tag.Marker.Type is SymbolType.Slack or SymbolType.Error && row.CoefficientFor(tag.Marker) < 0)
        {
            return tag.Marker;
        }

        if (tag.Other.Type is SymbolType.Slack or SymbolType.Error && row.CoefficientFor(tag.Other) < 0)
        {
            return tag.Other;
        }

        return Symbol.Invalid;
    }

    /// <summary>
    /// Compute the entering variable for a pivot operation.
    /// </summary>
    /// <param name="objective"></param>
    /// <remarks>
    /// This method will return first symbol in the objective function which
    /// is non-dummy and has a coefficient less than zero. If no symbol meets
    /// the criteria, it means the objective function is at a minimum, and an
    /// invalid symbol is returned.
    /// Could return an External symbol
    /// </remarks>
    private static Symbol GetEnteringSymbol(Row objective)
    {
        foreach (var (symbol, value) in objective.Cells)
        {
            if (symbol.Type != SymbolType.Dummy && value < 0)
            {
                return symbol;
            }
        }

        return Symbol.Invalid;
    }

    /// <summary>
    /// Get the first Slack or Error symbol in the row.
    /// </summary>
    /// <param name="row"></param>
    /// <remarks>
    /// If no such symbol is present, and Invalid symbol will be returned.
    /// Never returns an External symbol
    /// </remarks>
    private static Symbol AnyPivotableSymbol(Row row)
    {
        foreach (var symbol in row.Cells.Keys)
        {
            if (symbol.Type is SymbolType.Slack or SymbolType.Error)
            {
                return symbol;
            }
        }

        return Symbol.Invalid;
    }
}
