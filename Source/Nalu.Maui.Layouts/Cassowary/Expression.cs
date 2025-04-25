using System.Collections.Immutable;

namespace Nalu.Cassowary;

/// <summary>
/// An expression that can be the left hand or right hand side of a constraint equation.
/// It is a linear combination of variables, i.e. a sum of variables weighted by coefficients, plus an optional constant.
/// </summary>
public readonly struct Expression : IEquatable<Expression>
{
    /// <summary>
    /// Gets the terms of the expression.
    /// </summary>
    public ImmutableArray<Term> Terms { get; init; } = [];

    /// <summary>
    /// Gets the constant value of the expression.
    /// </summary>
    public double Constant { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Expression" /> struct.
    /// </summary>
    /// <param name="terms">Terms of the expression.</param>
    /// <param name="constant">Constant value.</param>
    public Expression(ImmutableArray<Term> terms, double constant)
    {
        Terms = terms;
        Constant = constant;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Expression" /> struct.
    /// </summary>
    public Expression()
    {
    }

    /// <summary>
    /// Deconstructs the expression into terms and constant.
    /// </summary>
    /// <param name="terms"></param>
    /// <param name="constant"></param>
    public void Deconstruct(out ImmutableArray<Term> terms, out double constant)
    {
        terms = Terms;
        constant = Constant;
    }

    /// <inheritdoc />
    // ReSharper disable once CompareOfFloatsByEqualityOperator
    public bool Equals(Expression other)
    {
        if (other.Constant != Constant)
        {
            return false;
        }

        if (Terms.IsDefaultOrEmpty && other.Terms.IsDefaultOrEmpty)
        {
            return true;
        }

        return Terms.SequenceEqual(other.Terms);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        if (obj == null || GetType() != obj.GetType())
        {
            return false;
        }

        return obj is Expression expression && Equals(expression);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = Constant.GetHashCode();
            var termsLength = Terms.Length;
            for (var i = 0; i < termsLength; i++)
            {
                hashCode = (hashCode * 19) ^ Terms[i].GetHashCode();
            }

            return hashCode;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var terms = string.Join(" + ", Terms);

        if (Constant == 0)
        {
            return terms;
        }

        if (Terms.Length == 0)
        {
            return $"{Constant}";
        }

        return $"{terms} + {Constant}";
    }

    /// <summary>
    /// Constructs an expression of the form _n_, where n is a constant real number, not a variable.
    /// </summary>
    /// <param name="constant">The constant value.</param>
    /// <returns>New instance of <see cref="Expression" />.</returns>
    public static Expression From(double constant) => new([], constant);

    /// <summary>
    /// Constructs an expression from a single term. Forms an expression of the form _n x_
    /// where n is the coefficient, and x is the variable.
    /// </summary>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <returns>New instance of <see cref="Expression" />.</returns>
    public static Expression From(Term term) => new([term], 0);

    /// <summary>
    /// Constructs an expression from a single variable. Forms an expression of the form _x_
    /// </summary>
    /// <param name="variable">The <see cref="Variable" />.</param>
    /// <returns>New instance of <see cref="Expression" />.</returns>
    public static Expression From(Variable variable) => From(new Term(variable, 1));

    #region operator +

    /// <summary>
    /// Sum <see cref="Expression" /> with a <see cref="float" /> value.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>New <see cref="Expression" /> instance with a constant sum.</returns>
    public static Expression operator +(Expression expression, float value)
        => expression + (double) value;

    /// <summary>
    /// Sum <see cref="Expression" /> with a <see cref="float" /> value.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>New <see cref="Expression" /> instance with a constant sum.</returns>
    public static Expression operator +(Expression expression, double value)
        => expression with { Constant = expression.Constant + value };

    /// <summary>
    /// Sum <see cref="Expression" /> with a <see cref="float" /> value.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>New <see cref="Expression" /> instance with a constant sum.</returns>
    public static Expression operator +(float value, Expression expression)
        => expression + (double) value;

    /// <summary>
    /// Sum <see cref="Expression" /> with a <see cref="float" /> value.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The value.</param>
    /// <returns>New <see cref="Expression" /> instance with a constant sum.</returns>
    public static Expression operator +(double value, Expression expression)
        => expression + value;

    /// <summary>
    /// Sum <see cref="Expression" /> with a <see cref="Variable" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="variable">The <see cref="Variable" />.</param>
    /// <returns>New <see cref="Expression" /> instance with a <see cref="Term" /> with a coefficient value as -1.</returns>
    public static Expression operator +(Expression expression, Variable variable)
        => expression with { Terms = expression.Terms.Add(new Term(variable, 1)) };

    /// <summary>
    /// Sum two <see cref="Expression" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="other">The <see cref="Expression" />.</param>
    /// <returns>New <see cref="Expression" /> instance with a joins terms and constant sum.</returns>
    public static Expression operator +(Expression expression, Expression other)
        => new(expression.Terms.AddRange(other.Terms), expression.Constant + other.Constant);

    /// <summary>
    /// Sum two <see cref="Expression" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="other">The <see cref="Term" />.</param>
    /// <returns>New <see cref="Expression" /> instance with a joins terms and constant sum.</returns>
    public static Expression operator +(Expression expression, Term other)
        => new(expression.Terms.Add(other), expression.Constant);

    #endregion

    #region operator -

    /// <summary>
    /// Negate <see cref="Expression" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <returns>New instance of <see cref="Expression" /> with negative <see cref="Cassowary.Term" /> and constant.</returns>
    public static Expression operator -(Expression expression)
        => new(ImmutableArray.CreateRange(expression.Terms, term => -term), -expression.Constant);

    /// <summary>
    /// Subtract <see cref="float" /> value from <see cref="Expression" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The <see cref="float" />.</param>
    /// <returns>New <see cref="Expression" /> instance with a constant sub.</returns>
    public static Expression operator -(Expression expression, float value)
        => expression - (double) value;

    /// <summary>
    /// Subtract <see cref="float" /> value from <see cref="Expression" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The <see cref="float" />.</param>
    /// <returns>New <see cref="Expression" /> instance with a constant sub.</returns>
    public static Expression operator -(Expression expression, double value)
        => expression with { Constant = expression.Constant - value };

    /// <summary>
    /// Subtract <see cref="float" /> value from <see cref="Expression" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The <see cref="float" />.</param>
    /// <returns>New <see cref="Expression" /> instance with a constant sub.</returns>
    public static Expression operator -(float value, Expression expression)
        => (double) value - expression;

    /// <summary>
    /// Subtract <see cref="float" /> value from <see cref="Expression" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The <see cref="float" />.</param>
    /// <returns>New <see cref="Expression" /> instance with a constant sub.</returns>
    public static Expression operator -(double value, Expression expression)
    {
        var negate = -expression;

        return negate with { Constant = negate.Constant + value };
    }

    /// <summary>
    /// Subtract <see cref="Term" /> from <see cref="Expression" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="term">The <see cref="Term" />.</param>
    /// <returns>New <see cref="Expression" /> with a negative <see cref="Term" />.</returns>
    public static Expression operator -(Expression expression, Term term)
        => expression with { Terms = expression.Terms.Add(-term) };

    /// <summary>
    /// Subtract <see cref="Variable" /> from <see cref="Expression" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="variable">The <see cref="Variable" />.</param>
    /// <returns>New <see cref="Expression" /> instance with a <see cref="Term" /> with a coefficient value as -1.</returns>
    public static Expression operator -(Expression expression, Variable variable)
        => expression with { Terms = expression.Terms.Add(new Term(variable, -1)) };

    /// <summary>
    /// Subtract <see cref="Expression" /> from <see cref="Expression" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="other">The <see cref="Expression" />.</param>
    /// <returns>New <see cref="Expression" /> instance by negative of <paramref name="other" /> and add terms and sum <see cref="Constant" />.</returns>
    public static Expression operator -(Expression expression, Expression other)
    {
        var negative = -other;

        return new Expression(expression.Terms.AddRange(negative.Terms), expression.Constant + negative.Constant);
    }

    #endregion

    #region operator |

    /// <summary>
    /// OR <see cref="Expression" /> with <see cref="WeightedRelation" />.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="relation">The <see cref="WeightedRelation" />.</param>
    /// <returns>New <see cref="PartialConstraint" /> instance.</returns>
    public static PartialConstraint operator |(Expression expression, WeightedRelation relation)
        => new(expression, relation);

    #endregion

    #region operator *

    /// <summary>
    /// Multiply <see cref="Expression" /> by <see cref="float" /> value.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The value</param>
    /// <returns>New <see cref="Expression" /> instance with <see cref="Term" /> and <see cref="Constant" /> multiply by <paramref name="value" />.</returns>
    public static Expression operator *(Expression expression, float value)
        => expression * (double) value;

    /// <summary>
    /// Multiply <see cref="Expression" /> by <see cref="float" /> value.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The value</param>
    /// <returns>New <see cref="Expression" /> instance with <see cref="Term" /> and <see cref="Constant" /> multiply by <paramref name="value" />.</returns>
    public static Expression operator *(Expression expression, double value)
        => new(
            ImmutableArray.CreateRange(expression.Terms, x => x * value),
            expression.Constant * value
        );

    /// <summary>
    /// Multiply <see cref="Expression" /> by <see cref="float" /> value.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The value</param>
    /// <returns>New <see cref="Expression" /> instance with <see cref="Term" /> and <see cref="Constant" /> multiply by <paramref name="value" />.</returns>
    public static Expression operator *(float value, Expression expression)
        => expression * (double) value;

    /// <summary>
    /// Multiply <see cref="Expression" /> by <see cref="float" /> value.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The value</param>
    /// <returns>New <see cref="Expression" /> instance with <see cref="Term" /> and <see cref="Constant" /> multiply by <paramref name="value" />.</returns>
    public static Expression operator *(double value, Expression expression)
        => expression * value;

    #endregion

    #region operator /

    /// <summary>
    /// Divide <see cref="Expression" /> by <see cref="float" /> value.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The value</param>
    /// <returns>New <see cref="Expression" /> instance with <see cref="Term" /> and <see cref="Constant" /> dividing by <paramref name="value" />.</returns>
    public static Expression operator /(Expression expression, float value)
        => expression / (double) value;

    /// <summary>
    /// Divide <see cref="Expression" /> by <see cref="float" /> value.
    /// </summary>
    /// <param name="expression">The <see cref="Expression" />.</param>
    /// <param name="value">The value</param>
    /// <returns>New <see cref="Expression" /> instance with <see cref="Term" /> and <see cref="Constant" /> dividing by <paramref name="value" />.</returns>
    public static Expression operator /(Expression expression, double value)
        => new(ImmutableArray.CreateRange(expression.Terms, term => term / value), expression.Constant / value);

    #endregion

    #region operator =

    /// <summary>
    /// Checks if two <see cref="Expression" /> are equal.
    /// </summary>
    /// <param name="left">Left <see cref="Expression" />.</param>
    /// <param name="right">Right <see cref="Expression" />.</param>
    public static bool operator ==(Expression left, Expression right)
        => left.Equals(right);

    /// <summary>
    /// Checks if two <see cref="Expression" /> are not equal.
    /// </summary>
    /// <param name="left">Left <see cref="Expression" />.</param>
    /// <param name="right">Right <see cref="Expression" />.</param>
    public static bool operator !=(Expression left, Expression right)
        => !left.Equals(right);

    #endregion
}
