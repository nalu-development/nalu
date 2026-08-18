using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the horizontal <c>ScrollBox</c> orientation and the RUNTIME orientation swap
/// (Android rebuilds the platform scroller; iOS re-measures the same scroll view — both must
/// keep programmatic scrolling exact afterwards). MAUI's ScrollView cannot even change
/// orientation at runtime on iOS (dotnet/maui#22111).
/// </summary>
public class ScrollBoxHorizontalTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Scroll Box Horizontal Tests";

    // The page hosts 40 items, each exactly 60 units wide, in a horizontal ScrollBox.

    [Fact]
    public async Task HorizontalJumpLandsOnTheExactTarget()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("HItem1");

        await App.TapAsync("HJumpTo300Button");

        await App.WaitForTextAsync("HResultLabel", "done X:300 Y:0");
    }

    [Fact]
    public async Task DescendantCenterWorksOnTheHorizontalAxis()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("HItem1");

        await App.TapAsync("HItem30CenterButton");
        await App.WaitForTextMatchAsync("HResultLabel", text => text?.StartsWith("done") == true);

        var item30 = await App.WaitForStableBoundsAsync("HItem30");
        var scrollBox = await App.GetBoundsAsync("HScrollBox");

        var itemCenter = item30.X + (item30.Width / 2);
        var viewportCenter = scrollBox.X + (scrollBox.Width / 2);
        itemCenter.Should().BeApproximately(viewportCenter, scrollBox.Width / 6);
    }

    [Fact]
    public async Task RuntimeOrientationSwapKeepsProgrammaticScrollingExact()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("HItem1");

        // Horizontal first: prove the axis works before swapping.
        await App.TapAsync("HJumpTo300Button");
        await App.WaitForTextAsync("HResultLabel", "done X:300 Y:0");

        // Swap to vertical: the scroll position resets and the OTHER axis becomes scrollable.
        await App.TapAsync("HToggleOrientationButton");
        await App.WaitForTextAsync("HOrientationLabel", "Vertical");
        await App.TapAsync("HJumpTo200YButton");
        await App.WaitForTextAsync("HResultLabel", "done X:0 Y:200");

        // And back to horizontal.
        await App.TapAsync("HToggleOrientationButton");
        await App.WaitForTextAsync("HOrientationLabel", "Horizontal");
        await App.TapAsync("HJumpTo300Button");
        await App.WaitForTextAsync("HResultLabel", "done X:300 Y:0");
    }
}

/// <summary>
/// Covers pull-to-refresh plumbing: the controller pipeline (simulated pull → RefreshCommand/
/// OnRefresh → completion), the two-way <c>IsRefreshing</c> sync, and the offset restore after
/// a programmatic refresh. The physical pull gesture itself has no synthetic equivalent.
/// </summary>
public class ScrollBoxRefreshTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Scroll Box Refresh Tests";

    [Fact]
    public async Task SimulatedPullRunsTheRefreshPipelineAndCompletes()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("RItem1");
        await App.WaitForTextAsync("RefreshCountLabel", "Refreshes: 0");

        await App.TapAsync("SimulatePullButton");

        // The pipeline raised OnRefresh and IsRefreshing went true (two-way sync).
        await App.WaitForTextAsync("RefreshCountLabel", "Refreshes: 1");
        await App.WaitForTextAsync("IsRefreshingLabel", "True");

        // Completing the refresh drops IsRefreshing back to false.
        await App.TapAsync("CompleteRefreshButton");
        await App.WaitForTextAsync("IsRefreshingLabel", "False");
    }

    [Fact]
    public async Task ProgrammaticSpinnerRestoresTheRestingOffsetWhenDone()
    {
        await App.OpenTestPageAsync(PageName);
        var restBounds = await App.WaitForStableBoundsAsync("RItem1");

        // Programmatic IsRefreshing reveals the spinner (content shifts down on iOS)...
        await App.TapAsync("ShowRefreshButton");
        await App.WaitForTextAsync("IsRefreshingLabel", "True");

        // ...and ending it must restore the EXACT resting offset once the platform settles.
        await App.TapAsync("CompleteRefreshButton");
        await App.WaitForTextAsync("IsRefreshingLabel", "False");

        await App.WaitForBoundsAsync(
            "RItem1",
            bounds => Math.Abs(bounds.Y - restBounds.Y) <= 1.5,
            TimeSpan.FromSeconds(8)
        );
    }
}
