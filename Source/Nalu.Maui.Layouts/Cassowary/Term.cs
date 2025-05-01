namespace Nalu.Cassowary;

/// <summary>
/// A variable and a coefficient to multiply that variable by. This is a sub-expression in
/// a constraint equation.
/// </summary>
/// <param name="Variable">The <see cref="Cassowary.Variable" />.</param>
/// <param name="Coefficient">The coefficient.</param>
public readonly record struct Term(Variable Variable, double Coefficient)
{
    /// <inheritdoc />
    public override string ToString()
    {
        if (Coefficient == 1)
        {
            return Variable.ToString();
        }

        if (Coefficient == -1)
        {
            return $"-{Variable}";
        }

        return $"({Coefficient} * {Variable})";
    }

    /// <summary>
    /// Create new <see cref="PartialConstraint" /> based on <see cref="Term" /> and <see cref="WeightedRelation" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="relation">The <see cref="WeightedRelation" />.</param>
    /// <returns>New <see cref="PartialConstraint" /> instance.</returns>
    public static PartialConstraint operator |(Term term, WeightedRelation relation)
        => new(Expression.From(term), relation);

    #region operator +

    /// <summary>
    /// Add <paramref name="value" /> to <paramref name="term" /> and return new <see cref="Expression" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value.</param>
    /// <returns> Add <paramref name="value" /> to <paramref name="term" /> and return new <see cref="Expression" />.</returns>
    public static Expression operator +(Term term, float value)
        => term + (double) value;

    /// <summary>
    /// Add <paramref name="value" /> to <paramref name="term" /> and return new <see cref="Expression" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value.</param>
    /// <returns> Add <paramref name="value" /> to <paramref name="term" /> and return new <see cref="Expression" />.</returns>
    public static Expression operator +(Term term, double value)
        => new([term], value);

    /// <summary>
    /// Add <paramref name="value" /> to <paramref name="term" /> and return new <see cref="Expression" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value.</param>
    /// <returns> Add <paramref name="value" /> to <paramref name="term" /> and return new <see cref="Expression" />.</returns>
    public static Expression operator +(float value, Term term)
        => term + (double) value;

    /// <summary>
    /// Add <paramref name="value" /> to <paramref name="term" /> and return new <see cref="Expression" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>Add <paramref name="value" /> to <paramref name="term" /> and return new <see cref="Expression" />.</returns>
    public static Expression operator +(double value, Term term)
        => term + value;

    /// <summary>
    /// Add <paramref name="term" /> to <paramref name="other" /> and return new <see cref="Expression" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="other">The <see cref="Term" />.</param>
    /// <returns>Create new instance of <see cref="Expression" /> with both <see cref="Term" />.</returns>
    public static Expression operator +(Term term, Term other)
        => new([term, other], 0);

    #endregion

    #region operator -

    /// <summary>
    /// Negate <paramref name="term" /> and return new <see cref="Term" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <returns>New <see cref="Term" /> instance with negate <see cref="Coefficient" />.</returns>
    public static Term operator -(Term term) => term with { Coefficient = -term.Coefficient };

    /// <summary>
    /// Subtract <paramref name="value" /> from <paramref name="term" /> and return new <see cref="Expression" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value</param>
    /// <returns>New <see cref="Expression" /> with the <paramref name="term" /> and negate <paramref name="value" />.</returns>
    public static Expression operator -(Term term, float value)
        => term - (double) value;

    /// <summary>
    /// Subtract <paramref name="value" /> from <paramref name="term" /> and return new <see cref="Expression" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value</param>
    /// <returns>New <see cref="Expression" /> with the <paramref name="term" /> and negate <paramref name="value" />.</returns>
    public static Expression operator -(Term term, double value)
        => new([term], -value);

    /// <summary>
    /// Subtract <paramref name="value" /> from <paramref name="term" /> and return new <see cref="Expression" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value</param>
    /// <returns>New <see cref="Expression" /> with the <paramref name="term" /> and negate <paramref name="value" />.</returns>
    public static Expression operator -(float value, Term term)
        => (double) value - term;

    /// <summary>
    /// Subtract <paramref name="value" /> from <paramref name="term" /> and return new <see cref="Expression" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value</param>
    /// <returns>New <see cref="Expression" /> with the <paramref name="term" /> and negate <paramref name="value" />.</returns>
    public static Expression operator -(double value, Term term)
        => new([-term], value);

    /// <summary>
    /// Subtract <paramref name="term" /> from <paramref name="expression" /> and return new <see cref="Expression" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <returns>New <see cref="Expression" /> instance by negate <paramref name="expression" /> and add the <paramref name="term" />.</returns>
    public static Expression operator -(Term term, Expression expression)
    {
        var negate = -expression;

        return negate with { Terms = negate.Terms.Add(term) };
    }

    #endregion

    #region operator *

    /// <summary>
    /// Multiply <paramref name="term" /> by <paramref name="value" /> and return new <see cref="Term" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>New <see cref="Term" /> instance with <see cref="Coefficient" /> multiply by <paramref name="value" />.</returns>
    public static Term operator *(Term term, float value)
        => term * (double) value;

    /// <summary>
    /// Multiply <paramref name="term" /> by <paramref name="value" /> and return new <see cref="Term" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>New <see cref="Term" /> instance with <see cref="Coefficient" /> multiply by <paramref name="value" />.</returns>
    public static Term operator *(Term term, double value)
        => term with { Coefficient = term.Coefficient * value };

    /// <summary>
    /// Multiply <paramref name="term" /> by <paramref name="value" /> and return new <see cref="Term" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>New <see cref="Term" /> instance with <see cref="Coefficient" /> multiply by <paramref name="value" />.</returns>
    public static Term operator *(float value, Term term)
        => term * value;

    /// <summary>
    /// Multiply <paramref name="term" /> by <paramref name="value" /> and return new <see cref="Term" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>New <see cref="Term" /> instance with <see cref="Coefficient" /> multiply by <paramref name="value" />.</returns>
    public static Term operator *(double value, Term term)
        => term * value;

    #endregion

    #region operator /

    /// <summary>
    /// Divide <paramref name="term" /> by <paramref name="value" /> and return new <see cref="Term" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>New <see cref="Term" /> instance with <see cref="Coefficient" /> dividing by <paramref name="value" />.</returns>
    public static Term operator /(Term term, float value)
        => term / (double) value;

    /// <summary>
    /// Divide <paramref name="term" /> by <paramref name="value" /> and return new <see cref="Term" />.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>New <see cref="Term" /> instance with <see cref="Coefficient" /> dividing by <paramref name="value" />.</returns>
    public static Term operator /(Term term, double value)
        => term with { Coefficient = term.Coefficient / value };

    #endregion
}
