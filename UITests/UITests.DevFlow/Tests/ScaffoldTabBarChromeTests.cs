using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the Scaffold's default tab bar template (§5.3) against the "Scaffold TabBar Tests"
/// harness: six roots at the default 76dp ItemWidth on a phone ⇒ four in-bar items plus the
/// trailing "More" item, Echo and Foxtrot in the overflow panel.
/// </summary>
public class ScaffoldTabBarChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold TabBar Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    /// <summary>
    /// Waits until the element is actually DISPLAYED (positioned in the window). On the
    /// Scaffold, covered root pages stay in the element tree detached (state preservation), so
    /// mere element presence never proves a page is on screen — detached views report (0,0).
    /// </summary>
    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    /// <summary>
    /// Taps a tab item and waits for its page to be displayed, retrying the tap: a selection
    /// fired while the previous navigation is still committing is silently ignored by design.
    /// </summary>
    private async Task SelectTabAsync(string tabAutomationId, string pageMarkerAutomationId)
    {
        for (var attempt = 0; ; attempt++)
        {
            await App.TapAsync(tabAutomationId);

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
    public async Task DefaultTemplateShowsFittingItemsAndMore()
    {
        await App.WaitForElementAsync("TabPageAlpha");

        (await App.WaitForElementAsync("TabAlpha")).IsVisible.Should().BeTrue();
        (await App.WaitForElementAsync("TabBravo")).IsVisible.Should().BeTrue();
        (await App.WaitForElementAsync("TabCharlie")).IsVisible.Should().BeTrue();
        (await App.WaitForElementAsync("TabDelta")).IsVisible.Should().BeTrue();
        (await App.WaitForElementAsync("TabMore")).IsVisible.Should().BeTrue();

        // Echo and Foxtrot do not fit: they live in the overflow panel, not the bar.
        var echoBounds = await App.GetBoundsAsync("TabEcho");
        echoBounds.X.Should().BeLessThan(0, "overflowed items are parked offscreen");
    }

    [Fact]
    public async Task BadgeRendersAttachedText()
    {
        await App.WaitForElementAsync("TabPageAlpha");
        await App.WaitForTextAsync("TabAlphaBadge", "11");
    }

    [Fact]
    public async Task TabSelectionSwitchesRootAndPreservesStack()
    {
        await WaitDisplayedAsync("TabPageAlpha");

        // Switch to Bravo and push a detail page onto its stack.
        await SelectTabAsync("TabBravo", "TabPageBravo");
        await App.TapAsync("PushTabDetailBravo");
        await WaitDisplayedAsync("TabDetailPage");

        // Switch away and back: Bravo's stack must be restored with the detail on top.
        await SelectTabAsync("TabAlpha", "TabPageAlpha");
        await SelectTabAsync("TabBravo", "TabDetailPage");
    }

    [Fact]
    public async Task ActiveTabTapPopsToRoot()
    {
        await WaitDisplayedAsync("TabPageAlpha");

        await App.TapAsync("PushTabDetailAlpha");
        await WaitDisplayedAsync("TabDetailPage");

        // Tapping the CURRENT tab pops its stack back to the root page.
        await SelectTabAsync("TabAlpha", "TabPageAlpha");
        await App.WaitForElementGoneAsync("TabDetailPage");
    }

    [Fact]
    public async Task EntryStateSurvivesTabSwitch()
    {
        await WaitDisplayedAsync("TabPageAlpha");
        var originalButton = await App.WaitForStableBoundsAsync("PushTabDetailAlpha");

        await App.FillAsync("AlphaStateEntry", "preserved!");
        await SelectTabAsync("TabCharlie", "TabPageCharlie");
        await SelectTabAsync("TabAlpha", "TabPageAlpha");

        await App.WaitForTextAsync("AlphaStateEntry", "preserved!");

        // Geometry, not just element-tree presence: the remounted page must land back at its
        // original horizontal position (regression: a leftover slide transform pushed the
        // platform view offscreen while element queries still saw it). Horizontal only — the
        // entry focus can leave a vertical keyboard scroll behind. Retry-until-match: the slide
        // animation is still settling when the page's elements first appear.
        await App.WaitForBoundsAsync(
            "PushTabDetailAlpha",
            b => Math.Abs(b.X - originalButton.X) <= 1 && Math.Abs(b.Width - originalButton.Width) <= 1
        );
    }

    [Fact]
    public async Task AutoVisibilityHidesBarWhilePushed()
    {
        await App.WaitForElementAsync("TabPageAlpha");
        (await App.WaitForElementAsync("TabAlpha")).IsVisible.Should().BeTrue();
        var restingBounds = await App.WaitForStableBoundsAsync("TabAlpha");

        // The pushed page opts into Auto: the bar hides (animated, in sync with the push)…
        await App.TapAsync("PushAutoDetailAlpha");
        await WaitDisplayedAsync("TabAutoDetailPage");
        await App.WaitForElementGoneAsync("TabAlpha");

        // …and shows again when the stack returns to its root — at the SAME resting frame
        // (regression: stale safe-area padding picked up while the strip was translated into
        // the Android system-bars region left the re-shown bar higher than its resting spot).
        await App.TapAsync("PopTabAutoDetail");
        await WaitDisplayedAsync("TabPageAlpha");
        (await App.WaitForElementAsync("TabAlpha")).IsVisible.Should().BeTrue();

        await App.WaitForBoundsAsync(
            "TabAlpha",
            b => Math.Abs(b.Y - restingBounds.Y) <= 1 && Math.Abs(b.Height - restingBounds.Height) <= 1
        );
    }

    [Fact]
    public async Task OverflowPanelOpensSelectsAndHighlights()
    {
        await App.WaitForElementAsync("TabPageAlpha");

        await App.TapAsync("TabMore");
        await App.WaitForElementAsync("TabBarOverflowPanel");
        (await App.WaitForElementAsync("OverflowRowEcho")).IsVisible.Should().BeTrue();
        (await App.WaitForElementAsync("OverflowRowFoxtrot")).IsVisible.Should().BeTrue();

        // Selecting an overflow root closes the panel and navigates.
        await App.TapAsync("OverflowRowEcho");
        await App.WaitForElementGoneAsync("TabBarOverflowPanel");
        await WaitDisplayedAsync("TabPageEcho");
    }

    [Fact]
    public async Task OverflowPanelTogglesOnMoreTap()
    {
        await WaitDisplayedAsync("TabPageAlpha");

        await App.TapAsync("TabMore");
        await App.WaitForElementAsync("TabBarOverflowPanel");

        // A second More tap dismisses the panel without navigating.
        await App.TapAsync("TabMore");
        await App.WaitForElementGoneAsync("TabBarOverflowPanel");
        await WaitDisplayedAsync("TabPageAlpha");
    }

    [Fact]
    public async Task OverflowPanelDismissesOnBarSelection()
    {
        await App.WaitForElementAsync("TabPageAlpha");

        await App.TapAsync("TabMore");
        await App.WaitForElementAsync("TabBarOverflowPanel");

        // The tab bar renders above the scrim: tapping an in-bar item while the panel is open
        // dismisses the panel AND performs that selection in one gesture.
        await App.TapAsync("TabBravo");
        await App.WaitForElementGoneAsync("TabBarOverflowPanel");
        await WaitDisplayedAsync("TabPageBravo");
    }

    [Fact]
    public async Task OverflowPanelDismissesOnSystemBack()
    {
        Assert.SkipUnless(!await App.IsAppleAsync(), "System back is an Android-only channel.");

        await App.WaitForElementAsync("TabPageAlpha");

        await App.TapAsync("TabMore");
        await App.WaitForElementAsync("TabBarOverflowPanel");

        // Back dismisses the overlay before the navigation engine is consulted.
        await App.SystemBackAsync();
        await App.WaitForElementGoneAsync("TabBarOverflowPanel");
        (await App.WaitForElementAsync("TabPageAlpha")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task TabBarVisibilityTogglesWithInsets()
    {
        await App.WaitForElementAsync("TabPageAlpha");
        var barBounds = await App.WaitForStableBoundsAsync("TabAlpha");
        barBounds.Width.Should().BeGreaterThan(0);

        // Hiding the bar is an inset change: the bar unmounts, the page keeps its layout.
        await App.TapAsync("ToggleTabBarAlpha");
        await App.WaitForElementGoneAsync("TabAlpha");

        await App.TapAsync("ToggleTabBarAlpha");
        (await App.WaitForStableBoundsAsync("TabAlpha")).Width.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PageContentEndsAboveTabBar()
    {
        await App.WaitForElementAsync("TabPageAlpha");
        var barBounds = await App.WaitForStableBoundsAsync("TabAlpha");

        // Scroll to the very end: the §5.4 inset contribution must leave the bottom probe
        // fully visible ABOVE the floating bar, not underneath it. (The library's
        // ScaffoldScrollToFix makes the programmatic scroll inset-aware on iOS — without it
        // MAUI's ScrollToAsync clamp under-scrolls by the safe-area amount.)
        await App.ScrollAsync("AlphaScroll", deltaY: 5000);

        var finalProbe = await App.WaitForStableBoundsAsync("BottomProbeAlpha");
        (finalProbe.Y + finalProbe.Height).Should().BeLessThanOrEqualTo(barBounds.Y + 1, "the bottom probe must settle above the tab bar");
    }
}
