using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the GEOMETRY of modal presentation (§7.1) against the "Scaffold Modal Geometry Tests"
/// harness: the usable band a modal page gets while it covers the tab bar, what it must give back
/// on dismiss, and what rotation does to a presented modal. The chrome/back-channel half of the
/// modal contract (X button, plain-Modal back consumption) lives in
/// <see cref="ScaffoldModalChromeTests"/>; this class adds the geometric half plus the iOS
/// interactive-pop gate, which nothing was driving.
/// </summary>
public class ScaffoldModalGeometryChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Modal Geometry Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForBoundsAsync("MgHomePage", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync()
    {
        // Never leave the device rotated for the next class.
        await App.SetOrientationAsync(landscape: false);
        await App.ResetAsync();
    }

    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    private async Task PushModalAsync()
    {
        await App.TapAsync("PushMgModal");
        await WaitDisplayedAsync("MgModalPage");
        await App.WaitForElementGoneAsync("TabMgOne");
    }

    /// <summary>
    /// The modal's usable band is exact on both edges: its top IS the modal nav bar's bottom edge
    /// (the title-only bar keeps its footprint), and its bottom IS the window bottom minus the
    /// system inset — the band the covered tab bar occupied is reclaimed, which is what "the modal
    /// covers the tab bar" means geometrically. Too small an inset puts content under the chrome;
    /// too large leaves a dead band where the tab bar used to be.
    /// </summary>
    [Fact]
    public async Task ModalUsableBandRunsFromTheNavBarToTheBottomInset()
    {
        // The covered page's usable bottom, while the tab bar is still there.
        var homeBottom = await App.WaitForStableBoundsAsync("MgHomeBottomMarker");

        await PushModalAsync();

        var (_, windowHeight) = await App.GetWindowSizeAsync();
        var insets = await App.GetSafeAreaInsetsAsync();

        var topMarker = await App.WaitForStableBoundsAsync("MgModalTopMarker");
        var bottomMarker = await App.WaitForStableBoundsAsync("MgModalBottomMarker");

        // Size FIRST: a collapsed marker still sits at a plausible offset, so edge assertions
        // alone would pass on an unlaid-out page.
        topMarker.Height.Should().BeApproximately(10, 1, "the marker keeps its requested height");
        bottomMarker.Height.Should().BeApproximately(10, 1, "the marker keeps its requested height");

        var navBar = await App.WaitForStableBoundsAsync("NavBarSurface");
        topMarker.Y.Should().BeApproximately(navBar.Bottom, 1, "the modal's usable top IS the modal nav bar's bottom edge");

        bottomMarker.Bottom.Should().BeApproximately(
            windowHeight - insets.Bottom,
            1,
            "with the tab bar covered, the modal's usable bottom is the window bottom minus the system inset"
        );

        bottomMarker.Bottom.Should().BeGreaterThan(
            homeBottom.Bottom + 1,
            "the modal reclaims the band the tab bar occupied — a modal ending where the covered page ended never covered the bar at all"
        );
    }

    /// <summary>
    /// Dismissing the modal must give the covered page back at its ORIGINAL geometry, and the tab
    /// bar back at its resting frame — a stale inset surviving the round trip shifts either one.
    /// </summary>
    [Fact]
    public async Task DismissRestoresTheCoveredPageAtItsOriginalGeometry()
    {
        var topBefore = await App.WaitForStableBoundsAsync("MgHomeTopMarker");
        var bottomBefore = await App.WaitForStableBoundsAsync("MgHomeBottomMarker");
        var tabBefore = await App.WaitForStableBoundsAsync("TabMgOne");

        await PushModalAsync();

        await App.TapAsync("NavBarCloseButton");
        await WaitDisplayedAsync("MgHomePage");

        // Retry-until-match: the pop transition is still settling when the page becomes visible.
        // Size and position both — a covered page re-laid out with a stale chrome inset comes back
        // displaced or resized, and either half alone can mask the other.
        await App.WaitForBoundsAsync(
            "MgHomeTopMarker",
            b => Math.Abs(b.Height - topBefore.Height) <= 1 && Math.Abs(b.Y - topBefore.Y) <= 1
        );

        await App.WaitForBoundsAsync(
            "MgHomeBottomMarker",
            b => Math.Abs(b.Height - bottomBefore.Height) <= 1 && Math.Abs(b.Bottom - bottomBefore.Bottom) <= 1
        );

        await App.WaitForBoundsAsync(
            "TabMgOne",
            b => Math.Abs(b.Y - tabBefore.Y) <= 1 && Math.Abs(b.Height - tabBefore.Height) <= 1
        );
    }

    /// <summary>
    /// The iOS interactive pop is gated to <c>ScaffoldPageMode.Default</c>: a modal enters from
    /// the bottom and must NOT leave through a left-edge swipe. The gesture is a real HID edge
    /// swipe — the recognizer reads raw touches, so nothing in-process can stand in for it.
    /// </summary>
    [Fact]
    public async Task AppleEdgeSwipeCannotDismissAModal()
    {
        Assert.SkipUnless(await App.IsAppleAsync(), "The interactive pop is driven by a simulator edge swipe.");

        await PushModalAsync();

        var gesture = await App.BeginAppleEdgeSwipeBackAsync();
        await gesture;

        // The swipe travelled far past the commit threshold: had the recognizer engaged, the
        // modal would be gone by now.
        await App.WaitForSettledDisplayAsync("MgModalPage");
        (await App.FindElementAsync("TabMgOne")).Should().BeNull("the tab bar must still be covered");

        await App.TapAsync("NavBarCloseButton");
        await WaitDisplayedAsync("MgHomePage");
    }

    /// <summary>
    /// Rotating while the modal is presented re-lays it out against a window of different
    /// proportions: the tab bar must stay covered, and the usable band must be re-derived from
    /// the NEW window — nav bar bottom to landscape bottom inset — not kept from portrait.
    /// </summary>
    [Fact]
    public async Task RotationKeepsTheModalCoveringTheNewWindow()
    {
        await PushModalAsync();

        await App.SetOrientationAsync(landscape: true);

        (await App.FindElementAsync("TabMgOne")).Should().BeNull("a covered tab bar must not reappear when the window changes shape");

        var (_, landscapeHeight) = await App.GetWindowSizeAsync();
        var insets = await App.GetSafeAreaInsetsAsync();

        var topMarker = await App.WaitForStableBoundsAsync("MgModalTopMarker");
        var bottomMarker = await App.WaitForStableBoundsAsync("MgModalBottomMarker");

        topMarker.Height.Should().BeApproximately(10, 1, "the marker keeps its requested height");
        bottomMarker.Height.Should().BeApproximately(10, 1, "the marker keeps its requested height");

        var navBar = await App.WaitForStableBoundsAsync("NavBarSurface");
        topMarker.Y.Should().BeApproximately(navBar.Bottom, 1, "the usable top tracks the nav bar in the new window");

        bottomMarker.Bottom.Should().BeApproximately(
            landscapeHeight - insets.Bottom,
            1,
            "the usable bottom is re-derived from the landscape window, not kept from portrait"
        );

        await App.SetOrientationAsync(landscape: false);

        // Back in portrait the modal is still up and still covering.
        await WaitDisplayedAsync("MgModalPage");
        (await App.FindElementAsync("TabMgOne")).Should().BeNull();

        await App.TapAsync("NavBarCloseButton");
        await WaitDisplayedAsync("MgHomePage");
        (await App.WaitForElementAsync("TabMgOne")).IsVisible.Should().BeTrue("dismissing after the round trip still restores the tab bar");
    }
}
