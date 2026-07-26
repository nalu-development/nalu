using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the §5.5 flyout completion against the "Scaffold Flyout Tests" harness: the default
/// <see cref="Nalu.ScaffoldFlyoutMenuView"/> composition rules, engine-routed selection,
/// Auto/page-level modes, the "stack of flyouts" resolution, the width option, open-state
/// events and the page-scope controller.
/// </summary>
public class ScaffoldFlyoutChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Flyout Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    /// <summary>
    /// Scaffold-hosted views never leave the element tree (state preservation) — "displayed"
    /// means positioned window bounds, presence proves nothing.
    /// </summary>
    private async Task<bool> IsDisplayedAsync(string automationId)
        => await App.FindElementAsync(automationId) is { } element
           && (element.WindowBounds ?? element.Bounds) is { Y: > 0 };

    /// <summary>
    /// Taps a drawer menu entry and waits for its page, retrying: the scaffold-wide selection
    /// gate makes taps racing a still-settling navigation honest no-ops
    /// (<c>SelectCommand.CanExecute</c> false), so tests retry exactly like tab taps.
    /// </summary>
    private async Task SelectMenuItemAsync(string itemAutomationId, string pageMarkerAutomationId)
    {
        for (var attempt = 0; ; attempt++)
        {
            await App.TapAsync(itemAutomationId);

            try
            {
                await App.WaitForBoundsAsync(pageMarkerAutomationId, b => b.Y > 0, TimeSpan.FromSeconds(2));

                return;
            }
            catch (TimeoutException) when (attempt < 3)
            {
            }
        }
    }

    [Fact]
    public async Task DefaultMenuComposesPerRules()
    {
        await WaitDisplayedAsync("FlyoutHomePage");
        await App.TapAsync("OpenStartFlyoutButton");

        // Flat entry for the single-root area; group header + entries for the multi-root one.
        await WaitDisplayedAsync("FlyoutItemFlyoutHome");
        (await IsDisplayedAsync("StartFlyoutHeader")).Should().BeTrue("the HeaderView renders above the menu");
        (await IsDisplayedAsync("FlyoutGroupZone")).Should().BeTrue("a multi-root area shows its title as group header");
        (await IsDisplayedAsync("FlyoutItemAlpha")).Should().BeTrue();
        (await IsDisplayedAsync("FlyoutItemBeta")).Should().BeTrue();

        // Hidden roots and tab-bar areas are excluded (they are not even in the menu's tree).
        (await App.FindElementAsync("FlyoutItemGhost")).Should().BeNull("non-visible roots are omitted");
        (await App.FindElementAsync("FlyoutItemTabOne")).Should().BeNull("tab-bar areas are excluded by default");
        (await App.FindElementAsync("FlyoutGroupTabs")).Should().BeNull("tab-bar areas are excluded by default");

        // The explicit Width option (300) drives the panel width.
        var menu = await App.WaitForStableBoundsAsync("StartFlyoutMenu");
        menu.Width.Should().BeApproximately(300, 2, "FlyoutStartOptions.Width is 300");

        await App.TapAsync("CloseStartFlyoutButton");
        await App.WaitForTextAsync("FlyoutStateLabel", "start-closed:False");
    }

    [Fact]
    public async Task MenuSelectionRoutesThroughEngineAndCloses()
    {
        await WaitDisplayedAsync("FlyoutHomePage");
        await App.TapAsync("OpenStartFlyoutButton");
        await WaitDisplayedAsync("FlyoutItemAlpha");

        // Selection navigates to the root (cross-area) and the navigation closes the drawer.
        await SelectMenuItemAsync("FlyoutItemAlpha", "FlyoutAlphaPage");

        // Back through the menu (the drawer button is available at the Alpha stack root).
        await App.TapAsync("NavBarFlyoutStartButton");
        await WaitDisplayedAsync("FlyoutItemFlyoutHome");
        await SelectMenuItemAsync("FlyoutItemFlyoutHome", "FlyoutHomePage");
        await App.WaitForTextAsync("FlyoutStateLabel", "start-closed:False");
    }

    [Fact]
    public async Task AutoModeDisablesDrawerOnPushedPages()
    {
        await WaitDisplayedAsync("FlyoutHomePage");

        // Auto mode shows the drawer button at stack roots (chrome settles async: wait-based).
        await WaitDisplayedAsync("NavBarFlyoutStartButton");

        await App.TapAsync("PushFlyoutDetail");
        await WaitDisplayedAsync("FlyoutDetailPage");

        // The drawer button hides and programmatic opens no-op (the menu stays undisplayed).
        (await App.WaitForElementAsync("NavBarFlyoutStartButton")).IsVisible.Should().BeFalse("Auto mode hides the drawer on pushed pages");
        await App.TapAsync("OpenStartFromDetailButton");
        (await IsDisplayedAsync("FlyoutItemFlyoutHome")).Should().BeFalse("OpenFlyoutAsync must no-op while the drawer is unavailable");

        await App.TapAsync("PopFlyoutDetail");
        await WaitDisplayedAsync("FlyoutHomePage");

        // Back at the root the drawer returns (chrome settles async: wait-based).
        await WaitDisplayedAsync("NavBarFlyoutStartButton");
    }

    [Fact]
    public async Task PageLevelEndModeAndOpenStateEvents()
    {
        await WaitDisplayedAsync("FlyoutHomePage");

        // The END drawer is enabled by the page-level mode+content on FlyoutHomePage only,
        // and its content inherits the page's BindingContext through logical parenting.
        await App.TapAsync("OpenEndFlyoutButton");
        await WaitDisplayedAsync("EndFlyoutLabel");
        await App.WaitForTextAsync("EndFlyoutBoundLabel", "HomeModel");
        await App.WaitForTextAsync("FlyoutStateLabel", "end-open:True");

        await App.TapAsync("CloseEndFlyoutButton");
        await App.WaitForTextAsync("FlyoutStateLabel", "end-closed:False");

        // Start events ride the same per-side channel.
        await App.TapAsync("OpenStartFlyoutButton");
        await App.WaitForTextAsync("FlyoutStateLabel", "start-open:True");
        await App.TapAsync("CloseStartFlyoutButton");
        await App.WaitForTextAsync("FlyoutStateLabel", "start-closed:False");
    }

    [Fact]
    public async Task StackOfFlyoutsKeepsPageDrawerAcrossPushes()
    {
        await WaitDisplayedAsync("FlyoutHomePage");

        // The home page carries the END drawer (content + mode). Pushing a page that does NOT
        // override it must keep the home page's drawer available and intact.
        await App.TapAsync("PushFlyoutDetail");
        await WaitDisplayedAsync("FlyoutDetailPage");

        await App.TapAsync("OpenEndFromDetailButton");
        await WaitDisplayedAsync("EndFlyoutLabel");
        await App.WaitForTextAsync("EndFlyoutBoundLabel", "HomeModel");

        await App.TapAsync("CloseEndFlyoutButton");
        await App.WaitForTextAsync("FlyoutStateLabel", "end-closed:False");

        // Popping restores the previous level unchanged: the drawer still opens at home.
        await App.TapAsync("PopFlyoutDetail");
        await WaitDisplayedAsync("FlyoutHomePage");
        await App.TapAsync("OpenEndFlyoutButton");
        await WaitDisplayedAsync("EndFlyoutLabel");
        await App.TapAsync("CloseEndFlyoutButton");
        await App.WaitForTextAsync("FlyoutStateLabel", "end-closed:False");
    }
}
