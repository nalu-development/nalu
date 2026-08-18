using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers <c>HorizontalWrapLayout</c> / <c>VerticalWrapLayout</c>: wrapping, spacing,
/// <c>ItemsAlignment</c> and the expand system (<c>ExpandRatio</c> + <c>ExpandMode</c>).
/// </summary>
public class WrapLayoutTests(NaluApp app) : BaseUiTest(app)
{
    private const string _pageName = "Wrap Layout Tests";
    private const double _tolerance = 1.5;

    [Fact]
    public async Task HorizontalLayoutWrapsToNextRowWithSpacing()
    {
        await App.OpenTestPageAsync(_pageName);

        var item1 = await App.GetBoundsAsync("HWrapItem1");
        var item2 = await App.GetBoundsAsync("HWrapItem2");
        var item3 = await App.GetBoundsAsync("HWrapItem3");

        // Items 1 and 2 share the first row, separated by HorizontalSpacing=10.
        item2.Y.Should().BeApproximately(item1.Y, _tolerance);
        (item2.X - item1.Right).Should().BeApproximately(10, _tolerance);

        // Item 3 does not fit (250 + 10 + 120 > 320) and wraps to a second row,
        // left-aligned and separated by VerticalSpacing=8.
        item3.X.Should().BeApproximately(item1.X, _tolerance);
        (item3.Y - item1.Bottom).Should().BeApproximately(8, _tolerance);
    }

    [Fact]
    public async Task ItemsAlignmentPositionsItemsWithinTheRow()
    {
        await App.OpenTestPageAsync(_pageName);

        // 320-wide layouts, single 100-wide item: remaining space is 220.
        var startLayout = await App.GetBoundsAsync("AlignStartLayout");
        var startItem = await App.GetBoundsAsync("AlignStartItem");
        (startItem.X - startLayout.X).Should().BeApproximately(0, _tolerance);

        var centerLayout = await App.GetBoundsAsync("AlignCenterLayout");
        var centerItem = await App.GetBoundsAsync("AlignCenterItem");
        (centerItem.X - centerLayout.X).Should().BeApproximately(110, _tolerance);

        var endLayout = await App.GetBoundsAsync("AlignEndLayout");
        var endItem = await App.GetBoundsAsync("AlignEndItem");
        (endItem.X - endLayout.X).Should().BeApproximately(220, _tolerance);
    }

    [Fact]
    public async Task DistributeAddsRemainingSpaceEvenly()
    {
        await App.OpenTestPageAsync(_pageName);

        // 320 wide, spacing 10, natural widths 60+60: remaining 190 -> 95 extra each.
        var itemA = await App.GetBoundsAsync("DistItemA");
        var itemB = await App.GetBoundsAsync("DistItemB");

        itemA.Width.Should().BeApproximately(155, _tolerance);
        itemB.Width.Should().BeApproximately(155, _tolerance);

        var layout = await App.GetBoundsAsync("DistributeLayout");
        itemB.Right.Should().BeApproximately(layout.Right, _tolerance, "the row must fill the layout width");
    }

    [Fact]
    public async Task DistributeProportionallyScalesWithItemSize()
    {
        await App.OpenTestPageAsync(_pageName);

        // 300 wide, natural widths 50+100: remaining 150 split by size -> 100 and 200.
        var itemA = await App.GetBoundsAsync("PropItemA");
        var itemB = await App.GetBoundsAsync("PropItemB");

        itemA.Width.Should().BeApproximately(100, _tolerance);
        itemB.Width.Should().BeApproximately(200, _tolerance);
    }

    [Fact]
    public async Task DivideAssignsEqualShares()
    {
        await App.OpenTestPageAsync(_pageName);

        // 300 wide, natural widths 50+100: Divide replaces sizes -> 150 each.
        var itemA = await App.GetBoundsAsync("DivItemA");
        var itemB = await App.GetBoundsAsync("DivItemB");

        itemA.Width.Should().BeApproximately(150, _tolerance);
        itemB.Width.Should().BeApproximately(150, _tolerance);
    }

    [Fact]
    public async Task VerticalLayoutWrapsToNextColumnWithSpacing()
    {
        await App.OpenTestPageAsync(_pageName);

        var item1 = await App.GetBoundsAsync("VWrapItem1");
        var item2 = await App.GetBoundsAsync("VWrapItem2");
        var item3 = await App.GetBoundsAsync("VWrapItem3");

        // Items 1 and 2 share the first column, separated by VerticalSpacing=10.
        item2.X.Should().BeApproximately(item1.X, _tolerance);
        (item2.Y - item1.Bottom).Should().BeApproximately(10, _tolerance);

        // Item 3 does not fit (130 + 10 + 60 > 150) and wraps to a second column,
        // top-aligned and separated by HorizontalSpacing=12.
        item3.Y.Should().BeApproximately(item1.Y, _tolerance);
        (item3.X - item1.Right).Should().BeApproximately(12, _tolerance);
    }
}
