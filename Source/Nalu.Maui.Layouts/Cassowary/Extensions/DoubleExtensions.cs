namespace Nalu.Cassowary.Extensions;

internal static class DoubleExtensions
{
    public static bool IsNearZero(this double value)
    {
        const double eps = 1E-8f;

        if (value < 0)
        {
            return -value < eps;
        }

        return value < eps;
    }

    public static double Max(this double source, double other)
        => Math.Max(source, other);

    public static double Min(this double source, double other)
        => Math.Min(source, other);
}
