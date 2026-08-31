using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// APP-LIFECYCLE matrix for the background NSUrlSession pipeline: requests are started on the
/// "Background Http Lifecycle" TestApp page, then the HOST backgrounds / kills / relaunches the
/// app (simctl on simulators, devicectl over USB on a physical iPhone) while the in-process
/// chaos server serves delayed responses and faults over the LAN. Assertions read the page's
/// outcome labels — including <c>LostSummary</c>, fed by the lost-message flow in the RELAUNCHED
/// process — plus the server's request log.
/// </summary>
/// <remarks>
/// Same use-case coverage as the foreground chaos suite, across lifecycle transitions:
/// delayed success (parallel), upload, fail-fast fault (-1007), silently-retrying fault,
/// mid-flight download — each against backgrounding and/or kill+relaunch.
/// Physical device: run <c>iproxy 9224 9224</c> AND <c>iproxy 10224 10224</c> (relaunch can land
/// on the fallback port), with <c>DEVFLOW_HOST=localhost DEVFLOW_PORT=9224</c>.
/// </remarks>
public class BackgroundHttpLifecycleUiTests(NaluApp app, ChaosServerFixture chaos) : BaseUiTest(app), IClassFixture<ChaosServerFixture>
{
    private const string _pageName = "Background Http Lifecycle";

    private async Task OpenAsync()
    {
        var platform = await App.GetPlatformAsync();
        Assert.SkipUnless(platform.Contains("ios", StringComparison.OrdinalIgnoreCase), "The background NSUrlSession lifecycle harness is iOS-only.");
        Assert.SkipWhen(chaos.LanAddress is null, "This machine has no LAN IPv4 for the device to reach.");

        await App.OpenTestPageAsync(_pageName);

        var mode = await App.WaitForTextMatchAsync("LifecycleModeLabel", t => t is "device" or "simulator-bg" or "simulator-default");
        Assert.SkipWhen(mode == "simulator-default", "This simulator cannot create background NSUrlSessions.");

        await App.FillVerifiedAsync("LifecycleBaseUrl", chaos.BaseUrl);
        await App.TapAsync("LifecycleClearButton");
        chaos.Server.ClearRequests();
    }

    private async Task WaitSummaryAsync(string expected, TimeSpan timeout)
        => (await App.WaitForTextMatchAsync("LifecycleSummary", t => t == expected, timeout))
           .Should().Be(expected);

    [Fact]
    public async Task ResponsesArriveAcrossBackgrounding()
    {
        await OpenAsync();

        await App.FillVerifiedAsync("LifecycleCount", "3");
        await App.FillVerifiedAsync("LifecycleDelayMs", "6000");
        await App.TapAsync("LifecycleStartButton");

        // The server must have the requests BEFORE the app leaves the foreground.
        await chaos.Server.WaitForRequestAsync(r => r.Path.StartsWith("/delay"), TimeSpan.FromSeconds(10));

        await App.BackgroundAppAsync();
        await Task.Delay(TimeSpan.FromSeconds(9));
        await App.ForegroundAppAsync();

        await WaitSummaryAsync("done=3 ok=3 err=0 canceled=0 inflight=0", TimeSpan.FromSeconds(30));
        chaos.Server.Requests.Count(r => r.Path.StartsWith("/delay")).Should().Be(3);
    }

    [Fact]
    public async Task UploadCompletesAcrossBackgrounding()
    {
        await OpenAsync();

        await App.FillVerifiedAsync("LifecycleDelayMs", "5000");
        await App.TapAsync("LifecycleUploadButton");
        await chaos.Server.WaitForRequestAsync(r => r.Method == "POST" && r.Path.StartsWith("/echo"), TimeSpan.FromSeconds(10));

        await App.BackgroundAppAsync();
        await Task.Delay(TimeSpan.FromSeconds(8));
        await App.ForegroundAppAsync();

        await WaitSummaryAsync("done=1 ok=1 err=0 canceled=0 inflight=0", TimeSpan.FromSeconds(30));

        var request = chaos.Server.Requests.Single(r => r.Method == "POST");
        request.BodyLength.Should().BeGreaterThan(8192, "the multipart body carries an 8KB payload plus boundaries");
    }

    [Fact]
    public async Task FailFastErrorSurfacesAcrossBackgrounding()
    {
        await OpenAsync();

        // ~200ms per redirect hop → the -1007 lands roughly 4s in, while the app is backgrounded.
        await App.FillVerifiedAsync("LifecycleDelayMs", "200");
        await App.TapAsync("LifecycleFaultButton");
        await chaos.Server.WaitForRequestAsync(r => r.Path.StartsWith("/redirect-loop"), TimeSpan.FromSeconds(10));

        await App.BackgroundAppAsync();
        await Task.Delay(TimeSpan.FromSeconds(8));
        await App.ForegroundAppAsync();

        await WaitSummaryAsync("done=1 ok=0 err=1 canceled=0 inflight=0", TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task RetryingFaultKeepsRetryingWhileBackgroundedAndCancels()
    {
        await OpenAsync();

        await App.TapAsync("LifecycleRetryButton");
        await chaos.Server.WaitForRequestAsync(r => r.Path.StartsWith("/truncate"), TimeSpan.FromSeconds(10));

        await App.BackgroundAppAsync();
        await Task.Delay(TimeSpan.FromSeconds(8));
        await App.ForegroundAppAsync();

        // nsurlsessiond retried while the app was backgrounded, and the request is still pending.
        chaos.Server.Requests.Count(r => r.Path.StartsWith("/truncate")).Should().BeGreaterThan(1, "retries continue while the app is backgrounded");
        await WaitSummaryAsync("done=0 ok=0 err=0 canceled=0 inflight=1", TimeSpan.FromSeconds(10));

        await App.TapAsync("LifecycleCancelButton");
        await WaitSummaryAsync("done=1 ok=0 err=0 canceled=1 inflight=0", TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task LostResponsesAreDeliveredAfterKillAndRelaunch()
    {
        await OpenAsync();

        await App.FillVerifiedAsync("LifecycleCount", "2");
        await App.FillVerifiedAsync("LifecycleDelayMs", "8000");
        await App.TapAsync("LifecycleStartButton");
        await chaos.Server.WaitForRequestAsync(r => r.Path.StartsWith("/delay"), TimeSpan.FromSeconds(10));

        await App.KillAppAsync();

        // The server completes the responses ~8s in, while the app is dead; nsurlsessiond
        // finishes the downloads on the app's behalf.
        await Task.Delay(TimeSpan.FromSeconds(12));

        await App.RelaunchAppAsync();

        // Opening the page recreates the background session, which re-attaches to nsurlsessiond
        // and gets the completed downloads delivered as LOST responses.
        await App.OpenTestPageAsync(_pageName);

        // Per-KIND assertion: a kill can make nsurlsessiond re-deliver a previous test's
        // not-fully-acknowledged event into this relaunch (ghost deliveries), so global lost
        // counters are not stable — this test's own kind is. 32 bytes = 2 × the /delay body.
        var lost = await App.WaitForTextMatchAsync("LostByKindLabel", t => t is not null && t.Contains("batch=2/0/32"), TimeSpan.FromSeconds(60));
        lost.Should().Contain("batch=2/0/32");
        chaos.Server.Requests.Count(r => r.Path.StartsWith("/delay")).Should().Be(2);
    }

    [Fact]
    public async Task UploadIsDeliveredAfterKillAndRelaunch()
    {
        await OpenAsync();

        await App.FillVerifiedAsync("LifecycleDelayMs", "8000");
        await App.TapAsync("LifecycleUploadButton");
        await chaos.Server.WaitForRequestAsync(r => r.Method == "POST" && r.Path.StartsWith("/echo"), TimeSpan.FromSeconds(10));

        await App.KillAppAsync();
        await Task.Delay(TimeSpan.FromSeconds(12));
        await App.RelaunchAppAsync();
        await App.OpenTestPageAsync(_pageName);

        var lost = await App.WaitForTextMatchAsync("LostByKindLabel", t => t is not null && t.Contains("upload=1/0/"), TimeSpan.FromSeconds(60));
        lost.Should().Contain("upload=1/0/");

        var request = chaos.Server.Requests.Single(r => r.Method == "POST");
        request.BodyLength.Should().BeGreaterThan(8192, "the whole multipart body must have been uploaded before the kill");
    }

    [Fact]
    public async Task MidFlightDownloadSurvivesKillAndIsDeliveredOnRelaunch()
    {
        await OpenAsync();

        // 4MB dripped over ~4s: the kill lands mid-body by construction.
        await App.TapAsync("LifecycleLargeButton");
        await chaos.Server.WaitForRequestAsync(r => r.Path.StartsWith("/drip"), TimeSpan.FromSeconds(10));

        await App.KillAppAsync();
        await Task.Delay(TimeSpan.FromSeconds(8));
        await App.RelaunchAppAsync();
        await App.OpenTestPageAsync(_pageName);

        // The FULL 4MB body must have been downloaded despite the app dying mid-flight.
        var lost = await App.WaitForTextMatchAsync("LostByKindLabel", t => t is not null && t.Contains("large=1/0/4000000"), TimeSpan.FromSeconds(60));
        lost.Should().Contain("large=1/0/4000000");
    }

    [Fact]
    public async Task MidFlightUploadSurvivesKillAndIsDeliveredOnRelaunch()
    {
        await OpenAsync();

        // 8MB body against the server's throttled reader (~6.4s to drain): killing ~1.5s in is
        // guaranteed mid-upload. This DOCUMENTS whether nsurlsessiond keeps the serialized body
        // of a download-task-with-body (the library's upload vehicle, chosen because upload
        // tasks surface no response) across process death.
        await App.TapAsync("LifecycleSlowUploadButton");
        await Task.Delay(TimeSpan.FromSeconds(1.5));

        await App.KillAppAsync();

        // Server finishes draining the body and responds while the app is dead.
        await Task.Delay(TimeSpan.FromSeconds(10));

        await App.RelaunchAppAsync();
        await App.OpenTestPageAsync(_pageName);

        var lost = await App.WaitForTextMatchAsync("LostByKindLabel", t => t is not null && t.Contains("slowupload=1/0/"), TimeSpan.FromSeconds(60));
        lost.Should().Contain("slowupload=1/0/");

        // The FULL multipart body must have reached the server despite the mid-upload kill.
        var request = await chaos.Server.WaitForRequestAsync(r => r.Method == "POST" && r.Path.StartsWith("/slow-read"), TimeSpan.FromSeconds(10));
        request.BodyLength.Should().BeGreaterThan(8 * 1024 * 1024, "the whole 8MB payload plus multipart boundaries must arrive");
    }

    [Fact]
    public async Task ErrorCompletionAfterKillIsAbsorbedSilently()
    {
        await OpenAsync();

        // ~1s per hop → the -1007 fires ~21s in, long after the kill below.
        await App.FillVerifiedAsync("LifecycleDelayMs", "1000");
        await App.TapAsync("LifecycleFaultButton");
        await chaos.Server.WaitForRequestAsync(r => r.Path.StartsWith("/redirect-loop"), TimeSpan.FromSeconds(10));

        await App.KillAppAsync();

        // Let the redirect chase play out and fail while the app is dead.
        await Task.Delay(TimeSpan.FromSeconds(28));

        await App.RelaunchAppAsync();
        await App.OpenTestPageAsync(_pageName);

        // An error completion has nothing to hand over: it must be absorbed without invoking
        // the lost-message handler, without leaking a pending handle, and without crashing.
        // Per-kind check ("fault" absent) — ghost deliveries of OTHER kinds are tolerated.
        await Task.Delay(TimeSpan.FromSeconds(3));
        await App.TapAsync("LifecycleRefreshButton");
        (await App.WaitForTextMatchAsync("LostByKindLabel", t => t is not null, TimeSpan.FromSeconds(10))).Should().NotContain("fault=");
        (await App.WaitForTextMatchAsync("PendingLabel", t => t is not null, TimeSpan.FromSeconds(10))).Should().Be("pending=0");
    }
}
