using BenchmarkDotNet.Running;

namespace Nalu.Maui.Benchmarks;

public class Program
{
#pragma warning disable IDE0060
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--direct")
        {
            // dotnet build -c Release && dotnet trace collect --format speedscope -- dotnet bin/Release/net9.0/Nalu.Maui.Benchmarks.dll --direct && speedscope *.speedscope.json && rm -rf *.speedscope.json *.nettrace
            Console.WriteLine("Direct run");

            var magnetBenchmarks = new MagnetBenchmarks();
            magnetBenchmarks.MagnetSetup();
            magnetBenchmarks.MagnetLayoutPerfDirect(100_000);

            return;
        }

        // dotnet run -c Release --project Tests/Nalu.Maui.Benchmarks
        BenchmarkRunner.Run(typeof(MagnetBenchmarks).Assembly, null, args);
    }
#pragma warning restore IDE0060
}
