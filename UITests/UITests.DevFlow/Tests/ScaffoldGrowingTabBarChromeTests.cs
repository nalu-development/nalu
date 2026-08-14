using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers a custom tab bar whose HEIGHT CHANGES AT RUNTIME, against the "Scaffold Growing TabBar
/// Tests" harness: a button toggles the bar's band between two heights.
/// </summary>
/// <remarks>
/// This is the measure-invalidation path end to end: the band's HeightRequest change propagates
/// through MAUI's ancestor invalidation into the strip, the strip re-measures the bar, the host
/// re-lays out the strip at its new height, and the page's chrome inset follows. Every other bar
/// harness answers the same measure for its whole lifetime, so none of this had coverage — a
/// strip that silently stopped listening to descendant invalidations would break height changes
/// while passing every static-bar test.
/// </remarks>
public class ScaffoldGrowingTabBarChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Growing TabBar Tests";
    private const double Growth = 60; // TallHeight - CompactHeight in the harness.

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForBoundsAsync("GrowBarPage", b => b.Y > 0);
    }

    public ValueTask DisposeAsync() => new(App.ResetAsync());

    /// <summary>
    /// Growing the band must grow the presented bar by exactly the same amount, and shrinking it
    /// must give the space back — the strip's measure follows the bar's content in BOTH
    /// directions, not just at mount.
    /// </summary>
    [Fact]
    public async Task BarGrowsAndShrinksWithItsContent()
    {
        var compact = await App.WaitForStableBoundsAsync("GrowBarRoot");

        await App.TapAsync("GrowBarToggle");

        await App.WaitForBoundsAsync("GrowBarRoot", b => Math.Abs(b.Height - (compact.Height + Growth)) <= 1);
        var tall = await App.WaitForStableBoundsAsync("GrowBarRoot");
        tall.Y.Should().BeApproximately(compact.Y - Growth, 1, "the bar grows UPWARD from its bottom-anchored resting position");

        await App.TapAsync("GrowBarToggle");

        await App.WaitForBoundsAsync("GrowBarRoot", b => Math.Abs(b.Height - compact.Height) <= 1);
        (await App.WaitForStableBoundsAsync("GrowBarRoot")).Y.Should().BeApproximately(compact.Y, 1);
    }

    /// <summary>
    /// The page's usable bottom must track the bar EXACTLY: the inset the bar contributes ends the
    /// page precisely at the bar's top edge — too small and content sits under the bar, too large
    /// and the page wastes a band of dead space above it. Asserted at both heights, both
    /// directions. This is the half a stale measure breaks FIRST.
    /// </summary>
    [Fact]
    public async Task PageInsetFollowsTheBarHeight()
    {
        var marker = await App.WaitForStableBoundsAsync("GrowBarBottomMarker");
        var bar = await App.WaitForStableBoundsAsync("GrowBarRoot");
        marker.Bottom.Should().BeApproximately(bar.Y, 1, "the page's usable bottom IS the bar's top edge");

        await App.TapAsync("GrowBarToggle");

        await App.WaitForBoundsAsync("GrowBarBottomMarker", b => Math.Abs(b.Bottom - (marker.Bottom - Growth)) <= 1);

        var grownBar = await App.WaitForStableBoundsAsync("GrowBarRoot");
        var grownMarker = await App.WaitForStableBoundsAsync("GrowBarBottomMarker");
        grownMarker.Bottom.Should().BeApproximately(grownBar.Y, 1, "and it still is against the taller bar");

        await App.TapAsync("GrowBarToggle");
        await App.WaitForBoundsAsync("GrowBarBottomMarker", b => Math.Abs(b.Bottom - marker.Bottom) <= 1);
    }

    private const double NavGrowth = 44; // TallNavHeight - CompactNavHeight in the harness.

    /// <summary>
    /// The top-edge mirror: the nav bar is freely sizable (the strip takes whatever its content
    /// measures), so growing its band must grow the presented bar downward by the same amount.
    /// </summary>
    [Fact]
    public async Task NavBarGrowsAndShrinksWithItsContent()
    {
        var compact = await App.WaitForStableBoundsAsync("GrowNavRoot");

        await App.TapAsync("GrowNavToggle");

        await App.WaitForBoundsAsync("GrowNavRoot", b => Math.Abs(b.Height - (compact.Height + NavGrowth)) <= 1);
        var tall = await App.WaitForStableBoundsAsync("GrowNavRoot");
        tall.Y.Should().BeApproximately(compact.Y, 1, "a top bar grows DOWNWARD from its top-anchored resting position");

        await App.TapAsync("GrowNavToggle");
        await App.WaitForBoundsAsync("GrowNavRoot", b => Math.Abs(b.Height - compact.Height) <= 1);
    }

    /// <summary>
    /// And the page's usable TOP must be the nav bar's bottom edge, exactly, at both heights.
    /// </summary>
    [Fact]
    public async Task PageInsetFollowsTheNavBarHeight()
    {
        var marker = await App.WaitForStableBoundsAsync("GrowBarTopMarker");
        var bar = await App.WaitForStableBoundsAsync("GrowNavRoot");
        marker.Y.Should().BeApproximately(bar.Bottom, 1, "the page's usable top IS the nav bar's bottom edge");

        await App.TapAsync("GrowNavToggle");

        await App.WaitForBoundsAsync("GrowBarTopMarker", b => Math.Abs(b.Y - (marker.Y + NavGrowth)) <= 1);

        var grownBar = await App.WaitForStableBoundsAsync("GrowNavRoot");
        var grownMarker = await App.WaitForStableBoundsAsync("GrowBarTopMarker");
        grownMarker.Y.Should().BeApproximately(grownBar.Bottom, 1, "and it still is against the taller nav bar");

        await App.TapAsync("GrowNavToggle");
        await App.WaitForBoundsAsync("GrowBarTopMarker", b => Math.Abs(b.Y - marker.Y) <= 1);
    }
}
