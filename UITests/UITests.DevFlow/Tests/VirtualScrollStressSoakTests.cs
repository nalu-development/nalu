using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Opt-in soak harness for rare VirtualScroll races: repeats the storm+scroll stress scenario
/// until an iteration fails, reporting the failing iteration number.
/// </summary>
/// <remarks>
/// Skipped unless <c>SOAK_ITERATIONS</c> is set (e.g. <c>SOAK_ITERATIONS=150 dotnet test ...
/// -- --filter-class Nalu.Maui.UITests.Tests.VirtualScrollStressSoakTests</c>).
/// This is the harness that reproduced (at iteration ~68) the July 2026 self-sizing relayout
/// livelock: the app froze at 100% CPU with no crash report, visible only as a "dead app" to
/// the suite. When an iteration fails, correlate the reported timestamp with the app console
/// (run via <c>xcrun simctl launch --console-pty</c>) and sample the process while it spins.
/// </remarks>
public class VirtualScrollStressSoakTests(NaluApp app) : BaseUiTest(app)
{
    private static readonly TimeSpan _stormTimeout = TimeSpan.FromSeconds(45);

    public static bool SoakEnabled => Environment.GetEnvironmentVariable("SOAK_ITERATIONS") is not null;

    [Fact(SkipUnless = nameof(SoakEnabled), Skip = "Soak harness: set SOAK_ITERATIONS to run")]
    public async Task MutationStormWhileScrollingSoak()
    {
        var iterations = int.TryParse(Environment.GetEnvironmentVariable("SOAK_ITERATIONS"), out var n) ? n : 60;

        for (var i = 1; i <= iterations; i++)
        {
            var startedAt = DateTimeOffset.Now;

            try
            {
                await App.OpenTestPageAsync("Virtual Scroll Stress Tests");
                await App.WaitForElementAsync("StressStatusLabel");

                await App.TapAsync("StormScrollButton");

                for (var j = 0; j < 6; j++)
                {
                    await App.SwipeAsync("StressScroll", j % 2 == 0 ? "up" : "down", 350);
                }

                await App.WaitForTextAsync("StressStatusLabel", "Done S:30 I:94", _stormTimeout);

                await App.TapAsync("ScrollToLastButton");
                var lastHeader = await App.WaitForTextMatchAsync("LastSectionLabel", t => t?.StartsWith("SH ") == true);
                await App.WaitForElementAsync(lastHeader!);

                await App.TapAsync("ScrollToFirstButton");
                var firstHeader = await App.WaitForTextMatchAsync("LastSectionLabel", t => t?.StartsWith("SH ") == true && t != lastHeader);
                await App.WaitForElementAsync(firstHeader!);
            }
            catch (Exception ex)
            {
                throw new Exception($"SOAK FAILED at iteration {i} (started {startedAt:HH:mm:ss.fff}, now {DateTimeOffset.Now:HH:mm:ss.fff}): {ex.Message}", ex);
            }

            Console.WriteLine($"SOAK iteration {i}/{iterations} OK at {DateTimeOffset.Now:HH:mm:ss.fff}");
        }
    }
}
