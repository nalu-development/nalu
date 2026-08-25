using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers COMPONENT-BASED navigation (MauiReactor, no pages, no page models) against the
/// "Scaffold Reactor Tests" harness: components registered with <c>AddPage&lt;TComponent&gt;()</c>
/// are rendered into native pages by the app's <c>IComponentPageFactory</c> bridge and are the
/// lifecycle targets themselves — entering/appearing/disappearing counters re-rendered from
/// component STATE, typed intents, a component-level <c>ILeavingGuard</c> honored on every leave
/// path, and tab-stack preservation.
/// </summary>
public class ScaffoldReactorChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Reactor Tests";

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
                await App.WaitForSettledDisplayAsync(pageMarkerAutomationId, TimeSpan.FromSeconds(2));

                return;
            }
            catch (TimeoutException) when (attempt < 3)
            {
            }
        }
    }

    [Fact]
    public async Task LifecycleEventsTargetTheComponent()
    {
        await WaitDisplayedAsync("ReactorOnePage");

        // Entering + appearing dispatched to the COMPONENT (no BindingContext anywhere), each
        // bump a SetState re-render into the same native page.
        await App.WaitForTextAsync("ReactorOneLifecycle", "E1 A1 D0");
    }

    [Fact]
    public async Task PushDeliversTypedIntentToTheComponentAndPopReappearsTheRoot()
    {
        await WaitDisplayedAsync("ReactorOnePage");
        await App.WaitForTextAsync("ReactorOneLifecycle", "E1 A1 D0");

        await App.TapAsync("PushReactorDetail");
        await WaitDisplayedAsync("ReactorDetailPage");

        // IEnteringAware<int> implemented on the pushed component received the intent.
        await App.WaitForTextAsync("ReactorDetailIntent", "42");

        // The covered root got its disappearing (its label is readable while detached).
        await App.WaitForTextAsync("ReactorOneLifecycle", "E1 A1 D1");

        await App.TapAsync("PopReactorDetail");
        await WaitDisplayedAsync("ReactorOnePage");
        await App.WaitForTextAsync("ReactorOneLifecycle", "E1 A2 D1");
    }

    [Fact]
    public async Task ComponentLevelGuardBlocksPopUntilAllowed()
    {
        await WaitDisplayedAsync("ReactorOnePage");
        await App.TapAsync("PushReactorGuard");
        await WaitDisplayedAsync("ReactorGuardPage");

        // DENY mode: the pop consults the component's guard and stays.
        await App.TapAsync("PopReactorGuard");
        await App.WaitForTextAsync("ReactorGuardChecks", "1");
        (await App.GetBoundsAsync("ReactorGuardPage")).Y.Should().BeGreaterThan(0, "the guard denied the pop");

        // ALLOW mode: the guard runs again and the pop goes through. (No counter assertion
        // after the pop: the popped component is unmounted and its labels leave the tree.)
        await App.TapAsync("ReactorGuardToggle");
        await App.WaitForTextAsync("ReactorGuardAllow", "allow");
        await App.TapAsync("PopReactorGuard");
        await WaitDisplayedAsync("ReactorOnePage");
    }

    [Fact]
    public async Task AndroidSystemBackConsultsTheComponentGuard()
    {
        var platform = await App.GetPlatformAsync();

        if (!platform.Contains("android", StringComparison.OrdinalIgnoreCase))
        {
            // The agent's Back command refuses on custom hosts and iOS has no system back
            // button: the real-back-channel guard coverage is Android-only.
            return;
        }

        await WaitDisplayedAsync("ReactorOnePage");
        await App.TapAsync("PushReactorGuard");
        await WaitDisplayedAsync("ReactorGuardPage");

        // System back routes through the OnBackPressedDispatcher into the engine → component guard.
        await App.SystemBackAsync();
        await App.WaitForTextAsync("ReactorGuardChecks", "1");
        (await App.GetBoundsAsync("ReactorGuardPage")).Y.Should().BeGreaterThan(0, "the guard denied the system back");

        await App.TapAsync("ReactorGuardToggle");
        await App.SystemBackAsync();
        await WaitDisplayedAsync("ReactorOnePage");
    }

    [Fact]
    public async Task TabSwitchPreservesTheComponentStack()
    {
        await WaitDisplayedAsync("ReactorOnePage");
        await App.TapAsync("PushReactorDetail");
        await WaitDisplayedAsync("ReactorDetailPage");

        await SelectTabAsync("TabTwo", "ReactorTwoPage");

        // Back to One: the preserved stack re-presents the pushed detail, not the root — and the
        // detail component kept its STATE (the intent survived the tab roundtrip).
        await SelectTabAsync("TabOne", "ReactorDetailPage");
        await App.WaitForTextAsync("ReactorDetailIntent", "42");

        await App.TapAsync("PopReactorDetail");
        await WaitDisplayedAsync("ReactorOnePage");
    }
}
