using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// DevFlow tests for VirtualScroll with carousel layouts (paged, full-viewport items)
/// and the CarouselVirtualScrollLayout.CurrentRange attached property.
/// </summary>
/// <remarks>
/// User-swipe paging cannot be tested: DevFlow synthetic swipes move the scroll offset
/// without real touch physics, so the platform paging snap never engages.
/// Navigation is exercised through the CurrentRange attached property instead.
/// </remarks>
public class VirtualScrollCarouselTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Virtual Scroll Carousel Tests";

    private async Task OpenPageAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("Carousel1");
    }

    /// <summary>Asserts that the given page is horizontally centered in the carousel viewport.</summary>
    private async Task AssertPageCenteredAsync(string automationId, bool horizontally = true)
    {
        var scroll = await App.GetBoundsAsync("CarouselScroll");

        if (horizontally)
        {
            await App.WaitForBoundsAsync(automationId, b => Math.Abs(b.CenterX - scroll.CenterX) < 5);
        }
        else
        {
            await App.WaitForBoundsAsync(automationId, b => Math.Abs(b.CenterY - scroll.CenterY) < 5);
        }
    }

    [Fact]
    public async Task FirstPageFillsViewport()
    {
        await OpenPageAsync();

        await AssertPageCenteredAsync("Carousel1");

        // The second page must not be inside the viewport.
        var scroll = await App.GetBoundsAsync("CarouselScroll");
        var second = await App.WaitForElementOrDefaultAsync("Carousel2", TimeSpan.FromSeconds(1));

        if (second is not null)
        {
            var secondBounds = await App.GetBoundsAsync("Carousel2");
            secondBounds.X.Should().BeGreaterThanOrEqualTo(scroll.Right - 1, "page 2 must be offscreen to the right");
        }
    }

    [Fact]
    public async Task NextAdvancesPages()
    {
        await OpenPageAsync();

        await App.TapAsync("NextPageButton");
        await App.WaitForTextAsync("CarouselRangeLabel", "1");
        await AssertPageCenteredAsync("Carousel2");

        await App.TapAsync("NextPageButton");
        await App.WaitForTextAsync("CarouselRangeLabel", "2");
        await AssertPageCenteredAsync("Carousel3");
    }

    [Fact]
    public async Task PrevGoesBack()
    {
        await OpenPageAsync();

        await App.TapAsync("NextPageButton");
        await App.WaitForTextAsync("CarouselRangeLabel", "1");

        await App.TapAsync("PrevPageButton");
        await App.WaitForTextAsync("CarouselRangeLabel", "0");
        await AssertPageCenteredAsync("Carousel1");
    }

    [Fact]
    public async Task NavigationClampsAtBothEnds()
    {
        await OpenPageAsync();

        await App.TapAsync("PrevPageButton");
        await App.WaitForTextAsync("CarouselRangeLabel", "0");
        await AssertPageCenteredAsync("Carousel1");

        for (var i = 0; i < 6; i++)
        {
            await App.TapAsync("NextPageButton");
        }

        await App.WaitForTextAsync("CarouselRangeLabel", "4");
        await AssertPageCenteredAsync("Carousel5");
    }

    [Fact]
    public async Task SwitchingToVerticalCarouselKeepsWorking()
    {
        await OpenPageAsync();

        await App.TapAsync("SwitchToVerticalButton");

        // Runtime ItemsLayout swap: paging keeps working, now vertically.
        await App.TapAsync("NextPageButton");
        await App.WaitForTextAsync("CarouselRangeLabel", "1");
        await AssertPageCenteredAsync("Carousel2", horizontally: false);

        // And back to horizontal.
        await App.TapAsync("SwitchToHorizontalButton");
        await App.TapAsync("NextPageButton");
        await App.WaitForTextAsync("CarouselRangeLabel", "2");
        await AssertPageCenteredAsync("Carousel3");
    }
}
