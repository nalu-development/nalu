using BenchmarkDotNet.Running;
using Nalu.Maui.Benchmarks;

namespace Microsoft.Maui.Handlers.Benchmarks;

public class Program
{
#pragma warning disable IDE0060
    public static void Main(string[] args) => BenchmarkRunner.Run<SolverBenchmarks>();
#pragma warning restore IDE0060
}
