using System.Globalization;
using System.Text;

namespace Nalu.Cassowary;

/// <summary>
/// A constraint, consisting of an equation governed by an expression and a relational operator,
/// and an associated strength.
/// </summary>
public class Constraint
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Constraint" /> struct.
    /// </summary>
    /// <param name="expression">The <see cref="Cassowary.Expression" />.</param>
    /// <param name="operator">The <see cref="RelationalOperator" />.</param>
    /// <param name="strength">The strength.</param>
    public Constraint(Expression expression, RelationalOperator @operator, double strength)
    {
        Expression = expression;
        Operator = @operator;
        Strength = strength;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Constraint" /> struct.
    /// </summary>
    /// <param name="expression">The <see cref="Cassowary.Expression" />.</param>
    /// <param name="relation">The <see cref="WeightedRelation" />.</param>
    public Constraint(Expression expression, WeightedRelation relation)
        : this(expression, relation.Operator, relation.Strength) { }

    /// <summary>The <see cref="Cassowary.Expression" />.</summary>
    public Expression Expression { get; }

    /// <summary>The <see cref="RelationalOperator" />.</summary>
    public RelationalOperator Operator { get; }

    /// <summary>The strength.</summary>
    public double Strength { get; }

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

        var sb = new StringBuilder(200);

        if (!AppendTerms(sb, true))
        {
            sb.Append('0');
        }
        
        sb.Append(' ');
        sb.Append(operatorString);
        sb.Append(' ');

        var hasRightTerms = AppendTerms(sb, false);
        if (!hasRightTerms && Expression.Constant == 0)
        {
            sb.Append('0');
        }

        if (Expression.Constant != 0)
        {
            if (Expression.Constant < 0)
            {
                sb.Append(hasRightTerms ? " + " : string.Empty);
                sb.Append(-Expression.Constant);
            }
            else
            {
                sb.Append(hasRightTerms ? " - " : "-");
                sb.Append(Expression.Constant);
            }
        }

        sb.Append($" [{GetStrength()}]");
        
        return sb.ToString();
    }

    private string GetStrength() => Strength switch
    {
        1_000_000_000 => "Required",
        1_000_000 => "Strong",
        1_000 => "Medium",
        1 => "Weak",
        _ => Strength.ToString(CultureInfo.InvariantCulture)
    };

    private bool AppendTerms(StringBuilder sb, bool left)
    {
        var firstTerm = true;
        foreach (var term in Expression.Terms)
        {
            if (left && term.Coefficient > 0 || !left && term.Coefficient < 0)
            {
                if (!firstTerm)
                {
                    sb.Append(" + ");
                }

                firstTerm = false;

                if (term.Coefficient is not (1 or -1))
                {
                    sb.Append(left ? term.Coefficient : -term.Coefficient);
                    sb.Append('*');
                }

                sb.Append(term.Variable.Name);
            }
        }

        return !firstTerm;
    }
}
