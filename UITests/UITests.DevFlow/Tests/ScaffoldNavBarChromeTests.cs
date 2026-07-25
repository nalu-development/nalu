using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the Scaffold's default nav bar (§5.2) against the "Scaffold NavBar Tests" harness:
/// a single plain area, a global start flyout, per-page titles, the Auto/Visible drawer-button
/// policy, a per-page custom bar, and the §5.4 top-inset contribution.
/// </summary>
public class ScaffoldNavBarChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold NavBar Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    /// <summary>Waits until the element is actually DISPLAYED (positioned in the window).</summary>
    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    [Fact]
    public async Task TitleShowsCurrentPageAndBackIsHiddenAtRoot()
    {
        await WaitDisplayedAsync("NavBarPageHome");

        await App.WaitForTextAsync("NavBarTitleLabel", "Home Title");
        (await App.WaitForElementAsync("NavBarBackButton")).IsVisible.Should().BeFalse("no pushed pages at the root");
        (await App.WaitForElementAsync("NavBarFlyoutStartButton")).IsVisible.Should().BeTrue("the global flyout is set and the stack is empty (Auto)");
        (await App.WaitForElementAsync("NavBarFlyoutEndButton")).IsVisible.Should().BeFalse("no end flyout is configured");
    }

    [Fact]
    public async Task PushShowsBackButtonAndPopsThroughIt()
    {
        await WaitDisplayedAsync("NavBarPageHome");

        await App.TapAsync("PushNavBarDetail");
        await WaitDisplayedAsync("NavBarPageDetail");
        await App.WaitForTextAsync("NavBarTitleLabel", "Detail Title");
        (await App.WaitForElementAsync("NavBarBackButton")).IsVisible.Should().BeTrue();

        // The drawer button yields while pages are pushed (Auto policy).
        (await App.WaitForElementAsync("NavBarFlyoutStartButton")).IsVisible.Should().BeFalse();

        await App.TapAsync("NavBarBackButton");
        await WaitDisplayedAsync("NavBarPageHome");
        await App.WaitForTextAsync("NavBarTitleLabel", "Home Title");
        (await App.WaitForElementAsync("NavBarBackButton")).IsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task VisiblePolicyKeepsDrawerButtonNextToBack()
    {
        await WaitDisplayedAsync("NavBarPageHome");

        await App.TapAsync("PushNavBarDrawerDetail");
        await WaitDisplayedAsync("NavBarPageDrawerDetail");

        // FlyoutStartButtonVisibility=Visible on the pushed page: drawer AND back, side by side.
        (await App.WaitForElementAsync("NavBarBackButton")).IsVisible.Should().BeTrue();
        (await App.WaitForElementAsync("NavBarFlyoutStartButton")).IsVisible.Should().BeTrue();

        await App.TapAsync("PopNavBarDrawerDetail");
        await WaitDisplayedAsync("NavBarPageHome");
    }

    [Fact]
    public async Task DrawerButtonOpensFlyout()
    {
        await WaitDisplayedAsync("NavBarPageHome");

        await App.TapAsync("NavBarFlyoutStartButton");
        await WaitDisplayedAsync("GlobalNavFlyoutLabel");

        // Flyout content never leaves the ELEMENT tree and keeps stale bounds when closed:
        // the harness's close handler records deterministic completion instead.
        await App.TapAsync("CloseNavFlyout");
        await App.WaitForTextAsync("NavFlyoutState", "closed");
    }

    [Fact]
    public async Task CustomPerPageNavBarSwapsAndRestores()
    {
        await WaitDisplayedAsync("NavBarPageHome");

        await App.TapAsync("PushNavBarCustom");
        await WaitDisplayedAsync("NavBarPageCustom");

        // The page-level custom bar replaces the default one; its primitive back button works.
        await WaitDisplayedAsync("CustomNavBarMarker");
        await App.WaitForElementGoneAsync("NavBarTitleLabel");

        await App.TapAsync("CustomNavBarBack");
        await WaitDisplayedAsync("NavBarPageHome");
        await App.WaitForElementGoneAsync("CustomNavBarMarker");
        await App.WaitForTextAsync("NavBarTitleLabel", "Home Title");
    }

    [Fact]
    public async Task NavBarContributesTopInset()
    {
        await WaitDisplayedAsync("NavBarPageHome");
        var withBar = await App.WaitForStableBoundsAsync("NavBarPageHome");

        // Hiding the bar is an inset change: the page content moves up by the bar footprint.
        await App.TapAsync("ToggleNavBar");
        await App.WaitForElementGoneAsync("NavBarTitleLabel");
        var withoutBar = await App.WaitForBoundsAsync("NavBarPageHome", b => b.Y < withBar.Y - 30);

        (withBar.Y - withoutBar.Y).Should().BeGreaterThan(30, "the bar footprint must stop insetting the page when hidden");

        await App.TapAsync("ToggleNavBar");
        await App.WaitForTextAsync("NavBarTitleLabel", "Home Title");
        await App.WaitForBoundsAsync("NavBarPageHome", b => Math.Abs(b.Y - withBar.Y) <= 1);
    }
}
