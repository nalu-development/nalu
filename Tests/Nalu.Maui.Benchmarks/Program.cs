using BenchmarkDotNet.Running;

namespace Nalu.Maui.Benchmarks;

public class Program
{
#pragma warning disable IDE0060
    public static void Main(string[] args)
    {
        if (args.Length > 0)
        {
            if (args[0] == "--speedscope-magnet")
            {
                // dotnet build -c Release && dotnet trace collect --format speedscope -- dotnet bin/Release/net9.0/Nalu.Maui.Benchmarks.dll --speedscope-magnet && speedscope *.speedscope.json && rm -rf *.speedscope.json *.nettrace
                Console.WriteLine("Direct run");

                var magnetBenchmarks = new MagnetBenchmarks();
                magnetBenchmarks.MagnetLayoutPerf(100_000);

                return;
            }
            
            if (args[0] == "--speedscope-virtualscrollflattened")
            {
                // dotnet build -c Release Tests/Nalu.Maui.Benchmarks && dotnet trace collect --format speedscope -- dotnet Tests/Nalu.Maui.Benchmarks/bin/Release/net9.0/Nalu.Maui.Benchmarks.dll --speedscope-virtualscrollflattened && speedscope *.speedscope.json && rm -rf *.speedscope.json *.nettrace
                Console.WriteLine("Direct run");

                var virtualScrollBenchmarks = new VirtualScrollFlattenedAdapterBenchmarks
                                              {
                                                  SectionCount = 1000,
                                                  ItemsPerSection = 1000
                                              };
                virtualScrollBenchmarks.Setup();

                for (var i = 0; i < 10000; i++)
                {
                    virtualScrollBenchmarks.GetItem();
                }

                return;
            }
        }

        // dotnet run -c Release --project Tests/Nalu.Maui.Benchmarks
        // To run specific benchmark: dotnet run -c Release --project Tests/Nalu.Maui.Benchmarks -- --filter "*VirtualScrollFlattenedAdapterBenchmarks*"
        BenchmarkRunner.Run(typeof(MagnetBenchmarks).Assembly, null, args);
    }
#pragma warning restore IDE0060
}
