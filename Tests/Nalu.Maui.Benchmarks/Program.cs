using BenchmarkDotNet.Running;

namespace Nalu.Maui.Benchmarks;

public class Program
{
#pragma warning disable IDE0060
    public static void Main(string[] args) => BenchmarkRunner.Run(typeof(MagnetBenchmarks).Assembly);
#pragma warning restore IDE0060
}
