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
        // A single swipe travels a platform-dependent distance: keep swiping until revealed.
        var revealed = await App.WaitForElementOrDefaultAsync("H8", TimeSpan.FromSeconds(1));

        for (var attempt = 0; revealed is null && attempt < 10; attempt++)
        {
            await App.SwipeAsync("HScroll", "right", 300);
            revealed = await App.WaitForElementOrDefaultAsync("H8", TimeSpan.FromSeconds(1));
        }

        revealed.Should().NotBeNull("swiping forward must reveal items further right");
    }

    [Fact]
    public async Task FadingEdgeChangesEdgePixels()
    {
        await OpenPageAsync();

        // Differential sampling: compare a pixel deep inside the 60-unit trailing fade zone
        // with one outside it, in the SAME state. This is robust across machines: no
        // absolute colors (theme-independent), no viewport-relative positions that could
        // land on text glyphs (y=12 is above the vertically-centered item text), and no
        // cross-screenshot comparisons of the same point.
        var bounds = await App.GetBoundsAsync("HScroll");
        const double sampleY = 12;
        var edgeX = bounds.Width - 3;
        var innerX = bounds.Width - 90;

        // Fading off: the trailing area is uniform item background.
        var innerColor = await App.GetPixelColorAsync("HScroll", innerX, sampleY);
        Delta(await App.GetPixelColorAsync("HScroll", edgeX, sampleY), innerColor)
            .Should().BeLessThan(25, "without fading the trailing area is uniform");

        await App.TapAsync("ToggleFadingButton");
        await App.WaitForTextAsync("FadingStateLabel", "Fading: 60");

        // Fading on: the edge pixel blends towards the page background, the inner one does not.
        await App.WaitForPixelColorAsync(
            "HScroll", edgeX, sampleY,
            c => Delta(c, innerColor) > 40);
        Delta(await App.GetPixelColorAsync("HScroll", innerX, sampleY), innerColor)
            .Should().BeLessThan(25, "the fade must not affect pixels outside the fade zone");

        // At scroll offset 0 there is no LEADING fade: the header area stays uniform.
        var leadingInner = await App.GetPixelColorAsync("HScroll", 40, sampleY);
        Delta(await App.GetPixelColorAsync("HScroll", 3, sampleY), leadingInner)
            .Should().BeLessThan(25, "there is no leading fade at scroll offset 0");

        // Toggling back restores a uniform trailing edge.
        await App.TapAsync("ToggleFadingButton");
        await App.WaitForTextAsync("FadingStateLabel", "Fading: 0");
        await App.WaitForPixelColorAsync(
            "HScroll", edgeX, sampleY,
            c => Delta(c, innerColor) < 25);

        static int Delta((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
            => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
    }
}
