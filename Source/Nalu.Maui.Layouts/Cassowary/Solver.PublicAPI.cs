namespace Nalu.Cassowary;

public partial class Solver
{
    /// <summary>
    /// Add a constraint to the solver.
    /// </summary>
    /// <param name="constraint">Constraint to add to the solver</param>
    public void AddConstraint(Constraint constraint)
    {
        if (_cnMap.ContainsKey(constraint))
        {
            throw new InvalidOperationException("duplicate constraint");
        }

        // Creating a row causes symbols to be reserved for the variables
        // in the constraint. If this method exits with an exception,
        // then its possible those variables will linger in the var map.
        // Since its likely that those variables will be used in other
        // constraints and since exceptional conditions are uncommon,
        // i'm not too worried about aggressive cleanup of the var map.
        var (row, tag) = CreateRow(constraint);
        var subject = ChooseSubject(row, tag);

        // If chooseSubject couldnt find a valid entering symbol, one
        // last option is available if the entire row is composed of
        // dummy variables. If the constant of the row is zero, then
        // this represents redundant constraints and the new dummy
        // marker can enter the basis. If the constant is non-zero,
        // then it represents an unsatisfiable constraint.
        if (subject.Type == SymbolType.Invalid && row.AllDummies())
        {
            if (!NearZero(row.Constant()))
            {
                throw new InvalidOperationException("unsatisfiable constraint");
            }
            else
            {
                subject = tag.Marker;
            }
        }

        // If an entering symbol still isn't found, then the row must
        // be added using an artificial variable. If that fails, then
        // the row represents an unsatisfiable constraint.
        if (subject.Type == SymbolType.Invalid)
        {
            if (!AddWithArtificialVariable(row))
            {
                throw new InvalidOperationException("unsatisfiable constraint");
            }
        }
        else
        {
            row.SolveFor(subject);
            Substitute(subject, row);
            _rowMap[subject] = row;
        }

        _cnMap[constraint] = tag;

        // Optimizing after each constraint is added performs less
        // aggregate work due to a smaller average system size. It
        // also ensures the solver remains in a consistent state.
        Optimize(_objective);
    }

    /// <summary>
    /// Remove a constraint from the solver.
    /// </summary>
    /// <param name="constraint">Constraint to remove from the solver</param>
    public void RemoveConstraint(Constraint constraint)
    {
        if (!_cnMap.Remove(constraint, out var cnPair))
        {
            return;
        }

        // Remove the error effects from the objective function
        // *before* pivoting, or substitutions into the objective
        // will lead to incorrect solver results.
        RemoveConstraintEffects(constraint, cnPair);

        // If the marker is basic, simply drop the row. Otherwise,
        // pivot the marker into the basis and then drop the row.
        var marker = cnPair.Marker;
        if (!_rowMap.Remove(marker, out var rowPair))
        {
            var leaving = GetMarkerLeavingSymbol(marker);
            if (leaving.Type == SymbolType.Invalid)
            {
                throw new InvalidOperationException("failed to find leaving row");
            }
            _rowMap.Remove(leaving, out rowPair);
            rowPair!.SolveForEx(leaving, marker);
            Substitute(marker, rowPair);
        }

        // Optimizing after each constraint is removed ensures that the
        // solver remains consistent. It makes the solver api easier to
        // use at a small tradeoff for speed.
        Optimize(_objective);
    }

    /// <summary>
    /// Test whether the solver contains the constraint.
    /// </summary>
    /// <param name="constraint">Constraint to test for</param>
    /// <returns>True or false</returns>
    public bool HasConstraint(Constraint constraint) => _cnMap.ContainsKey(constraint);

    /// <summary>
    /// Get an array of the current constraints.
    /// </summary>
    /// <returns>Array of constraints</returns>
    public IEnumerable<Constraint> GetConstraints() => _cnMap.Keys;

    /// <summary>
    /// Add an edit variable to the solver.
    /// </summary>
    /// <param name="variable">Edit variable to add to the solver</param>
    /// <param name="strength">Strength, should be less than Strength.Required</param>
    public void AddEditVariable(Variable variable, double strength)
    {
        if (_editMap.ContainsKey(variable))
        {
            throw new InvalidOperationException("duplicate edit variable");
        }

        strength = Strength.Clip(strength);

        if (strength == Strength.Required)
        {
            throw new InvalidOperationException("bad required strength");
        }

        // Create a constraint for the edit variable: variable == 0 with the given strength
        // Note: The TS version creates a Constraint with rhs as undefined, which its constructor handles.
        // We need to create the equivalent Constraint object using your API.
        var expr = Expression.From(variable); // Expression representing just the variable
        var cn = new Constraint(expr, RelationalOperator.Equal, strength);

        AddConstraint(cn); // Add the constraint to the solver

        // Find the tag associated with the newly added constraint
        // This assumes AddConstraint successfully added the constraint, and it exists in _cnMap
        if (!_cnMap.TryGetValue(cn, out var tag))
        {
             // This should not happen if AddConstraint was successful
             throw new InvalidOperationException("Failed to find tag for newly added edit constraint.");
        }

        var info = new EditInfo(tag, cn, 0.0);
        _editMap[variable] = info;
    }

    /// <summary>
    /// Remove an edit variable from the solver.
    /// </summary>
    /// <param name="variable">Edit variable to remove from the solver</param>
    public void RemoveEditVariable(Variable variable)
    {
        if (!_editMap.Remove(variable, out var editPair))
        {
            throw new InvalidOperationException("unknown edit variable");
        }
        RemoveConstraint(editPair.Constraint);
    }

    /// <summary>
    /// Test whether the solver contains the edit variable.
    /// </summary>
    /// <param name="variable">Edit variable to test for</param>
    /// <returns>True or false</returns>
    public bool HasEditVariable(Variable variable) => _editMap.ContainsKey(variable);

    /// <summary>
    /// Suggest the value of an edit variable.
    /// </summary>
    /// <param name="variable">Edit variable to suggest a value for</param>
    /// <param name="value">Suggested value</param>
    public void SuggestValue(Variable variable, double value)
    {
        if (!_editMap.TryGetValue(variable, out var info))
        {
            throw new InvalidOperationException("unknown edit variable");
        }

        var delta = value - info.Constant;

        if (delta == 0)
        {
            return;
        }

        info.Constant = value;

        // Check first if the positive error variable is basic.
        var rows = _rowMap;
        var marker = info.Tag.Marker;

        if (rows.TryGetValue(marker, out var row))
        {
            if (row.Add(-delta) < 0.0)
            {
                _infeasibleRows.Add(marker);
            }
            DualOptimize();
            return;
        }

        // Check next if the negative error variable is basic.
        var other = info.Tag.Other;
        if (rows.TryGetValue(other, out row))
        {
            if (row.Add(delta) < 0.0)
            {
                _infeasibleRows.Add(other);
            }
            DualOptimize();
            return;
        }

        // Otherwise update each row where the error variables exist.
        // Need to iterate over a copy of the keys to avoid modifying the collection during iteration
        foreach (ref var entry in rows)
        {
            var basicRow = entry.Value;
            var symbol = entry.Key;
            var coeff = basicRow.CoefficientFor(marker);
            if (!NearZero(coeff))
            {
                if (basicRow.Add(delta * coeff) < 0.0 && symbol.Type != SymbolType.External)
                {
                    _infeasibleRows.Add(symbol);
                }
            }
        }

        DualOptimize();
    }

    /// <summary>
    /// Update the values of the variables.
    /// </summary>
    public void UpdateVariables()
    {
        foreach (ref var variableEntry in _varMap)
        {
            var variable = variableEntry.Key;
            var symbol = variableEntry.Value;

            if (_rowMap.TryGetValue(symbol, out var row))
            {
                variable.CurrentValue = row.Constant();
            }
            else
            {
                variable.CurrentValue = 0.0;
            }
        }
    }

    /// <summary>
    /// Add multiple constraints to the solver.
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
    /// Updates the variables.
    /// </summary>
    public void FetchChanges() => UpdateVariables();
}
