using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Network-fault matrix for <c>NSUrlBackgroundSessionHttpMessageHandler</c>: drives the
/// "Background Http Chaos" TestApp page against the in-process <c>ChaosServer</c> (reached by the
/// device over the LAN) and asserts the handler surfaces each wire-level fault as a clean
/// HttpClient outcome — success, HttpRequestException, or cancellation — never a hang or crash.
/// </summary>
/// <remarks>
/// iOS-only (background NSUrlSession), and meaningful on a REAL DEVICE (`ChaosModeLabel` =
/// "device"); a simulator whose OS accepts background session configurations runs the same
/// pipeline ("simulator-bg"), otherwise the suite skips. The device and this Mac must share a
/// Wi-Fi network. Run with <c>DEVFLOW_HOST=&lt;device-ip&gt; DEVFLOW_PORT=9224</c> for a device.
/// </remarks>
public class BackgroundHttpChaosUiTests(NaluApp app, ChaosServerFixture chaos) : BaseUiTest(app), IClassFixture<ChaosServerFixture>
{
    private const string _pageName = "Background Http Chaos";

    private async Task OpenAsync()
    {
        var platform = await App.GetPlatformAsync();
        Assert.SkipUnless(platform.Contains("ios", StringComparison.OrdinalIgnoreCase), "The background NSUrlSession harness is iOS-only.");
        Assert.SkipWhen(chaos.LanAddress is null, "This machine has no LAN IPv4 for the device to reach.");

        await App.OpenTestPageAsync(_pageName);

        var mode = await App.WaitForTextMatchAsync("ChaosModeLabel", t => t is "device" or "simulator-bg" or "simulator-default");
        Assert.SkipWhen(mode == "simulator-default", "This simulator cannot create background NSUrlSessions.");

        await App.FillVerifiedAsync("ChaosBaseUrl", chaos.BaseUrl);
        chaos.Server.ClearRequests();
    }

    /// <summary>Sends one request through the page and returns the settled ChaosLastResult.</summary>
    private async Task<string> SendAsync(string path, string timeoutMs = "0", string button = "ChaosGetButton", TimeSpan? wait = null)
    {
        await App.FillVerifiedAsync("ChaosPath", path);
        await App.FillVerifiedAsync("ChaosTimeoutMs", timeoutMs);
        await App.TapAsync("ChaosClearButton");
        await App.TapAsync(button);

        // Default wait 60s: success-path requests carry NO managed timeout, and a transient
        // device Wi-Fi stall makes the background session silently retry — observed once on
        // the physical iPhone as a gzip download still "pending" at 30s that then delivered.
        var result = await App.WaitForTextMatchAsync(
            "ChaosLastResult",
            t => t is not null && t != "idle" && t != "pending",
            wait ?? TimeSpan.FromSeconds(60)
        );

        return result!;
    }

    [Fact]
    public async Task PlainSuccess()
    {
        await OpenAsync();

        var result = await SendAsync("/ok");

        result.Should().Be("OK 200 len=11");
        chaos.Server.Requests.Should().Contain(r => r.Method == "GET" && r.Path.StartsWith("/ok"));
    }

    [Fact]
    public async Task HttpErrorStatusIsAResponseNotAnException()
    {
        await OpenAsync();

        var result = await SendAsync("/status/503");

        result.Should().Be("OK 503 len=14", "an HTTP-level failure must surface as a status code, not an exception");
    }

    [Fact]
    public async Task MultipartUploadRoundTrips()
    {
        await OpenAsync();

        var result = await SendAsync("/echo", button: "ChaosPostButton");

        result.Should().StartWith("OK 200");

        var body = await App.GetPropertyAsync("ChaosLastBody", "Text");
        body.Should().Contain("\"method\":\"POST\"");

        var request = await chaos.Server.WaitForRequestAsync(r => r.Method == "POST" && r.Path.StartsWith("/echo"), TimeSpan.FromSeconds(5));
        request.BodyLength.Should().BeGreaterThan(8192, "the multipart body carries an 8KB payload plus boundaries");
    }

    // BACKGROUND-SESSION SEMANTICS, verified with this harness: connection-level faults do NOT
    // fail the task — nsurlsessiond RETRIES silently until the session's resource timeout (24h
    // here, so that HttpClient.Timeout governs; see the config in
    // MessageHandlerNSUrlSessionDownloadDelegate). The invariant these tests encode is therefore
    // "never hangs past the managed timeout, never succeeds": the request must end CANCELED (the
    // managed timeout cancelling the native task) or ERR (iOS gave up early) — plus, where the
    // server can see it, proof that the fault path was really exercised (retry hits).
    private static void ShouldBeCanceledOrFaulted(string result)
        => (result == "CANCELED" || result.StartsWith("ERR", StringComparison.Ordinal))
           .Should().BeTrue($"a faulted request must be cancellable and must never succeed (was: '{result}')");

    [Fact]
    public async Task TruncatedBodyRetriesUntilCanceled()
    {
        await OpenAsync();

        // 15s managed timeout: device Wi-Fi can take seconds to wake, and the fault+retry
        // evidence needs a window AFTER the first packet actually lands.
        var result = await SendAsync("/truncate?declared=100000&send=1000", timeoutMs: "15000", wait: TimeSpan.FromSeconds(40));

        ShouldBeCanceledOrFaulted(result);
        chaos.Server.Requests.Count(r => r.Path.StartsWith("/truncate")).Should().BeGreaterThan(1, "the background session retries a truncated download");
    }

    [Fact]
    public async Task ConnectionResetRetriesUntilCanceled()
    {
        await OpenAsync();

        var result = await SendAsync("/reset", timeoutMs: "15000", wait: TimeSpan.FromSeconds(40));

        ShouldBeCanceledOrFaulted(result);
        chaos.Server.Requests.Count(r => r.Path == "/reset").Should().BeGreaterThan(1, "the background session retries after an RST");
    }

    [Fact]
    public async Task ConnectionResetMidBodyIsCanceledOrFaulted()
    {
        await OpenAsync();

        var result = await SendAsync("/reset?after=500", timeoutMs: "15000", wait: TimeSpan.FromSeconds(40));

        ShouldBeCanceledOrFaulted(result);
        await chaos.Server.WaitForRequestAsync(r => r.Path == "/reset?after=500", TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task GarbageIsDeliveredAsHttp09OrCanceled()
    {
        await OpenAsync();

        // Real NSUrlSession behavior discovered by this harness: unparseable bytes are accepted
        // as an HTTP/0.9 simple response — a synthesized 200 whose body is the raw garbage —
        // though sometimes only after a few retries (which the timeout then cancels).
        var result = await SendAsync("/garbage", timeoutMs: "15000", wait: TimeSpan.FromSeconds(40));

        (result.StartsWith("OK 200", StringComparison.Ordinal) || result == "CANCELED")
            .Should().BeTrue($"garbage must surface as an HTTP/0.9 body or be canceled (was: '{result}')");

        if (result.StartsWith("OK 200", StringComparison.Ordinal))
        {
            (await App.GetPropertyAsync("ChaosLastBody", "Text")).Should().Contain("NOT HTTP", "the HTTP/0.9 body is the raw bytes the server sent");
        }

        chaos.Server.Requests.Should().Contain(r => r.Path == "/garbage");
    }

    [Fact]
    public async Task StalledServerHonorsManagedTimeout()
    {
        await OpenAsync();

        var result = await SendAsync("/stall?ms=600000", timeoutMs: "3000");

        result.Should().Be("CANCELED", "the HttpClient-side cancellation must cancel the native task");
    }

    [Fact]
    public async Task TlsAgainstPlainHttpIsCanceledOrFaulted()
    {
        await OpenAsync();

        var result = await SendAsync($"https://{chaos.LanAddress}:{chaos.Server.Port}/ok", timeoutMs: "8000", wait: TimeSpan.FromSeconds(30));

        ShouldBeCanceledOrFaulted(result);
    }

    [Fact]
    public async Task DnsFailureIsCanceledOrFaulted()
    {
        await OpenAsync();

        var result = await SendAsync("https://chaos-nonexistent-host-nalu.invalid/ok", timeoutMs: "8000", wait: TimeSpan.FromSeconds(30));

        ShouldBeCanceledOrFaulted(result);
    }

    [Fact]
    public async Task ConnectionRefusedIsCanceledOrFaulted()
    {
        await OpenAsync();

        // Port 1 on the Mac: nothing listens there, the SYN is refused outright.
        var result = await SendAsync($"http://{chaos.LanAddress}:1/ok", timeoutMs: "8000", wait: TimeSpan.FromSeconds(30));

        ShouldBeCanceledOrFaulted(result);
    }

    [Fact]
    public async Task RedirectChainIsFollowed()
    {
        await OpenAsync();

        var result = await SendAsync("/redirect?n=3");

        result.Should().StartWith("OK 200");
        chaos.Server.Requests.Count(r => r.Path.StartsWith("/redirect")).Should().Be(4, "3 hops plus the landing request");
    }

    [Fact]
    public async Task RedirectLoopFailsFast()
    {
        await OpenAsync();

        // One of the few faults that DOES fail fast on a background session: iOS gives up
        // after ~21 hops with NSURLErrorHTTPTooManyRedirects.
        var result = await SendAsync("/redirect-loop", wait: TimeSpan.FromSeconds(90));

        result.Should().StartWith("ERR", "NSUrlSession must give up on a redirect loop");
        result.Should().Contain("-1007", "the NSError code must be preserved in the surfaced message");
        result.Should().Contain("[HttpProtocolError]", "the NSError must map to HttpRequestException.HttpRequestError");
        chaos.Server.Requests.Count(r => r.Path == "/redirect-loop").Should().BeGreaterThan(10, "the loop is followed up to the redirect limit before failing");
    }

    [Fact]
    public async Task DrippedBodyCompletes()
    {
        await OpenAsync();

        var result = await SendAsync("/drip?bytes=200&delayms=50&chunk=10");

        result.Should().Be("OK 200 len=200");
    }

    [Fact]
    public async Task LargeDownloadCompletes()
    {
        await OpenAsync();

        var result = await SendAsync("/huge?mb=20", wait: TimeSpan.FromSeconds(120));

        result.Should().Be("OK 200 len=20971520");
    }

    [Fact]
    public async Task CookiesRoundTripThroughTheContainer()
    {
        await OpenAsync();

        (await SendAsync("/cookies")).Should().StartWith("OK 200");

        // The second request must carry the cookies the first one set.
        (await SendAsync("/cookies")).Should().StartWith("OK 200");

        var body = await App.GetPropertyAsync("ChaosLastBody", "Text");
        body.Should().Contain("chaos1=alpha").And.Contain("chaos2=beta");
    }

    [Fact]
    public async Task ChunkedResponseCompletes()
    {
        await OpenAsync();

        // No Content-Length anywhere: the unknown-length download path.
        var result = await SendAsync("/chunked?bytes=1000");

        result.Should().Be("OK 200 len=1000");
    }

    [Fact]
    public async Task GzipResponseIsAutomaticallyDecompressed()
    {
        await OpenAsync();

        // Verifies the handler's SupportsAutomaticDecompression claim: the wire carries fewer
        // bytes than 1000, the app must read the DECODED body.
        var result = await SendAsync("/gzip?bytes=1000");

        result.Should().Be("OK 200 len=1000");
    }

    [Fact]
    public async Task DefaultTimeoutIsEnforcedManagedSide()
    {
        await OpenAsync();

        // iOS ignores the per-request NATIVE timeout on background sessions (probed on device
        // and simulator: it never fired), so the handler enforces DefaultTimeout managed-side.
        // The page button sends with a 3s DefaultTimeout against a stalled server, with a 20s
        // managed cancel as safety net: TIMEOUT well before the safety proves enforcement.
        await App.FillVerifiedAsync("ChaosPath", "/stall?ms=600000");
        await App.FillVerifiedAsync("ChaosTimeoutMs", "20000");
        await App.TapAsync("ChaosClearButton");

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await App.TapAsync("ChaosNativeTimeoutButton");

        var result = await App.WaitForTextMatchAsync(
            "ChaosLastResult",
            t => t is not null && t != "idle" && t != "pending",
            TimeSpan.FromSeconds(40)
        );

        result.Should().Be("TIMEOUT", "DefaultTimeout must surface as TaskCanceledException wrapping TimeoutException");
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15), "the 3s DefaultTimeout, not the 20s safety cancel, must settle the request");
    }

    [Fact]
    public async Task ParallelRequestsAllComplete()
    {
        await OpenAsync();

        await App.FillVerifiedAsync("ChaosPath", "/delay?ms=1000");
        await App.FillVerifiedAsync("ChaosTimeoutMs", "0");
        await App.TapAsync("ChaosClearButton");
        await App.TapAsync("ChaosParallelButton");

        var summary = await App.WaitForTextMatchAsync(
            "ChaosSummaryLabel",
            t => t is not null && t.StartsWith("done=", StringComparison.Ordinal),
            TimeSpan.FromSeconds(60)
        );

        summary.Should().Be("done=8 ok=8 err=0 canceled=0");
    }
}
