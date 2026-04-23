using BenchmarkDotNet.Running;

namespace Nalu.SharpState.Benchmarks;

public static class Program
{
#pragma warning disable IDE0060
    public static void Main(string[] args) => BenchmarkRunner.Run(typeof(FlatActorBenchmarks).Assembly, null, args);
#pragma warning restore IDE0060
}
