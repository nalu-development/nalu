using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Full ScrollTo combination matrix for VirtualScroll: items, section headers (itemIndex -1)
/// and the global header/footer sentinels, across Start/Center/End/MakeVisible.
/// </summary>
/// <remarks>
/// Page geometry (see the TestApp page): global header/footer 40, section headers 30,
/// items 50, section footers 24; 5 sections × 5 items. Targets are chosen so the asserted
/// edge is physically reachable (unclamped) unless the row explicitly asserts clamping —
/// the global header/footer live at the content extremes, so every position clamps to the
/// top/bottom edge respectively.
/// </remarks>
public class VirtualScrollScrollToTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Virtual Scroll ScrollTo Tests";
    private const double Tolerance = 2;

    private async Task OpenPageAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("S0I0");
    }

    private async Task ScrollToAsync(string target, string position, bool animated = false)
    {
        await App.FillAsync("TargetEntry", target);
        await App.FillAsync("PositionEntry", position);
        await App.TapAsync(animated ? "ScrollToAnimatedButton" : "ScrollToButton");
    }

    /// <summary>Waits until the element satisfies the expected relation to the scroll viewport.</summary>
    private async Task AssertEdgeAsync(string automationId, string expectedEdge)
    {
        var viewport = await App.GetBoundsAsync("STScroll");

        var _ = expectedEdge switch
        {
            "top" => await App.WaitForBoundsAsync(automationId, b => Math.Abs(b.Y - viewport.Y) <= Tolerance),
            "bottom" => await App.WaitForBoundsAsync(automationId, b => Math.Abs(b.Bottom - viewport.Bottom) <= Tolerance),
            "center" => await App.WaitForBoundsAsync(automationId, b => Math.Abs(b.CenterY - viewport.CenterY) <= Tolerance),
            "visible" => await App.WaitForBoundsAsync(automationId, b => b.Y >= viewport.Y - Tolerance && b.Bottom <= viewport.Bottom + Tolerance),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedEdge))
        };
    }

    [Theory]
    // Items.
    [InlineData("2:2", "Start", "S2I2", "top")]
    [InlineData("2:2", "Center", "S2I2", "center")]
    [InlineData("3:2", "End", "S3I2", "bottom")]
    // Target below the viewport: MakeVisible scrolls minimally, aligning its bottom edge.
    [InlineData("3:2", "MakeVisible", "S3I2", "bottom")]
    // Section headers (itemIndex -1).
    [InlineData("2:h", "Start", "SH2", "top")]
    [InlineData("2:h", "Center", "SH2", "center")]
    [InlineData("4:h", "End", "SH4", "bottom")]
    // Target below the viewport: minimal scroll aligns the header's bottom edge.
    [InlineData("4:h", "MakeVisible", "SH4", "bottom")]
    public async Task ScrollToFromTopPositionsTarget(string target, string position, string automationId, string expectedEdge)
    {
        await OpenPageAsync();

        await ScrollToAsync(target, position);

        await AssertEdgeAsync(automationId, expectedEdge);
    }

    [Theory]
    // From the bottom of the list, targets above the viewport.
    [InlineData("0:2", "MakeVisible", "S0I2", "top")]
    [InlineData("1:h", "MakeVisible", "SH1", "top")]
    [InlineData("1:h", "Start", "SH1", "top")]
    [InlineData("1:2", "Center", "S1I2", "center")]
    public async Task ScrollToFromBottomPositionsTarget(string target, string position, string automationId, string expectedEdge)
    {
        await OpenPageAsync();
        await ScrollToAsync("gf", "End");
        await AssertEdgeAsync("GFooter", "bottom");

        await ScrollToAsync(target, position);

        await AssertEdgeAsync(automationId, expectedEdge);
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("Center")]
    [InlineData("End")]
    [InlineData("MakeVisible")]
    public async Task GlobalHeaderLandsAtContentStart(string position)
    {
        await OpenPageAsync();
        await ScrollToAsync("gf", "End");
        await AssertEdgeAsync("GFooter", "bottom");

        await ScrollToAsync("gh", position);

        // The global header sits at the very start of the content:
        // every position clamps to the top edge of the viewport.
        await AssertEdgeAsync("GHeader", "top");
    }

    [Theory]
    [InlineData("Start")]
    [InlineData("Center")]
    [InlineData("End")]
    [InlineData("MakeVisible")]
    public async Task GlobalFooterLandsAtContentEnd(string position)
    {
        await OpenPageAsync();

        await ScrollToAsync("gf", position);

        // The global footer sits at the very end of the content:
        // every position clamps to the bottom edge of the viewport.
        await AssertEdgeAsync("GFooter", "bottom");
    }

    [Fact]
    public async Task MakeVisibleDoesNotMoveWhenTargetAlreadyVisible()
    {
        await OpenPageAsync();
        await ScrollToAsync("2:2", "Start");
        await AssertEdgeAsync("S2I2", "top");

        // S2I4 (100-150pt below the anchored item) is already fully visible.
        await ScrollToAsync("2:4", "MakeVisible");

        var anchor = await App.WaitForStableBoundsAsync("S2I2");
        var viewport = await App.GetBoundsAsync("STScroll");
        anchor.Y.Should().BeApproximately(viewport.Y, Tolerance, "MakeVisible must not scroll when the target is already fully visible");
    }

    [Fact]
    public async Task AnimatedScrollToAlsoLands()
    {
        await OpenPageAsync();

        await ScrollToAsync("gf", "End", animated: true);
        await AssertEdgeAsync("GFooter", "bottom");

        await ScrollToAsync("2:h", "Start", animated: true);
        await AssertEdgeAsync("SH2", "top");

        await ScrollToAsync("gh", "Start", animated: true);
        await AssertEdgeAsync("GHeader", "top");
    }
}
