using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers ScrollBox safe-area behavior inside a Scaffold with a translucent nav bar and a tab
/// bar, on an edge-to-edge page (<c>SafeAreaEdges=None</c>): insets are applied exactly ONCE by
/// the scroller — content rests inside the chrome, scrolls under it, programmatic scrolls clamp
/// against the insets, and the resting position is stable across scroll round-trips (the MAUI
/// "safe area applied twice" bug class).
/// </summary>
public class ScrollBoxSafeAreaTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Scroll Box SafeArea Tests";

    /// <summary>The harness page is Scaffold-hosted, and the Scaffold runs on iOS and Android only.</summary>
    private async Task<bool> IsScaffoldPlatformAsync()
    {
        var platform = await App.GetPlatformAsync();

        return platform.Contains("android", StringComparison.OrdinalIgnoreCase)
               || (platform.Contains("ios", StringComparison.OrdinalIgnoreCase)
                   && !platform.Contains("catalyst", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ContentRestsInsideTheChromeAndScrollsUnderIt()
    {
        if (!await IsScaffoldPlatformAsync())
        {
            return;
        }

        await App.OpenTestPageAsync(PageName);
        var restBounds = await App.WaitForStableBoundsAsync("SafeItem1");
        var (_, windowHeight) = await App.GetWindowSizeAsync();

        // At rest the first item must sit BELOW the status bar + nav bar chrome, not at the
        // physical top edge.
        restBounds.Y.Should().BeGreaterThan(40);

        // And the inset must live INSIDE the scroller: the ScrollBox frame extends edge-to-edge
        // behind the chrome, well above the first item's resting position — otherwise content
        // would clip at the bar edge instead of scrolling under it.
        var scrollBoxBounds = await App.GetBoundsAsync("SafeAreaScrollBox");
        scrollBoxBounds.Y.Should().BeLessThan(restBounds.Y - 50);
        (scrollBoxBounds.Y + scrollBoxBounds.Height).Should().BeGreaterThan(windowHeight - 30);

        // Scroll to the very end: the entry must land fully visible ABOVE the tab bar footprint.
        await App.TapAsync("SafeEndButton");
        await App.WaitForTextMatchAsync("SafeAreaResultLabel", text => text?.StartsWith("done") == true);

        var entryBounds = await App.WaitForStableBoundsAsync("SafeAreaEntry");
        (entryBounds.Y + entryBounds.Height).Should().BeLessThan(windowHeight - 40);

        // And the first item scrolled UNDER the nav bar (content is edge-to-edge).
        var scrolledBounds = await App.GetBoundsAsync("SafeItem1");
        scrolledBounds.Y.Should().BeLessThan(restBounds.Y - 200);
    }

    [Fact]
    public async Task RestingPositionIsStableAcrossScrollRoundTrips()
    {
        if (!await IsScaffoldPlatformAsync())
        {
            return;
        }

        await App.OpenTestPageAsync(PageName);
        var restBounds = await App.WaitForStableBoundsAsync("SafeItem1");

        // End → Start → the first item must land EXACTLY where it rested: any drift means an
        // inset got applied twice (or a clamp missed one).
        await App.TapAsync("SafeEndButton");
        await App.WaitForTextMatchAsync("SafeAreaResultLabel", text => text?.StartsWith("done") == true);
        await App.TapAsync("SafeStartButton");
        await App.WaitForTextAsync("SafeAreaResultLabel", "done Y:0");

        var roundTripBounds = await App.WaitForStableBoundsAsync("SafeItem1");
        roundTripBounds.Y.Should().BeApproximately(restBounds.Y, 1.5);
    }

    [Fact]
    public async Task AndroidImeInsetLetsTheEntryScrollAboveTheKeyboard()
    {
        if (await App.GetPlatformAsync() != "android")
        {
            return;
        }

        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("SafeItem1");

        await App.TapAsync("SafeEndButton");
        await App.WaitForTextMatchAsync("SafeAreaResultLabel", text => text?.StartsWith("done") == true);
        var beforeKeyboard = await App.WaitForStableBoundsAsync("SafeAreaEntry");

        // Focusing the entry raises the IME: the ime inset must join the scroller's self-padding
        // so the entry can sit above the keyboard instead of being covered by it.
        await App.FocusAsync("SafeAreaEntry");
        await App.WaitForAndroidSoftKeyboardAsync(visible: true);

        var (_, windowHeight) = await App.GetWindowSizeAsync();

        var withKeyboard = await App.WaitForBoundsAsync(
            "SafeAreaEntry",
            bounds => bounds.Y + bounds.Height < windowHeight - 250,
            TimeSpan.FromSeconds(5)
        );

        (withKeyboard.Y + withKeyboard.Height).Should().BeLessThan(beforeKeyboard.Y + beforeKeyboard.Height);

        // Dismiss the keyboard to leave the app in a clean state for the next test.
        await App.TapAsync("SafeItem20");
        await App.WaitForAndroidSoftKeyboardAsync(visible: false);
    }
}
