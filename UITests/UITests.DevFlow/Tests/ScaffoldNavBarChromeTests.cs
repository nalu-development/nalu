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

    /// <summary>
    /// A custom nav bar CONSUMING the top inset: its content must sit below the system inset, and
    /// the strip must span inset + content. Asserted against platform ground truth so a dropped
    /// inset contribution cannot pass (the geometry alone would look plausible either way).
    /// </summary>
    [Fact]
    public async Task CustomNavBarConsumesTheTopInset()
    {
        await WaitDisplayedAsync("NavBarPageHome");

        var insets = await App.GetSafeAreaInsetsAsync();
        Assert.SkipWhen(insets.Top <= 0, "The device reports no top system inset: nothing to consume.");

        await App.TapAsync("PushNavBarCustom");
        await WaitDisplayedAsync("NavBarPageCustom");

        var marker = await App.WaitForStableBoundsAsync("CustomNavBarMarker");
        marker.Y.Should().BeApproximately(insets.Top, 1.5, "a consuming bar lays its content out below the status inset");
        marker.Height.Should().BeApproximately(44, 1.5, "the bar content keeps its declared height");

        // The strip itself must re-measure to THIS bar (inset + content), not keep the height of
        // the bar it replaced: the swap keeps the platform host, so only an explicit re-measure
        // resizes it. Without this the bar's own geometry above still looks right while the strip
        // stays as tall as the default bar.
        var surface = await App.WaitForStableBoundsAsync("NavBarSurface");
        surface.Height.Should().BeApproximately(insets.Top + 44, 1.5, "the strip spans the consumed inset plus the bar content");

        await App.TapAsync("PopNavBarCustom");
        await WaitDisplayedAsync("NavBarPageHome");
    }

    /// <summary>
    /// A custom nav bar declaring <c>SafeAreaEdges.None</c> starts at the SCREEN EDGE and keeps its
    /// own height: the author paints under the status bar themselves.
    /// </summary>
    /// <remarks>
    /// The regression this pins is the virtual bar swap. The strip keeps its platform host across
    /// swaps, so nothing raises an inset callback and its measure still describes the bar it
    /// replaced — the incoming 20dp bar was centered inside the 96dp strip of the default bar
    /// (48 inset + the 48dp default row), floating below the top edge with surface above and below.
    /// Asserting the SURFACE height is what catches it: the bar's own geometry alone looked
    /// plausible either way.
    /// </remarks>
    [Fact]
    public async Task SwappedCustomNavBarStartsAtTheScreenEdge()
    {
        await WaitDisplayedAsync("NavBarPageHome");

        var insets = await App.GetSafeAreaInsetsAsync();

        await App.TapAsync("PushNavBarEdgeToEdge");
        await WaitDisplayedAsync("NavBarPageEdgeToEdge");

        var bar = await App.WaitForStableBoundsAsync("EdgeToEdgeNavBarMarker");
        bar.Y.Should().BeApproximately(0, 1.5, "declining the safe area means starting at the very top edge");
        bar.Height.Should().BeApproximately(20, 1.5, "declining the inset must not inflate the bar");

        var surface = await App.WaitForStableBoundsAsync("NavBarSurface");
        surface.Height.Should().BeApproximately(20, 1.5, "the strip must re-measure to the swapped bar instead of keeping the previous bar's height");

        // The page below still clears the system bar: the strip's contribution clamps at zero
        // rather than going negative for a bar shorter than the inset.
        var pageMarker = await App.WaitForStableBoundsAsync("NavBarPageEdgeToEdge");
        pageMarker.Y.Should().BeGreaterThanOrEqualTo(insets.Top - 1, "page content must never be pulled under the system bar");

        await App.TapAsync("PopNavBarEdgeToEdge");
        await WaitDisplayedAsync("NavBarPageHome");
    }

    /// <summary>
    /// With NO tab bar anywhere (this harness is a single plain area), the page must still receive
    /// the native BOTTOM inset: the scaffold contributes chrome ON TOP of the system safe area
    /// (iOS <c>AdditionalSafeAreaInsets</c> is additive), so contributing zero at the bottom must
    /// not flatten the home-indicator clearance the page is entitled to.
    /// </summary>
    /// <remarks>
    /// Asserted on the OBSERVABLE — where the page's content actually ends — rather than on any
    /// inset value read back from the framework. Whether MAUI turns the dispatched insets into
    /// padding, and on which view, depends on each view's <c>SafeAreaEdges</c>; reading that back
    /// would encode an interpretation of the declaration, which is exactly what the chrome
    /// measurement refuses to do. A bottom-anchored marker needs no interpretation and means the
    /// same thing on both platforms.
    /// </remarks>
    [Fact]
    public async Task PageKeepsTheNativeBottomInsetWithoutATabBar()
    {
        await WaitDisplayedAsync("NavBarPageHome");

        var insets = await App.GetSafeAreaInsetsAsync();
        Assert.SkipWhen(insets.Bottom <= 0, "The device reports no bottom system inset: nothing to preserve.");

        var (_, windowHeight) = await App.GetWindowSizeAsync();
        var probe = await App.WaitForStableBoundsAsync("NavBarBottomProbe");

        (probe.Y + probe.Height).Should().BeApproximately(
            windowHeight - insets.Bottom,
            2,
            "with no tab bar the page still receives the native bottom inset, so its content ends above the system bar"
        );
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
