namespace Nalu.Cassowary;

using Extensions;

/// <summary>
/// Contains useful constants and functions for producing strengths for use in the constraint solver.
/// Each constraint added to the solver has an associated strength specifying the precedence the solver should
/// impose when choosing which constraints to enforce. It will try to enforce all constraints, but if that
/// is impossible the lowest strength constraints are the first to be violated.
/// </summary>
/// <remarks>
/// <para>
/// Strengths are simply real numbers. The strongest legal strength is 1,001,001,000.0. The weakest is 0.0.
/// For convenience constants are declared for commonly used strengths. These are <see cref="Required"/>, <see cref="Strong"/>,
/// <see cref="Medium"/> and <see cref="Weak"/>. Feel free to multiply these by other values to get intermediate strengths.
/// Note that the solver will clip given strengths to the legal range.
/// </para>
///
/// <para>
/// <see cref="Required"/> signifies a constraint that cannot be violated under any circumstance. Use this special strength
/// sparingly, as the solver will fail completely if it find that not all of the `REQUIRED` constraints
/// can be satisfied. The other strengths represent fallible constraints. These should be the most
/// commonly used strenghts for use cases where violating a constraint is acceptable or even desired.
/// </para>
///
/// <para>
/// The solver will try to get as close to satisfying the constraints it violates as possible, strongest first.
/// This behaviour can be used (for example) to provide a "default" value for a variable should no other
/// stronger constraints be put upon it.
/// </para>
/// </remarks>
public static class Strength
{
    /// <summary>
    /// The required strength.
    /// </summary>
    public const double Required = 1_000_000_000;

    /// <summary>
    /// The strong strengths.
    /// </summary>
    public const double Strong = 1_000_000;

    /// <summary>
    /// The medium strength.
    /// </summary>
    public const double Medium = 1_000;

    /// <summary>
    /// The weak strength.
    /// </summary>
    public const double Weak = 1;

    /// <summary>
    /// Clips a strength value to the legal range.
    /// </summary>
    /// <param name="strength">The strength.</param>
    /// <returns>The clip value.</returns>
    public static double Clip(double strength)
        => strength.Min(Required).Max(0);

    /// <summary>
    /// Create a constraint as a linear combination of <see cref="Strong"/>, <see cref="Medium"/> and <see cref="Weak"/> strengths,
    /// corresponding to <paramref name="a"/> <paramref name="b"/> and <paramref name="c"/> respectively.
    /// The result is further multiplied by <paramref name="w"/>.
    /// </summary>
    /// <param name="a">The strong value.</param>
    /// <param name="b">The medium value.</param>
    /// <param name="c">The weak value.</param>
    /// <param name="w">The multiplied.</param>
    public static double Create(double a, double b, double c, double w) =>
        ((a * w).Max(0).Min(1_000) * Strong) +
        ((b * w).Max(0).Min(1_000) * Medium) +
        (c * w).Max(0).Min(1_000);
}
