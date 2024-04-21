namespace Nalu.Maui.Test.Layouts;

using Cassowary;
using static Cassowary.Strength;
using static Cassowary.WeightedRelation;

public class CassowaryTests
{
    [Fact(DisplayName = "Test cassowary")]
    public void TestCassowary()
    {
        var solver = new Solver();

        var px1 = Expression.From(0);
        var px2 = Expression.From(100);

        var ax1 = new Variable();
        var ax2 = new Variable();
        var aw = Expression.From(20);

        // var bias = 0.5;

        // ax1 >= px1 => ax1 - px1 >= 0
        solver.AddConstraint(new Constraint(ax1 - px1, RelationalOperator.GreaterThanOrEqual, 0));

        // ax2 <= px2 => ax2 - px2 <= 0
        solver.AddConstraint(new Constraint(ax2 - px2, RelationalOperator.LessThanOrEqual, 0));

        // ax2 - ax1 = aw => ax2 - ax1 - aw = 0
        solver.AddConstraint(new Constraint(Expression.From(ax2) - ax1 - aw, RelationalOperator.Equal, 1));

        // bias:
        // (ax1 + ax2) / 2 = (px1 + px2) / 2
        var bias = 0.5;
        solver.AddConstraint(new Constraint(new Term(ax1, 0.5) + new Term(ax2, 0.5) - (px1 * bias) - (px2 * bias), RelationalOperator.Equal, 1));

        var changes = solver.FetchChanges();
    }

    [Fact(DisplayName = "Test cassowary chain")]
    public void TestCassowaryChain()
    {
        var solver = new Solver();

        // parent
        var px1 = new Expression([], 0);
        var px2 = new Expression([], 100);

        // view a
        var ax1 = new Variable();
        ax1.SetName("ax1");
        var ax2 = new Variable();
        ax2.SetName("ax2");

        // view b
        var bx1 = new Variable();
        bx1.SetName("bx1");
        var bx2 = new Variable();
        bx2.SetName("bx2");

        // Anchors
        solver.AddConstraint(ax1 | GreaterOrEq(Medium) | px1);
        solver.AddConstraint(ax2 | LessOrEq(Medium) | bx1);
        solver.AddConstraint(bx1 | GreaterOrEq(Medium) | ax2);
        solver.AddConstraint(bx2 | LessOrEq(Medium) | px2);

        // Bias
        // solver.AddConstraint(ax1 | Eq(Medium) | (px1 + ((bx1 - px1 - ax2 + ax1) * 0.5)));
        // solver.AddConstraint(bx1 | Eq(Medium) | (ax2 + ((px2 - ax2 - bx2 + bx1) * 0.5)));

        // Centered Bias (simplified)
        solver.AddConstraint(((ax2 + ax1) / 2) | Eq(Medium) | ((px1 + bx1) / 2));
        solver.AddConstraint(((bx2 + bx1) / 2) | Eq(Medium) | ((ax2 + px2) / 2));

        // Spread chain with weights
        var s = new Variable();
        s.SetName("s");

        solver.AddConstraint((bx1 - px1) | Eq(Weak) | (s * 2));
        solver.AddConstraint((px2 - ax2) | Eq(Weak) | (s * 1));

        var changes = solver.FetchChanges();
        foreach (var (variable, value) in changes)
        {
            variable.CurrentValue = value;
        }

        var result = $"ax1: {ax1.CurrentValue} | ax2: {ax2.CurrentValue} | bx1: {bx1.CurrentValue} | bx2: {bx2.CurrentValue}";
    }
}
