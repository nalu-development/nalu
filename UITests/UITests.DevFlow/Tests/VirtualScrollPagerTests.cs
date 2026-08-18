using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers VirtualScroll inside a PAGER — pages laid out side by side, brought into view by
/// scrolling — against the "Virtual Scroll Pager Tests" harness.
/// </summary>
/// <remarks>
/// Regression guard for github.com/nalu-development/nalu/issues/187: the Android positional
/// safe-area self-padding measured how far the list's REST footprint reached into each inset
/// band without clamping it to the band's thickness. Page N of a pager rests at N * pageWidth,
/// so the right band "overlap" came out as whole page widths — a padding that swallowed the
/// whole content box and left every cell zero-wide (reported with Telerik's RadTabView, which
/// lays its tabs out exactly this way).
/// </remarks>
public class VirtualScrollPagerTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Virtual Scroll Pager Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(_pageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task ItemsFillThePageWidthOnEveryPage(int pageIndex)
    {
        await App.WaitForElementAsync("Pager");
        var pagerWidth = (await App.WaitForBoundsAsync("Pager", b => b.Width > 0)).Width;

        await App.TapAsync($"PagerGoPage{pageIndex}");
        await App.WaitForTextAsync("PagerStateLabel", $"Page {pageIndex}");

        // The page is scrolled into view: its cells must span the viewport, not collapse to a
        // sliver. Before the fix the off-screen pages ended up with zero-wide (hence invisible)
        // cells while the first page — resting at x=0 — was fine.
        var bounds = await App.WaitForBoundsAsync(
            $"Pager{pageIndex}Item0",
            b => b.Width > pagerWidth * 0.9
        );

        // ...and it must be the VISIBLE page, i.e. actually inside the window.
        bounds.X.Should().BeInRange(-1, pagerWidth * 0.1);
    }

    [Fact]
    public async Task PagesKeepTheirItemsAfterPagingBackAndForth()
    {
        await App.WaitForElementAsync("Pager");
        var pagerWidth = (await App.WaitForBoundsAsync("Pager", b => b.Width > 0)).Width;

        foreach (var pageIndex in (int[]) [2, 0, 1, 0, 2])
        {
            await App.TapAsync($"PagerGoPage{pageIndex}");
            await App.WaitForTextAsync("PagerStateLabel", $"Page {pageIndex}");

            await App.WaitForBoundsAsync(
                $"Pager{pageIndex}Item0",
                b => b.Width > pagerWidth * 0.9
            );
        }
    }
}
