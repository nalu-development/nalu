using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// VirtualScroll items hosting an <c>ExpanderViewBox</c>: expanding must grow the CELL in place
/// (the content's measure invalidation has to reach the collection view), pushing the following
/// item down; collapsing must bring it back. This guards the measure-invalidation signal path
/// (see the July 2026 self-sizing livelock fix in the Apple VirtualScroll platform code).
/// </summary>
public class VirtualScrollExpanderTests(NaluApp app) : BaseUiTest(app)
{
    private const string _pageName = "Virtual Scroll Expander Tests";

    [Fact]
    public async Task ExpandingAnItemGrowsItsCellAndPushesFollowingItemsDown()
    {
        await App.OpenTestPageAsync(_pageName);

        // Collapsed: the expander is capped at CollapsedHeight (60).
        var collapsed = await App.WaitForStableBoundsAsync("Expander E1");
        collapsed.Height.Should().BeApproximately(60, 1.5);

        var nextBefore = await App.WaitForStableBoundsAsync("Toggle E2");

        await App.TapAsync("Toggle E1");

        // Expanded: the content (label + 240 box) exceeds 240; the cell must grow with it,
        // pushing item 2 down by the same delta.
        var expanded = await App.WaitForBoundsAsync("Expander E1", b => b.Height > 220, TimeSpan.FromSeconds(5));
        var nextExpanded = await App.WaitForBoundsAsync("Toggle E2", b => b.Y > nextBefore.Y + 150, TimeSpan.FromSeconds(5));
        (nextExpanded.Y - nextBefore.Y).Should().BeApproximately(expanded.Height - collapsed.Height, 2.5);

        // Collapse again: the following item returns to its original position.
        await App.TapAsync("Toggle E1");

        await App.WaitForBoundsAsync("Expander E1", b => b.Height < 65, TimeSpan.FromSeconds(5));
        var nextRestored = await App.WaitForBoundsAsync("Toggle E2", b => b.Y < nextBefore.Y + 5, TimeSpan.FromSeconds(5));
        nextRestored.Y.Should().BeApproximately(nextBefore.Y, 2.5);
    }
}
