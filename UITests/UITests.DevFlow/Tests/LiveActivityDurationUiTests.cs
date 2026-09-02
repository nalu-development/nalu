using System.Globalization;
using System.Text;
using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Timer ranges an iOS Live Activity is expected to survive: still ahead, and already elapsed.
/// </summary>
/// <remarks>
/// <para>
/// Written while chasing a field report of "the Live Activity does not show up when the timer is
/// longer than 24 hours". Two findings came out of it, both worth not re-deriving.
/// </para>
/// <para>
/// FIRST — ActivityKit accepts ANY range. Verified on an iPhone 13 at 0.2/4/8/12/23/25/48 h into
/// the future and at 0.5/2/22 h already elapsed: every one returns an active activity. A long or
/// expired timer is never the reason a request fails.
/// </para>
/// <para>
/// SECOND — and this is why the test BACKGROUNDS the app rather than trusting the status label:
/// a foreground app never shows its own Live Activity, so "StartAsync returned Started" is not
/// evidence of presentation. Presentation was verified out of band from SpringBoard's own
/// telemetry (<c>idevicesyslog</c>), where an activity ending 22 hours ago produced an event
/// sequence identical to a normal one — 120 lines, 73 distinct shapes, both carrying
/// <c>SpringBoard(CoverSheet): Inserting supplementary item</c> and
/// <c>WidgetRenderer_Activities: Rendering view: AnyView(…)</c>. The widget renders an elapsed
/// range fine, so the overflow branch's 1 h bound is not a problem.
/// </para>
/// <para>
/// The actual cause of that report was neither: the activity had been alive for 23 hours, and
/// iOS ends a Live Activity after about 8 hours. Nothing in the content model can extend that —
/// an app whose session outlives the limit has to end and re-request. Related, and the other
/// half of the same report: the Dynamic Island presentation needs an iPhone 14 Pro or later; on
/// a notch device (iPhone 13 and earlier) only the Lock Screen presentation ever appears.
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
    public async Task FutureAndElapsedTimersAreBothAccepted()
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
                failures.Add($"{hours}h -> {status}");
            }
        }

        await File.WriteAllTextAsync(
            Environment.GetEnvironmentVariable("LA_REPORT") ?? Path.Combine(AppContext.BaseDirectory, "live-activity-duration.txt"),
            report.ToString()
        );

        failures.Should().BeEmpty($"ActivityKit accepts a countdown of any length, elapsed or not{Environment.NewLine}{Environment.NewLine}{report}");
    }
}
