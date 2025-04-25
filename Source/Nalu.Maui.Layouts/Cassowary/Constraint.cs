namespace Nalu.Cassowary;

/// <summary>
/// A constraint, consisting of an equation governed by an expression and a relational operator,
/// and an associated strength.
/// </summary>
/// <summary>
/// Initializes a new instance of the <see cref="Constraint" /> struct.
/// </summary>
/// <param name="Expression">The <see cref="Cassowary.Expression" />.</param>
/// <param name="Operator">The <see cref="RelationalOperator" />.</param>
/// <param name="Strength">The strength.</param>
public readonly record struct Constraint(Expression Expression, RelationalOperator Operator, double Strength)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Constraint" /> struct.
    /// </summary>
    /// <param name="expression">The <see cref="Cassowary.Expression" />.</param>
    /// <param name="relation">The <see cref="WeightedRelation" />.</param>
    public Constraint(Expression expression, WeightedRelation relation)
        : this(expression, relation.Operator, relation.Strength) { }

    /// <inheritdoc />
    public override string ToString()
    {
        var operatorString = Operator switch
        {
            RelationalOperator.LessThanOrEqual => "<=",
            RelationalOperator.Equal => "=",
            RelationalOperator.GreaterThanOrEqual => ">=",
            _ => throw new InvalidOperationException("Invalid relational operator.")
        };

        return $"{Expression} {operatorString} 0";
    }
}
