namespace Nalu.Cassowary;

/// <summary>
/// This is an intermediate type used in the syntactic sugar for specifying constraints. You should not use it
/// directly.
/// </summary>
/// <param name="Expression">The <see cref="Cassowary.Expression"/>.</param>
/// <param name="Relation">The <see cref="WeightedRelation"/>.</param>
public readonly record struct PartialConstraint(Expression Expression, WeightedRelation Relation)
{
    /// <summary>
    /// Create a <see cref="Constraint"/> from a <see cref="PartialConstraint"/> and a value.
    /// </summary>
    /// <param name="partial">The <see cref="PartialConstraint"/>.</param>
    /// <param name="value">The strength.</param>
    /// <returns>New <see cref="Constraint"/> instance.</returns>
    public static Constraint operator |(PartialConstraint partial, float value)
        => partial | (double)value;

    /// <summary>
    /// Create a <see cref="Constraint"/> from a <see cref="PartialConstraint"/> and a value.
    /// </summary>
    /// <param name="partial">The <see cref="PartialConstraint"/>.</param>
    /// <param name="value">The strength.</param>
    /// <returns>New <see cref="Constraint"/> instance.</returns>
    public static Constraint operator |(PartialConstraint partial, double value)
        => new(partial.Expression - value, partial.Relation);

    /// <summary>
    /// Create a <see cref="Constraint"/> from a <see cref="PartialConstraint"/> and a value.
    /// </summary>
    /// <param name="partial">The <see cref="PartialConstraint"/>.</param>
    /// <param name="value">The strength.</param>
    /// <returns>New <see cref="Constraint"/> instance.</returns>
    public static Constraint operator |(double value, PartialConstraint partial)
        => partial | value;

    /// <summary>
    /// Create a <see cref="Constraint"/> from a <see cref="PartialConstraint"/> and a value.
    /// </summary>
    /// <param name="partial">The <see cref="PartialConstraint"/>.</param>
    /// <param name="value">The strength.</param>
    /// <returns>New <see cref="Constraint"/> instance.</returns>
    public static Constraint operator |(float value, PartialConstraint partial)
        => partial | value;

    /// <summary>
    /// Create a <see cref="Constraint"/> from a <see cref="PartialConstraint"/> and a <see cref="Variable"/>.
    /// </summary>
    /// <param name="partial">The <see cref="PartialConstraint"/>.</param>
    /// <param name="variable">The <see cref="Variable"/>.</param>
    /// <returns>New <see cref="Constraint"/> instance by sub <see cref="Expression"/> from <paramref name="variable"/>.</returns>
    public static Constraint operator |(PartialConstraint partial, Variable variable)
        => new(partial.Expression - variable, partial.Relation);

    /// <summary>
    /// Create a <see cref="Constraint"/> from a <see cref="PartialConstraint"/> and a <see cref="Term"/>.
    /// </summary>
    /// <param name="partial">The <see cref="PartialConstraint"/>.</param>
    /// <param name="term">The <see cref="Term"/>.</param>
    /// <returns>New <see cref="Constraint"/> instance by sub <see cref="Expression"/> from <paramref name="term"/>.</returns>
    public static Constraint operator |(PartialConstraint partial, Term term)
        => new(partial.Expression - term, partial.Relation);

    /// <summary>
    /// Create a <see cref="Constraint"/> from a <see cref="PartialConstraint"/> and a <see cref="Expression"/>.
    /// </summary>
    /// <param name="partial">The <see cref="PartialConstraint"/>.</param>
    /// <param name="expression">The <see cref="Cassowary.Expression"/>.</param>
    /// <returns>New <see cref="Constraint"/> instance by sub <see cref="Expression"/> from <paramref name="expression"/>.</returns>
    public static Constraint operator |(PartialConstraint partial, Expression expression)
        => new(partial.Expression - expression, partial.Relation);
}
