using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers VIEW-ONLY navigation (no page models) against the "Scaffold View Only Tests" harness:
/// pages registered with the model-less <c>AddPage&lt;TPage&gt;()</c> overload are the lifecycle
/// targets themselves — entering/appearing/disappearing counters, typed intents, a page-level
/// <c>ILeavingGuard</c> honored on every leave path, and tab-stack preservation.
/// </summary>
public class ScaffoldViewOnlyChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold View Only Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(_pageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    /// <summary>
    /// Waits until the element is actually DISPLAYED (positioned in the window): covered root
    /// pages stay in the tree detached (state preservation), so presence alone proves nothing.
    /// </summary>
    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    /// <summary>
    /// Taps a tab item and waits for its page to display, retrying: a selection fired while the
    /// previous navigation is still committing is silently ignored by design.
    /// </summary>
    private async Task SelectTabAsync(string tabAutomationId, string pageMarkerAutomationId)
    {
        for (var attempt = 0; ; attempt++)
        {
            await App.TapAsync(tabAutomationId);

            try
            {
                // Settled display, not a single sample: the page being switched AWAY from is
                // still on screen for the length of its motion, so an ignored selection would
                // otherwise read as a successful one and never be retried.
                await App.WaitForSettledDisplayAsync(pageMarkerAutomationId, TimeSpan.FromSeconds(2));

                return;
            }
            catch (TimeoutException) when (attempt < 3)
            {
            }
        }
    }

    [Fact]
    public async Task LifecycleEventsTargetThePageItself()
    {
        await WaitDisplayedAsync("ViewOnlyOnePage");

        // Entering + appearing dispatched to the PAGE (no BindingContext anywhere).
        await App.WaitForTextAsync("ViewOnlyOneLifecycle", "E1 A1 D0");
    }

    [Fact]
    public async Task PushDeliversTypedIntentToThePageAndPopReappearsTheRoot()
    {
        await WaitDisplayedAsync("ViewOnlyOnePage");
        await App.WaitForTextAsync("ViewOnlyOneLifecycle", "E1 A1 D0");

        await App.TapAsync("PushViewOnlyDetail");
        await WaitDisplayedAsync("ViewOnlyDetailPage");

        // IEnteringAware<int> implemented on the pushed page received the intent.
        await App.WaitForTextAsync("ViewOnlyDetailIntent", "42");

        // The covered root got its disappearing (its label is readable while detached).
        await App.WaitForTextAsync("ViewOnlyOneLifecycle", "E1 A1 D1");

        await App.TapAsync("PopViewOnlyDetail");
        await WaitDisplayedAsync("ViewOnlyOnePage");
        await App.WaitForTextAsync("ViewOnlyOneLifecycle", "E1 A2 D1");
    }

    [Fact]
    public async Task PageLevelGuardBlocksPopUntilAllowed()
    {
        await WaitDisplayedAsync("ViewOnlyOnePage");
        await App.TapAsync("PushViewOnlyGuard");
        await WaitDisplayedAsync("ViewOnlyGuardPage");

        // DENY mode: the pop consults the page's guard and stays.
        await App.TapAsync("PopViewOnlyGuard");
        await App.WaitForTextAsync("ViewOnlyGuardChecks", "1");
        (await App.GetBoundsAsync("ViewOnlyGuardPage")).Y.Should().BeGreaterThan(0, "the guard denied the pop");

        // ALLOW mode: the guard runs again and the pop goes through. (No counter assertion
        // after the pop: the popped page is disposed and its labels leave the tree.)
        await App.TapAsync("ViewOnlyGuardToggle");
        await App.WaitForTextAsync("ViewOnlyGuardAllow", "allow");
        await App.TapAsync("PopViewOnlyGuard");
        await WaitDisplayedAsync("ViewOnlyOnePage");
    }

    [Fact]
    public async Task AndroidSystemBackConsultsThePageGuard()
    {
        var platform = await App.GetPlatformAsync();

        if (!platform.Contains("android", StringComparison.OrdinalIgnoreCase))
        {
            // The agent's Back command refuses on custom hosts and iOS has no system back
            // button: the real-back-channel guard coverage is Android-only.
            return;
        }

        await WaitDisplayedAsync("ViewOnlyOnePage");
        await App.TapAsync("PushViewOnlyGuard");
        await WaitDisplayedAsync("ViewOnlyGuardPage");

        // System back routes through the OnBackPressedDispatcher into the engine → page guard.
        await App.SystemBackAsync();
        await App.WaitForTextAsync("ViewOnlyGuardChecks", "1");
        (await App.GetBoundsAsync("ViewOnlyGuardPage")).Y.Should().BeGreaterThan(0, "the guard denied the system back");

        await App.TapAsync("ViewOnlyGuardToggle");
        await App.SystemBackAsync();
        await WaitDisplayedAsync("ViewOnlyOnePage");
    }

    [Fact]
    public async Task TabSwitchPreservesTheViewOnlyStack()
    {
        await WaitDisplayedAsync("ViewOnlyOnePage");
        await App.TapAsync("PushViewOnlyDetail");
        await WaitDisplayedAsync("ViewOnlyDetailPage");

        await SelectTabAsync("TabTwo", "ViewOnlyTwoPage");

        // Back to One: the preserved stack re-presents the pushed detail, not the root.
        await SelectTabAsync("TabOne", "ViewOnlyDetailPage");
        await App.WaitForTextAsync("ViewOnlyDetailIntent", "42");

        await App.TapAsync("PopViewOnlyDetail");
        await WaitDisplayedAsync("ViewOnlyOnePage");
    }
}
