using Nalu.Cassowary;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.Maui.Test.Layouts.Cassowary;

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

        solver.FetchChanges();
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
        solver.AddConstraint(ax2 | LessOrEq(Medium) | (bx1 - 10));
        solver.AddConstraint(bx1 | GreaterOrEq(Medium) | ax2);
        solver.AddConstraint(bx2 | LessOrEq(Medium) | px2);

        // Bias
        // solver.AddConstraint(ax1 | Eq(Medium) | (px1 + ((bx1 - px1 - ax2 + ax1) * 0.5)));
        // solver.AddConstraint(bx1 | Eq(Medium) | (ax2 + ((px2 - ax2 - bx2 + bx1) * 0.5)));

        // Centered Bias (simplified)
        // solver.AddConstraint(((ax2 + ax1) / 2) | Eq(Medium) | ((px1 + bx1) / 2));
        // solver.AddConstraint(((bx2 + bx1) / 2) | Eq(Medium) | ((ax2 + px2) / 2));

        // Spread chain with weights
        var s = new Variable();
        s.SetName("s");

        solver.AddConstraint((ax2 - ax1) | Eq(Weak) | (s * 2));
        solver.AddConstraint((bx2 - bx1) | Eq(Weak) | (s * 1));

        solver.ShouldHaveVariables((ax1, 0), (ax2, 60), (bx1, 70), (bx2, 100));
    }

    [Fact(DisplayName = "Test cassowary chain3")]
    public void TestCassowaryChain3()
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

        // view c
        var cx1 = new Variable();
        cx1.SetName("cx1");
        var cx2 = new Variable();
        cx2.SetName("cx2");

        // Anchors
        solver.AddConstraint(ax1 | GreaterOrEq(Medium) | px1);
        solver.AddConstraint(ax2 | LessOrEq(Medium) | (bx1 - 10));
        solver.AddConstraint(bx1 | GreaterOrEq(Medium) | ax2);
        solver.AddConstraint(bx2 | LessOrEq(Medium) | cx1);
        solver.AddConstraint(cx1 | GreaterOrEq(Medium) | bx2);
        solver.AddConstraint(cx2 | LessOrEq(Medium) | px2);

        // Bias
        // solver.AddConstraint(ax1 | Eq(Medium) | (px1 + ((bx1 - px1 - ax2 + ax1) * 0.5)));
        // solver.AddConstraint(bx1 | Eq(Medium) | (ax2 + ((px2 - ax2 - bx2 + bx1) * 0.5)));

        // Centered Bias (simplified)
        // solver.AddConstraint(((ax2 + ax1) / 2) | Eq(Medium) | ((px1 + bx1) / 2));
        // solver.AddConstraint(((bx2 + bx1) / 2) | Eq(Medium) | ((ax2 + px2) / 2));

        // Spread chain with weights
        var s = new Variable();
        s.SetName("s");

        solver.AddConstraint((ax2 - ax1) | Eq(Weak) | s);
        solver.AddConstraint((bx2 - bx1) | Eq(Weak) | s);
        solver.AddConstraint((cx2 - cx1) | Eq(Weak) | s);

        solver.ShouldHaveVariables(
            (ax1, 0),
            (ax2, 30),
            (bx1, 40),
            (bx2, 70),
            (cx1, 70),
            (cx2, 100)
        );
    }
}
