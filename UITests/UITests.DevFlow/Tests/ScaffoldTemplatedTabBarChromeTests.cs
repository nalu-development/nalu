using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers a TEMPLATED custom tab bar across root switches, against the "Scaffold Templated TabBar
/// Tests" harness: a non-consuming bar (<c>SafeAreaEdges.None</c>) whose cells come from a template,
/// and two roots that disagree about the nav bar so every switch presents or dismisses it.
/// </summary>
/// <remarks>
/// The strip settles its bar and then dirties the host so the new height propagates. A chrome
/// animation lays the tree out through <c>LayoutIfNeeded</c> inside an animation block, where UIKit
/// drains dirty views before returning — so a settle that keeps re-dirtying the host wedges the
/// main thread outright, with no failed assertion to show for it. These tests fail as TIMEOUTS
/// (the in-app agent stops answering), which is the only signal a frozen thread can give.
/// </remarks>
public class ScaffoldTemplatedTabBarChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Templated TabBar Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForBoundsAsync("TplTabHomePage", b => b.Y > 0);
    }

    public ValueTask DisposeAsync() => new(App.ResetAsync());

    /// <summary>
    /// Switching roots repeatedly drives the nav bar in and out under the bar. Each switch must
    /// complete, and the bar must keep the geometry it had — a strip that re-measures itself into a
    /// loop either never lets the switch finish, or settles at a height that keeps growing.
    /// </summary>
    [Fact]
    public async Task RepeatedRootSwitchesLeaveTheBarWhereItWas()
    {
        var resting = await App.WaitForStableBoundsAsync("TplTabBarRoot");

        for (var i = 0; i < 4; i++)
        {
            await App.TapAsync("TplGoOtherRoot");
            await App.WaitForBoundsAsync("TplTabOtherPage", b => b.Y > 0);

            await App.TapAsync("TplGoHomeRoot");
            await App.WaitForBoundsAsync("TplTabHomePage", b => b.Y > 0);
        }

        var settled = await App.WaitForStableBoundsAsync("TplTabBarRoot");

        settled.Height.Should().BeApproximately(resting.Height, 1, "a bar that consumes nothing has no reason to re-measure to a different height");
        settled.Y.Should().BeApproximately(resting.Y, 1, "so it must also come to rest where it started");
    }

    /// <summary>
    /// The page must keep clear of the bar after the switches: the inset the host applies comes
    /// from the bar's settled measure, so a measure left stale (or one that never stopped moving)
    /// shows up as content running under the bar.
    /// </summary>
    [Fact]
    public async Task PageStaysClearOfTheBarAcrossRootSwitches()
    {
        await App.TapAsync("TplGoOtherRoot");
        await App.WaitForBoundsAsync("TplTabOtherPage", b => b.Y > 0);

        await App.TapAsync("TplGoHomeRoot");
        await App.WaitForBoundsAsync("TplTabHomePage", b => b.Y > 0);

        var bar = await App.WaitForStableBoundsAsync("TplTabBarRoot");
        var button = await App.WaitForStableBoundsAsync("TplGoOtherRoot");

        button.Bottom.Should().BeLessThanOrEqualTo(bar.Y + 1, "page content must not run under the tab bar");
    }
}
