using System.Text;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Perf-stats collector (not an assertion suite): drives the "Magnet Perf" page — cold inflate to warm the
/// JIT, warm inflate, text change — for both flavours and appends the page's settled stats to
/// MAGNET_PERF_OUT (skipped when the variable is unset, so normal runs don't pay for it).
/// </summary>
public class MagnetPerfStatsTests(NaluApp app) : BaseUiTest(app)
{
    [Fact]
    public async Task CollectPerfStats()
    {
        var outPath = Environment.GetEnvironmentVariable("MAGNET_PERF_OUT");
        Assert.SkipWhen(string.IsNullOrEmpty(outPath), "MAGNET_PERF_OUT not set: stats collection is opt-in.");

        await App.OpenTestPageAsync("Magnet Perf");
        await App.WaitForElementAsync("PerfGridButton");
        var platform = await App.GetPlatformAsync();
        var sb = new StringBuilder().AppendLine($"=== {platform} · {DateTime.Now:HH:mm} ===");

        foreach (var (label, button) in new[] { ("Grid", "PerfGridButton"), ("Magnet", "PerfMagnetButton") })
        {
            await TapAndSettleAsync(button); // cold run: JIT/warmup, discarded
            sb.AppendLine($"[{label} inflate warm] {await TapAndSettleAsync(button)}");
            sb.AppendLine($"[{label} text change] {await TapAndSettleAsync("PerfTextButton")}");
            await App.TapAsync("PerfClearButton");
        }

        File.AppendAllText(outPath!, sb.ToString());
    }

    /// <summary>
    /// Taps and waits for THIS run's settled stats: the status text must first move away from the previous
    /// content (a new scenario resets it to "waiting for layout…"), then report "settled".
    /// </summary>
    private async Task<string> TapAndSettleAsync(string button)
    {
        var previous = await App.GetElementPropertyAsync("PerfStatus", "Text");
        await App.TapAsync(button);
        var deadline = DateTime.UtcNow.AddSeconds(60);

        while (DateTime.UtcNow < deadline)
        {
            var text = await App.GetElementPropertyAsync("PerfStatus", "Text");

            if (text is not null && text != previous && text.Contains("settled"))
            {
                return text.ReplaceLineEndings(" · ");
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("Perf page never settled.");
    }
}
