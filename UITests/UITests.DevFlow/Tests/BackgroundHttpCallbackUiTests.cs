using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Runs the self-asserting CALLBACK-INJECTION suite on the "Background Http Callbacks" TestApp
/// page: the page drives <c>MessageHandlerNSUrlSessionDownloadDelegate</c>'s session callbacks
/// directly with fake tasks (unexpected states, null descriptions, non-HTTP responses, missing
/// files, throwing getters, duplicates, the lost-request flow, background-completion
/// bookkeeping) and reports one PASS/FAIL summary this test asserts on.
/// </summary>
/// <remarks>
/// The chaos server's <c>/stall</c> provides the never-answering endpoint that keeps real
/// requests pending while their outcome is synthesized. iOS-only; on a simulator whose OS
/// refuses background session configurations the page reports SKIP and the test skips with it.
/// </remarks>
public class BackgroundHttpCallbackUiTests(NaluApp app, ChaosServerFixture chaos) : BaseUiTest(app), IClassFixture<ChaosServerFixture>
{
    [Fact]
    public async Task AllCallbackScenariosPass()
    {
        var platform = await App.GetPlatformAsync();
        Assert.SkipUnless(platform.Contains("ios", StringComparison.OrdinalIgnoreCase), "The background NSUrlSession harness is iOS-only.");
        Assert.SkipWhen(chaos.LanAddress is null, "This machine has no LAN IPv4 for the device to reach.");

        await App.OpenTestPageAsync("Background Http Callbacks");
        await App.FillVerifiedAsync("CallbackStallUrl", $"{chaos.BaseUrl}/stall?ms=600000");
        await App.TapAsync("CallbackClearButton");
        await App.TapAsync("CallbackRunAllButton");

        var summary = await App.WaitForTextMatchAsync(
            "CallbackSummary",
            t => t is not null && (t.StartsWith("PASS", StringComparison.Ordinal)
                                  || t.StartsWith("FAIL", StringComparison.Ordinal)
                                  || t.StartsWith("SKIP", StringComparison.Ordinal)),
            TimeSpan.FromSeconds(150)
        );

        Assert.SkipWhen(summary!.StartsWith("SKIP", StringComparison.Ordinal), summary);

        var failures = await App.GetPropertyAsync("CallbackFailures", "Text");
        summary.Should().StartWith("PASS", $"every callback scenario must pass. Failures: {failures}");
    }
}
