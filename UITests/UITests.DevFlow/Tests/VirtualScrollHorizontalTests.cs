using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// DevFlow tests for VirtualScroll with the horizontal linear layout,
/// including the fading edge effect.
/// </summary>
public class VirtualScrollHorizontalTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Virtual Scroll Horizontal Tests";

    private async Task OpenPageAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("H1");
    }

    [Fact]
    public async Task ItemsFlowHorizontally()
    {
        await OpenPageAsync();

        var header = await App.GetBoundsAsync("HHeader");
        var first = await App.GetBoundsAsync("H1");
        var second = await App.GetBoundsAsync("H2");

        first.X.Should().BeGreaterThan(header.X, "the header leads the items");
        second.X.Should().BeGreaterThan(first.X, "items flow left to right");
        second.Y.Should().Be(first.Y);
    }

    [Fact]
    public async Task OffscreenItemsAreNotMaterialized()
    {
        await OpenPageAsync();

        var farItem = await App.WaitForElementOrDefaultAsync("H20", TimeSpan.FromSeconds(2));
        farItem.Should().BeNull("item 20 is outside the horizontal viewport");
    }

    [Fact]
    public async Task ScrollToEndShowsLastItemAndFooter()
    {
        await OpenPageAsync();

        await App.TapAsync("ScrollToEndButton");
        (await App.WaitForElementAsync("H30")).IsVisible.Should().BeTrue();

        // ScrollTo(End) aligns the last ITEM with the right edge: the footer sits just
        // outside; one extra forward swipe reveals it ("right" scrolls forward, see SwipeAsync).
        var footer = await App.WaitForElementOrDefaultAsync("HFooter", TimeSpan.FromSeconds(2));

        for (var attempt = 0; footer is null && attempt < 5; attempt++)
        {
            await App.SwipeAsync("HScroll", "right", 200);
            footer = await App.WaitForElementOrDefaultAsync("HFooter", TimeSpan.FromSeconds(1));
        }

        footer.Should().NotBeNull();
        footer!.Text.Should().Be("H-Foot");
    }

    [Fact]
    public async Task ScrollBackToStartShowsHeaderAgain()
    {
        await OpenPageAsync();

        await App.TapAsync("ScrollToEndButton");
        await App.WaitForElementAsync("H30");

        await App.TapAsync("ScrollToStartButton");
        (await App.WaitForElementAsync("H1")).IsVisible.Should().BeTrue();
        (await App.WaitForElementAsync("HHeader")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task SwipeScrollsHorizontally()
    {
        await OpenPageAsync();

        // "right" scrolls forward on horizontal lists (see SwipeAsync direction semantics).
        var revealed = await App.WaitForElementOrDefaultAsync("H8", TimeSpan.FromSeconds(1));

        for (var attempt = 0; revealed is null && attempt < 5; attempt++)
        {
            await App.SwipeAsync("HScroll", "right", 200);
            revealed = await App.WaitForElementOrDefaultAsync("H8", TimeSpan.FromSeconds(1));
        }

        revealed.Should().NotBeNull("swiping forward must reveal items further right");
    }

    [Fact]
    public async Task FadingEdgeChangesEdgePixels()
    {
        await OpenPageAsync();

        // At scroll offset 0 only the TRAILING (right) edge fades: sample near it, fading off.
        var bounds = await App.GetBoundsAsync("HScroll");
        var sampleY = bounds.Height / 2;
        var trailingX = bounds.Width - 4;
        var trailingBefore = await App.GetPixelColorAsync("HScroll", trailingX, sampleY);
        var leadingBefore = await App.GetPixelColorAsync("HScroll", 4, sampleY);
        var centerBefore = await App.GetPixelColorAsync("HScroll", bounds.Width / 2, sampleY);

        await App.TapAsync("ToggleFadingButton");
        await App.WaitForTextAsync("FadingStateLabel", "Fading: 60");

        // The faded edge blends the item color towards the background: the pixel must change.
        await App.WaitForPixelColorAsync(
            "HScroll", trailingX, sampleY,
            c => Delta(c, trailingBefore) > 30);

        // The leading edge (nothing scrolled past yet) and the center must be unaffected.
        Delta(await App.GetPixelColorAsync("HScroll", 4, sampleY), leadingBefore)
            .Should().BeLessThan(30, "there is no leading fade at scroll offset 0");
        Delta(await App.GetPixelColorAsync("HScroll", bounds.Width / 2, sampleY), centerBefore)
            .Should().BeLessThan(30, "the fading edge must not affect the center of the viewport");

        // Toggling back restores the edge.
        await App.TapAsync("ToggleFadingButton");
        await App.WaitForTextAsync("FadingStateLabel", "Fading: 0");
        await App.WaitForPixelColorAsync(
            "HScroll", trailingX, sampleY,
            c => Delta(c, trailingBefore) < 20);

        static int Delta((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
            => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
    }
}
