namespace Nalu.Cassowary;

/// <summary>
/// This is part of the syntactic sugar used for specifying constraints. This enum should be used as part of a
/// constraint expression. See the module documentation for more information.
/// </summary>
/// <param name="Operator">The <see cref="RelationalOperator" />.</param>
/// <param name="Strength">The <see cref="Strength" />.</param>
public readonly record struct WeightedRelation(RelationalOperator Operator, double Strength)
{
    /// <summary>
    /// Create new <see cref="WeightedRelation" /> with <see cref="RelationalOperator.Equal" />.
    /// </summary>
    /// <param name="strength">The <see cref="Strength" /> value.</param>
    /// <returns>
    /// New <see cref="WeightedRelation" /> instance with
    /// <see cref="Operator" /> as <see cref="RelationalOperator.Equal" /> and
    /// <see cref="Strength" /> as <paramref name="strength" />.
    /// </returns>
    public static WeightedRelation Eq(double strength) => new(RelationalOperator.Equal, strength);

    /// <summary>
    /// Create new <see cref="WeightedRelation" /> with <see cref="RelationalOperator.LessThanOrEqual" />.
    /// </summary>
    /// <param name="strength">The <see cref="Strength" /> value.</param>
    /// <returns>
    /// New <see cref="WeightedRelation" /> instance with
    /// <see cref="Operator" /> as <see cref="RelationalOperator.LessThanOrEqual" /> and
    /// <see cref="Strength" /> as <paramref name="strength" />.
    /// </returns>
    public static WeightedRelation LessOrEq(double strength) => new(RelationalOperator.LessThanOrEqual, strength);

    /// <summary>
    /// Create new <see cref="WeightedRelation" /> with <see cref="RelationalOperator.GreaterThanOrEqual" />.
    /// </summary>
    /// <param name="strength">The strength</param>
    /// <returns>
    /// New <see cref="WeightedRelation" /> instance with
    /// <see cref="Operator" /> as <see cref="RelationalOperator.LessThanOrEqual" /> and
    /// <see cref="Strength" /> as <paramref name="strength" />.
    /// </returns>
    public static WeightedRelation GreaterOrEq(double strength) => new(RelationalOperator.GreaterThanOrEqual, strength);

    #region operator |

    /// <summary>
    /// Create new <see cref="PartialConstraint" /> based on <see cref="Expression" /> and <see cref="WeightedRelation" />.
    /// </summary>
    /// <param name="strength">The strength.</param>
    /// <param name="relation">The <see cref="WeightedRelation" />.</param>
    /// <returns>New <see cref="PartialConstraint" /> instance.</returns>
    public static PartialConstraint operator |(float strength, WeightedRelation relation)
        => (double) strength | relation;

    /// <summary>
    /// Create new <see cref="PartialConstraint" /> based on <see cref="Expression" /> and <see cref="WeightedRelation" />.
    /// </summary>
    /// <param name="strength">The strength.</param>
    /// <param name="relation">The <see cref="WeightedRelation" />.</param>
    /// <returns>New <see cref="PartialConstraint" /> instance.</returns>
    public static PartialConstraint operator |(double strength, WeightedRelation relation)
        => new(Expression.From(strength), relation);

    #endregion
}
