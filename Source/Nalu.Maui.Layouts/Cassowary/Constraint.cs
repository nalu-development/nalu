namespace Nalu.Cassowary;

/// <summary>
/// A constraint, consisting of an equation governed by an expression and a relational operator,
/// and an associated strength.
/// </summary>
public readonly record struct Constraint
{
    private readonly ConstraintData _data;

    /// <summary>
    /// Initializes a new instance of the <see cref="Constraint" /> struct.
    /// </summary>
    /// <param name="expression">The <see cref="Cassowary.Expression" />.</param>
    /// <param name="relation">The <see cref="WeightedRelation" />.</param>
    public Constraint(Expression expression, WeightedRelation relation)
        : this(expression, relation.Operator, relation.Strength) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Constraint" /> struct.
    /// </summary>
    /// <param name="expression">The <see cref="Cassowary.Expression" />.</param>
    /// <param name="relationalOperator">The <see cref="RelationalOperator" />.</param>
    /// <param name="strength">The strength.</param>
    public Constraint(Expression expression, RelationalOperator relationalOperator, double strength)
    {
        _data = new ConstraintData(expression, strength, relationalOperator);
    }

    /// <summary>
    /// Gets the expression of the left hand side of the constraint equation.
    /// </summary>
    public Expression Expression => _data.Expression;

    /// <summary>
    /// Gets the relational operator governing the constraint.
    /// </summary>
    public RelationalOperator Operator => _data.Operator;

    /// <summary>
    /// Gets the strength of the constraint that the solver will use.
    /// </summary>
    public double Strength => _data.Strength;

    /// <inheritdoc />
    public override string ToString()
    {
        var operatorString = _data.Operator switch
        {
            RelationalOperator.LessThanOrEqual => "<=",
            RelationalOperator.Equal => "=",
            RelationalOperator.GreaterThanOrEqual => ">=",
            _ => throw new InvalidOperationException("Invalid relational operator.")
        };

        return $"{Expression} {operatorString} 0";
    }
}
