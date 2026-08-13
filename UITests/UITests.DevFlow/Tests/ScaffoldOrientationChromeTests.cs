using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers what ROTATION does to the scaffold, against the "Scaffold Orientation Tests" harness:
/// six tab roots that overflow in portrait, plus the safe-area probe on the root page.
/// </summary>
/// <remarks>
/// The scaffold has several size-change-sensitive paths that no portrait-only suite can reach —
/// the tab bar's overflow set is recomputed per layout pass, a hidden strip must stay offscreen
/// across size changes, and the nav bar claims to keep clear of landscape notches. Rotation is
/// driven in-app (see <c>OrientationProbe</c>): no host-side tool can rotate an iOS simulator.
/// Every test restores portrait, since the app is shared by the whole run.
/// </remarks>
public class ScaffoldOrientationChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Orientation Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForBoundsAsync("OrRootPage", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync()
    {
        // Never leave the device rotated for the next class.
        await App.SetOrientationAsync(landscape: false);
        await App.ResetAsync();
    }

    /// <summary>
    /// The bar fits as many roots as the window allows: a wider window must take items BACK from
    /// the overflow panel, not keep the partition it computed in portrait.
    /// </summary>
    [Fact]
    public async Task RotatingRepartitionsTheTabBarOverflow()
    {
        // Portrait: the last roots do not fit and are parked offscreen in the overflow panel.
        var overflowedInPortrait = await App.GetBoundsAsync("TabSix");
        overflowedInPortrait.X.Should().BeLessThan(0, "six roots cannot fit a portrait phone bar");
        (await App.WaitForElementAsync("TabMore")).IsVisible.Should().BeTrue("the overflow item stands in for them");

        await App.SetOrientationAsync(landscape: true);

        // Landscape: the same root is now IN the bar.
        await App.WaitForBoundsAsync("TabSix", b => b.X > 0);

        await App.SetOrientationAsync(landscape: false);

        // …and back out again when the window narrows.
        await App.WaitForBoundsAsync("TabSix", b => b.X < 0);
    }

    /// <summary>
    /// Landscape brings side insets into play (the notch edge). The page must keep clear of them:
    /// content starting at x = 0 would sit under the cutout.
    /// </summary>
    [Fact]
    public async Task PageKeepsClearOfTheSideInsetsInLandscape()
    {
        await App.SetOrientationAsync(landscape: true);

        var insets = await App.GetSafeAreaInsetsAsync();
        var side = Math.Max(insets.Left, insets.Right);
        Assert.SkipWhen(side <= 0, "This device has no side insets in landscape: nothing to keep clear of.");

        var marker = await App.WaitForStableBoundsAsync("OrRootPage");
        marker.X.Should().BeGreaterThanOrEqualTo(insets.Left - 1, "page content must not sit under the display cutout");

        await App.SetOrientationAsync(landscape: false);
    }
}
