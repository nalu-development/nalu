using BenchmarkDotNet.Attributes;
using Nalu.Cassowary;
using static Nalu.Cassowary.Strength;
using static Nalu.Cassowary.WeightedRelation;

namespace Nalu.Maui.Benchmarks;

public class SolverBenchmarks
{
    [Benchmark]
    public void Solve()
    {
        for (var i = 0; i < 100; i++)
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

            solver.FetchChanges();
        }
    }
}
