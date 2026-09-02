using System.Globalization;
using System.Text;
using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// REPRODUCTION suite for "Failed to stage downloaded file … (source file exists: False)":
/// bursts of simultaneous background-session downloads whose temp files nsurlsessiond reclaims
/// before <c>DidFinishDownloading</c> gets its turn on the serial delegate queue.
/// </summary>
/// <remarks>
/// <para>
/// This suite is deliberately DIAGNOSTIC rather than regression-shaped: it sweeps the three
/// candidate drivers independently — burst size, payload size and per-call logger cost — and
/// always writes the whole table to <c>BURST_REPORT</c>, because a single red/green verdict
/// would not say WHICH one moves the needle.
/// </para>
/// <para>
/// RESULT SO FAR — it does NOT reproduce, and that is the finding. On an iPhone 13 every cell
/// came back <c>staging=0</c>, including 48 concurrent 5 MB downloads with the delegate's
/// logger deliberately crippled to 50 ms per call:
/// </para>
/// <code>
/// burst payload      logMs | done  ok staging elapsedMs maxMs logCalls
///    48 /ok             50 |   48  48       0     10274 10197      336
///    48 /huge?mb=5      50 |   48  48       0     61665 52393      336
/// </code>
/// <para>
/// The logger axis is demonstrably live (<c>logCalls</c> ≈ 7 per request, and the delay inflates
/// a 48-request burst from 331 ms to 10.3 s), so the delegate queue really was backed up by
/// SECONDS — and nsurlsessiond still never reclaimed a temp file. Queue latency alone therefore
/// does not explain "Failed to stage downloaded file … (source file exists: False)".
/// </para>
/// <para>
/// What that redirects attention to: the source file lives under
/// <c>Library/Caches/com.apple.nsurlsessiond/Downloads/</c>, and <c>Library/Caches</c> is exactly
/// what iOS purges under storage pressure, at any moment and independently of how fast the
/// delegate runs. Reproducing it most likely needs a device with little free space (or the
/// lifecycle transitions the <c>BackgroundHttpLifecycleUiTests</c> suite already covers), not a
/// bigger burst.
/// </para>
/// <para>
/// SUPERSEDED as a reproduction by the <c>lost-staging</c> scenario in the callback-injection
/// suite, which reaches the real production failure deterministically and in seconds. This
/// suite is kept for the negative result above — the evidence that queue latency is NOT the
/// mechanism, which is worth not re-deriving.
/// </para>
/// <para>
/// Physical device only in practice: staging is a rename inside the app container, so the loss
/// happens off-device-CPU in nsurlsessiond and a simulator will not schedule it the same way.
/// Run with <c>DEVFLOW_HOST=localhost DEVFLOW_PORT=9224</c> behind
/// <c>iproxy 9224 9224 -u &lt;udid&gt;</c>.
/// </para>
/// </remarks>
public class BackgroundHttpBurstUiTests(NaluApp app, ChaosServerFixture chaos) : BaseUiTest(app), IClassFixture<ChaosServerFixture>
{
    private const string _pageName = "Background Http Burst";

    /// <summary>A burst of 5 MB downloads over Wi-Fi needs room; the page carries no timeout.</summary>
    private static readonly TimeSpan _burstTimeout = TimeSpan.FromMinutes(5);

    private async Task OpenAsync()
    {
        var platform = await App.GetPlatformAsync();
        Assert.SkipUnless(platform.Contains("ios", StringComparison.OrdinalIgnoreCase), "The background NSUrlSession harness is iOS-only.");
        Assert.SkipWhen(chaos.LanAddress is null, "This machine has no LAN IPv4 for the device to reach.");

        await App.OpenTestPageAsync(_pageName);

        var mode = await App.WaitForTextMatchAsync("BurstModeLabel", t => t is "device" or "simulator-bg" or "simulator-default");
        Assert.SkipWhen(mode == "simulator-default", "This simulator cannot create background NSUrlSessions.");

        await App.FillVerifiedAsync("BurstBaseUrl", chaos.BaseUrl);
    }

    /// <summary>Runs one cell of the matrix and returns the parsed summary counters.</summary>
    private async Task<IReadOnlyDictionary<string, long>> RunAsync(int count, string path, int logDelayMs)
    {
        await App.FillVerifiedAsync("BurstPath", path);
        await App.FillVerifiedAsync("BurstCount", count.ToString(CultureInfo.InvariantCulture));
        await App.FillVerifiedAsync("BurstLogDelayMs", logDelayMs.ToString(CultureInfo.InvariantCulture));
        await App.TapAsync("BurstRunButton");

        var summary = await App.WaitForTextMatchAsync(
            "BurstSummaryLabel",
            t => t is not null && t != "idle" && t != "running",
            _burstTimeout
        );

        summary.Should().NotBeNull().And.NotStartWith("FAILED", "the page itself must not blow up");

        return Parse(summary!);
    }

    private static IReadOnlyDictionary<string, long> Parse(string summary)
    {
        var values = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var token in summary.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = token.IndexOf('=', StringComparison.Ordinal);

            if (separator > 0 && long.TryParse(token[(separator + 1)..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                values[token[..separator]] = value;
            }
        }

        return values;
    }

    /// <summary>
    /// The three axes crossed. Small payload isolates queue latency from allocation pressure;
    /// the 10 ms logger stands in for a provider (Sentry, a flushing file sink) that does
    /// synchronous work inside every one of the delegate's ~12 on-queue Log calls.
    /// </summary>
    private static readonly (int Count, string Path, int LogDelayMs)[] _matrix =
    [
        // Small payload isolates pure queue latency from allocation pressure.
        (8, "/ok", 0),
        (24, "/ok", 0),
        (48, "/ok", 0),
        (24, "/ok", 25),
        (48, "/ok", 25),
        (48, "/ok", 50),
        // Big payload adds the deferred-processing allocation the GC-pressure theory needs.
        (8, "/huge?mb=5", 0),
        (24, "/huge?mb=5", 0),
        (24, "/huge?mb=5", 25),
        (48, "/huge?mb=5", 50)
    ];

    /// <summary>
    /// Where the sweep table is written. A passing run prints nothing through the assertion, and
    /// the table is the actual product of this suite — so it always goes to disk.
    /// </summary>
    private static string ReportPath
        => Environment.GetEnvironmentVariable("BURST_REPORT") ?? Path.Combine(AppContext.BaseDirectory, "burst-sweep.txt");

    [Fact]
    public async Task Sweep()
    {
        await OpenAsync();

        var table = new StringBuilder();
        table.AppendLine("burst payload            logMs | done  ok staging procerr err canceled elapsedMs maxMs logCalls");

        var staged = 0L;

        foreach (var (count, path, logDelayMs) in _matrix)
        {
            var result = await RunAsync(count, path, logDelayMs);
            staged += result.GetValueOrDefault("staging");

            table.AppendLine(CultureInfo.InvariantCulture, $"{count,5} {path,-18} {logDelayMs,5} | {result.GetValueOrDefault("done"),4} {result.GetValueOrDefault("ok"),3} {result.GetValueOrDefault("staging"),7} {result.GetValueOrDefault("procerr"),7} {result.GetValueOrDefault("err"),3} {result.GetValueOrDefault("canceled"),8} {result.GetValueOrDefault("elapsedMs"),9} {result.GetValueOrDefault("maxMs"),5} {result.GetValueOrDefault("logCalls"),8}");
        }

        await File.WriteAllTextAsync(ReportPath, table.ToString());

        staged.Should().Be(0, $"no burst should lose a downloaded file to staging\n\n{table}");
    }
}
