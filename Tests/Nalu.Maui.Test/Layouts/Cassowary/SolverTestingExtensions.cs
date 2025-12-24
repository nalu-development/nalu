namespace Nalu.Cassowary;

public static class SolverTestingExtensions
{
    public static void ShouldHaveVariables(
        this Solver solver,
        params (Variable Variable, double Value)[] expected
    )
    {
        solver.FetchChanges();

        foreach (var (variable, value) in expected)
        {
            var actual = variable.CurrentValue;
            Math.Abs(actual - value).Should().BeLessThanOrEqualTo(0.1, $"Variable {variable.Name} should be {value} but was {actual}");
        }
    }
}
