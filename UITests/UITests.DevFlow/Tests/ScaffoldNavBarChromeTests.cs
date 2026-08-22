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
    private const string _pageName = "Scaffold NavBar Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(_pageName);

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

    /// <summary>
    /// The window-controls spacer costs NOTHING unless the app is a window on an iPad — which no
    /// device this suite drives is: phones and Android have no such controls, and a full-screen
    /// iPad shows them only transiently. A spacer that reserved space here would push every bar's
    /// leading button right on every platform.
    /// </summary>
    [Fact(DisplayName = "The window-controls spacer reserves nothing on a full-screen window")]
    public async Task WindowControlsSpacerReservesNothingOutsideAWindowedIPad()
    {
        await WaitDisplayedAsync("NavBarPageHome");

        var (windowWidth, windowHeight) = await App.GetWindowSizeAsync();
        var spacer = await App.WaitForElementAsync("NavBarWindowControlsSpacer");

        spacer.IsVisible.Should().BeFalse(
            "the app fills the screen ({0}x{1}), so no system window controls sit over its corner",
            windowWidth,
            windowHeight
        );

        var button = await App.WaitForStableBoundsAsync("NavBarFlyoutStartButton");

        button.X.Should().BeLessThan(20, "the leading button keeps its usual bar padding, not a reserved band");
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

    /// <summary>
    /// The bar's internal layout must be CURRENT after a push: the back button appears with its
    /// real size and the title moves over to make room, both inside the bar's row. This is the
    /// regression the strip/host measure plumbing broke once (a stale row left the button 0×0 and
    /// the title at its root position, overlapping the back glyph) — asserted on geometry, not
    /// on visibility flags.
    /// </summary>
    [Fact]
    public async Task PushLaysOutBackButtonAndTitleSideBySide()
    {
        await WaitDisplayedAsync("NavBarPageHome");
        var rootTitle = await App.WaitForStableBoundsAsync("NavBarTitleLabel");
        var rootRow = await App.WaitForStableBoundsAsync("NavBarTitle");

        await App.TapAsync("PushNavBarDetail");
        await WaitDisplayedAsync("NavBarPageDetail");
        await App.WaitForTextAsync("NavBarTitleLabel", "Detail Title");

        // Materialized AND settled. The bar travels WITH its page now, so a back button read
        // while the push is still sliding sits wherever the page had got to — comparing it with
        // a settled title slot is comparing two different moments, and the overlap it reports is
        // not one that ever exists on screen.
        await App.WaitForBoundsAsync("NavBarBackButton", b => b.Width >= 40 && b.Height >= 40);
        var back = await App.WaitForStableBoundsAsync("NavBarBackButton");
        var title = await App.WaitForStableBoundsAsync("NavBarTitleLabel");
        var titleSlot = await App.WaitForStableBoundsAsync("NavBarTitle");

        back.X.Should().BeGreaterThanOrEqualTo(0);
        back.CenterY.Should().BeApproximately(rootRow.CenterY, 2, "the back button is vertically centered in the same row as the title");
        titleSlot.X.Should().BeGreaterThanOrEqualTo(back.Right - 1, "the title slot starts after the back button");
        title.X.Should().BeGreaterThanOrEqualTo(back.Right - 1, "the title text never overlaps the back button");
        title.CenterY.Should().BeApproximately(rootTitle.CenterY, 2, "the bar keeps its height across the push");

        await App.TapAsync("NavBarBackButton");
        await WaitDisplayedAsync("NavBarPageHome");
        await App.WaitForTextAsync("NavBarTitleLabel", "Home Title");
        (await App.WaitForStableBoundsAsync("NavBarTitleLabel")).X.Should().BeApproximately(rootTitle.X, 1, "the title returns to its root position");
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

    /// <summary>
    /// A hidden bar must STAY hidden across a round trip through a page whose bar has a different
    /// height (the edge-to-edge page swaps in a 20dp custom bar, the pop swaps the default bar
    /// back). Regression: the hide slide targeted the strip's PREVIOUS height, so the taller
    /// default bar came back peeking over the page — the strip visible while the page was laid
    /// out for no bar. Asserted on pixels: the strip is out of the element tree while hidden.
    /// </summary>
    [Fact]
    public async Task HiddenBarStaysHiddenAcrossABarSwapRoundTrip()
    {
        await WaitDisplayedAsync("NavBarPageHome");
        var withBar = await App.WaitForStableBoundsAsync("NavBarPageHome");

        await App.TapAsync("ToggleNavBar");
        await App.WaitForElementGoneAsync("NavBarTitleLabel");
        var hidden = await App.WaitForBoundsAsync("NavBarPageHome", b => b.Y < withBar.Y - 30);
        var (width, height) = await App.GetWindowSizeAsync();

        // Sample point: beside the page's first line (right edge, no text there) — the bar-less
        // page paints its own background where the bar used to be.
        var samplePoint = ((width - 24) / width, hidden.CenterY / height);
        var restingSample = await WaitForStablePixelAsync(samplePoint);

        await App.TapAsync("PushNavBarEdgeToEdge");
        await WaitDisplayedAsync("NavBarPageEdgeToEdge");
        await App.WaitForBoundsAsync("EdgeToEdgeNavBarMarker", b => b.Height > 0);

        await App.TapAsync("PopNavBarEdgeToEdge");
        await App.WaitForSettledDisplayAsync("NavBarPageHome");

        (await App.WaitForStableBoundsAsync("NavBarPageHome")).Y.Should().BeApproximately(hidden.Y, 1, "the page is still laid out for NO bar");
        (await App.WaitForElementOrDefaultAsync("NavBarTitleLabel", TimeSpan.FromMilliseconds(500))).Should().BeNull("the bar stays out of the tree");

        var sample = await WaitForStablePixelAsync(samplePoint);
        sample.Should().Be(restingSample, "nothing but the page's own background is drawn beside its first line — no bar surface peeking over it");
    }

    /// <summary>The window pixel once it stops changing (a chrome slide may still be in flight).</summary>
    private async Task<(byte R, byte G, byte B)> WaitForStablePixelAsync((double X, double Y) point)
    {
        var last = (await App.SampleWindowPixelsAsync(point))[0];

        for (var i = 0; i < 20; i++)
        {
            await Task.Delay(150);
            var current = (await App.SampleWindowPixelsAsync(point))[0];

            if (current == last)
            {
                return current;
            }

            last = current;
        }

        return last;
    }
}
