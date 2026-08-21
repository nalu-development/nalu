using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers MAUI's <c>Page.OnAppearing</c> / <c>OnDisappearing</c> on SCAFFOLD-HOSTED pages, against
/// the "Scaffold Page Lifecycle Tests" harness.
/// </summary>
/// <remarks>
/// These are the events every MAUI page (and a good deal of third-party code) assumes fire when a
/// page becomes visible or is covered. The harness records them into one order-preserving log
/// rendered by every page — a popped page is disposed and leaves the tree, so its own label could
/// not be read afterwards.
/// </remarks>
public class ScaffoldPageLifecycleTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Page Lifecycle Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(_pageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    /// <summary>Waits for the shared log, read from whichever page is currently displayed.</summary>
    private Task WaitForLogAsync(string page, string expected)
        => App.WaitForTextAsync($"LifecycleLog{page}", expected);

    /// <summary>
    /// Taps a tab and waits for the LOG to reach <paramref name="expectedLog" />, retrying the tap.
    /// </summary>
    /// <remarks>
    /// The log is the only trustworthy witness here. A selection fired while the previous
    /// navigation is still committing is silently ignored by design, and the appearance events are
    /// raised when the transition STARTS — so a test that waits on the log and immediately taps
    /// the next tab races the commit. Bounds are no better: a just-covered root can still report a
    /// positive origin, which would make the wait succeed without anything having happened.
    /// Re-tapping is safe: re-selecting the current root with an empty stack is a no-op.
    /// </remarks>
    private async Task SelectTabAndWaitForLogAsync(string tabAutomationId, string witnessPage, string expectedLog)
    {
        for (var attempt = 0; ; attempt++)
        {
            await App.TapAsync(tabAutomationId);

            try
            {
                await App.WaitForTextAsync($"LifecycleLog{witnessPage}", expectedLog, TimeSpan.FromSeconds(3));

                return;
            }
            catch (TimeoutException) when (attempt < 3)
            {
            }
        }
    }

    [Fact]
    public async Task RootPageAppearsOnInitialDisplay()
    {
        await App.WaitForElementAsync("LifecyclePageOne");
        await WaitForLogAsync("One", "One+");
    }

    [Fact]
    public async Task PushDisappearsTheCoveredPageAndAppearsThePushedOne()
    {
        await WaitForLogAsync("One", "One+");

        await App.TapAsync("LifecyclePushDetail");

        // The covered page disappears BEFORE the pushed one appears, matching MAUI's order.
        await WaitForLogAsync("Detail", "One+ One- Detail+");
    }

    [Fact]
    public async Task PopDisappearsThePoppedPageAndReappearsTheRevealedOne()
    {
        await WaitForLogAsync("One", "One+");
        await App.TapAsync("LifecyclePushDetail");
        await WaitForLogAsync("Detail", "One+ One- Detail+");

        await App.TapAsync("LifecyclePopDetail");

        await WaitForLogAsync("One", "One+ One- Detail+ Detail- One+");
    }

    [Fact]
    public async Task TabSwitchDisappearsTheOutgoingRootAndAppearsTheIncomingOne()
    {
        await WaitForLogAsync("One", "One+");

        await SelectTabAndWaitForLogAsync("TabTwo", "Two", "One+ One- Two+");
        await SelectTabAndWaitForLogAsync("TabOne", "One", "One+ One- Two+ Two- One+");
    }

    [Fact]
    public async Task AppearingIsNotRaisedTwiceForTheSamePresentation()
    {
        await WaitForLogAsync("One", "One+");

        // A settled presentation must not keep firing: give the layout a moment and re-read.
        await Task.Delay(1000, TestContext.Current.CancellationToken);

        (await App.FindElementAsync("LifecycleLogOne"))!.Text.Should().Be("One+");
    }
}
