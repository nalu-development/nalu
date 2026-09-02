using System.Globalization;
using System.Text;
using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Guards the rule that a Live Activity is never handed a stale date already in the past.
/// </summary>
/// <remarks>
/// <para>
/// Field report: "the Live Activity does not show up when the timer is longer than 24 hours."
/// The duration turned out to be a red herring — ActivityKit accepts ANY range (verified on an
/// iPhone 13 from 48 h ahead to 22 h elapsed, every one returning an active activity) and the
/// widget renders an elapsed range correctly.
/// </para>
/// <para>
/// The actual cause is the STALE DATE. Setting <c>StaleAt</c> to the countdown end — which the
/// appointment pattern in the docs recommends — makes it a PAST instant once that end goes by,
/// and ActivityKit then creates the activity directly in <c>ActivityState.stale</c>. A stale
/// activity is never presented at all. Measured on device from SpringBoard's telemetry:
/// </para>
/// <code>
/// case          staleDate    CoverSheet insert   state
/// 0.2 h ahead   ahead                1           active -> dismissed
/// -0.5 h        30 min past          0           stale  -> dismissed
/// -2 h          2 h past             0           stale  -> dismissed
/// -22 h         22 h past            0           stale  -> dismissed
/// </code>
/// <para>
/// The control logs 115 lines including "CoverSheet: Inserting supplementary item"; the stale
/// cases log 17 with no insert and no render. Note it already happens at MINUS THIRTY MINUTES:
/// the threshold is not 24 h, it is simply "in the past".
/// </para>
/// <para>
/// <c>ToStaleEpochMs</c> now drops such a date, so this suite asserts the ActivityKit state is
/// "active" for every case. It reads that state from the bridge rather than from the handle,
/// because the managed handle cannot see staleness — and it backgrounds the app for every case,
/// because a foreground app never shows its own Live Activity and a start call that returned
/// successfully is NOT evidence of presentation.
/// </para>
/// <para>
/// Two unrelated limits found along the way, worth not re-deriving: iOS ends a Live Activity
/// after about 8 hours, and the Dynamic Island presentation needs an iPhone 14 Pro or later —
/// on a notch device only the Lock Screen presentation ever appears.
/// </para>
/// </remarks>
public class LiveActivityDurationUiTests(NaluApp app) : BaseUiTest(app)
{
    private const string _pageName = "Live Activity Tests";

    private async Task OpenAsync()
    {
        var platform = await App.GetPlatformAsync();
        Assert.SkipUnless(platform.Contains("ios", StringComparison.OrdinalIgnoreCase), "Live Activities are an iOS feature.");

        await App.OpenTestPageAsync(_pageName);
        await App.TapAsync("LiveActivityRequestPermission");

        var permission = await App.WaitForTextMatchAsync("LiveActivityStatus", t => t is not null && t.StartsWith("Permission", StringComparison.Ordinal));
        Assert.SkipWhen(permission!.Contains("granted: False", StringComparison.Ordinal), $"Live Activities are not enabled on this device: {permission}");
    }

    /// <summary>Starts a countdown of <paramref name="hours" /> (negative = already elapsed).</summary>
    private async Task<string> StartAsync(double hours)
    {
        await App.FillVerifiedAsync("LiveActivityHours", hours.ToString(CultureInfo.InvariantCulture));
        await App.TapAsync("LiveActivityStart");

        var status = await App.WaitForTextMatchAsync(
            "LiveActivityStatus",
            t => t is not null && (t.StartsWith("Started", StringComparison.Ordinal) || t.StartsWith("Error", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(30)
        );

        // Leaving the app is what lets the system present the activity at all, and is therefore
        // where a rendering failure would surface.
        await App.BackgroundAppAsync();
        await Task.Delay(TimeSpan.FromSeconds(15));
        await App.ForegroundAppAsync();

        // Dismiss so the next case starts clean: the page ADOPTS a surviving "demo" activity.
        await App.TapAsync("LiveActivityEndImmediate");
        await App.WaitForTextMatchAsync("LiveActivityStatus", t => t is not null && t.StartsWith("Ended", StringComparison.Ordinal), TimeSpan.FromSeconds(30));

        return status!;
    }

    [Fact]
    public async Task ElapsedTimersAreNotBornStale()
    {
        await OpenAsync();

        var report = new StringBuilder();
        var failures = new List<string>();

        // 0.2 h is the harness default and the known-good control. NEGATIVE = end already in the
        // past, driving the widget's overflow branch; -22 is the reported case.
        foreach (var hours in (double[]) [0.2, -0.5, -2, -22])
        {
            var status = await StartAsync(hours);
            report.AppendLine(CultureInfo.InvariantCulture, $"{hours,5} h | {status}");

            if (!status.StartsWith("Started", StringComparison.Ordinal))
            {
                failures.Add($"{hours}h was not accepted -> {status}");

                continue;
            }

            // "kit=stale" is the regression: such an activity is never put on the Lock Screen.
            var kit = status.Split("kit=", StringSplitOptions.None) is [_, { } tail] ? tail.Trim() : "(missing)";

            if (kit != "active")
            {
                failures.Add($"{hours}h reached ActivityKit as '{kit}', expected 'active'");
            }
        }

        await File.WriteAllTextAsync(
            Environment.GetEnvironmentVariable("LA_REPORT") ?? Path.Combine(AppContext.BaseDirectory, "live-activity-duration.txt"),
            report.ToString()
        );

        failures.Should().BeEmpty(
            $"every timer, elapsed or not, must reach ActivityKit ACTIVE — a stale one is never presented{Environment.NewLine}{Environment.NewLine}{report}"
        );
    }
}
