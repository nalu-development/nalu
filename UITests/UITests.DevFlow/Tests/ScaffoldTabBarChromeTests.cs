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
    private const string _pageName = "Scaffold TabBar Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(_pageName);

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

    [Fact(DisplayName = "An in-app theme change repaints the nav bar surface")]
    public async Task InAppThemeChangeRepaintsTheNavBarSurface()
    {
        await WaitDisplayedAsync("TabPageAlpha");
        await SelectTabAsync("TabBravo", "TabPageBravo");

        try
        {
            // The harness styles the bar through an implicit style on a DERIVED scaffold — the
            // shape a real app uses, and the one that silently applies to nothing when the
            // ApplyToDerivedTypes opt-in is missing. A bar stuck on the library's fixed light
            // default fails the very first sample.
            await App.WaitForPixelColorAsync("NavBarSurface", 10, 10, c => IsClose(c, 0xE8, 0xF0, 0xFE));

            await App.TapAsync("ToggleThemeBravo");
            await App.WaitForPixelColorAsync("NavBarSurface", 10, 10, c => IsClose(c, 0x10, 0x18, 0x27));

            // And back: both directions must repaint live.
            await App.TapAsync("ToggleThemeBravo");
            await App.WaitForPixelColorAsync("NavBarSurface", 10, 10, c => IsClose(c, 0xE8, 0xF0, 0xFE));
        }
        finally
        {
            // Never leak a forced theme into the next test.
            await App.TapAsync("ResetThemeBravo");
        }
    }

    [Fact(Skip = "Upstream MAUI Android bug: a Border nested inside a shadowed Border (the bar pill) never repaints "
                 + "when its Background brush mutates in place, which is what AppThemeBinding does on UserAppTheme changes. "
                 + "Re-enable when https://github.com/dotnet/maui/issues/37289 is fixed and the MAUI version is bumped.")]
    public async Task InAppThemeChangeRepaintsSelectionPill()
    {
        await WaitDisplayedAsync("TabPageAlpha");
        await SelectTabAsync("TabBravo", "TabPageBravo");

        try
        {
            // Baseline: the selected pill paints the harness style's LIGHT AppThemeBinding
            // value (the sample point sits inside the pill, left of the centered icon).
            await App.WaitForPixelColorAsync("TabBravo", 10, 30, c => IsClose(c, 0xE4, 0xEB, 0xFD));

            // App-scope theme change (UserAppTheme): the shared style brush mutates IN PLACE.
            // Regression: on Android the chrome hosting chain swallowed the child-level damage,
            // so the pill kept the previous theme's pixels (MAUI state and even the native
            // drawable paint were correct) until some unrelated global sweep redrew the strip.
            await App.TapAsync("ToggleThemeBravo");
            await App.WaitForPixelColorAsync("TabBravo", 10, 30, c => IsClose(c, 0x22, 0x30, 0x50));

            // And back: both directions must repaint live.
            await App.TapAsync("ToggleThemeBravo");
            await App.WaitForPixelColorAsync("TabBravo", 10, 30, c => IsClose(c, 0xE4, 0xEB, 0xFD));
        }
        finally
        {
            // Never leak a forced theme into the next test.
            await App.TapAsync("ResetThemeBravo");
        }
    }

    private static bool IsClose((byte R, byte G, byte B) c, byte r, byte g, byte b)
        => Math.Abs(c.R - r) < 20 && Math.Abs(c.G - g) < 20 && Math.Abs(c.B - b) < 20;
}
