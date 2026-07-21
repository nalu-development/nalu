using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// DevFlow tests for VirtualScroll with a grouped (multi-section) observable adapter:
/// section headers/footers, global header/footer, section-level mutations and
/// object-based ScrollTo overloads.
/// </summary>
public class VirtualScrollGroupedTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Virtual Scroll Grouped Tests";

    private async Task OpenPageAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("A1");
    }

    [Fact]
    public async Task SectionsRenderWithHeadersFootersAndItems()
    {
        await OpenPageAsync();

        (await App.WaitForElementAsync("GroupedHeader")).Text.Should().Be("Grouped header");
        (await App.WaitForElementAsync("Header A")).Text.Should().Be("Section A");
        (await App.WaitForElementAsync("A1")).IsVisible.Should().BeTrue();
        (await App.WaitForElementAsync("Footer A")).Text.Should().Be("End of A");

        // Order: global header, then section header, items, section footer.
        var globalHeader = await App.GetBoundsAsync("GroupedHeader");
        var sectionHeader = await App.GetBoundsAsync("Header A");
        var firstItem = await App.GetBoundsAsync("A1");
        var sectionFooter = await App.GetBoundsAsync("Footer A");

        sectionHeader.Y.Should().BeGreaterThan(globalHeader.Y);
        firstItem.Y.Should().BeGreaterThan(sectionHeader.Y);
        sectionFooter.Y.Should().BeGreaterThan(firstItem.Y);
    }

    [Fact]
    public async Task FarSectionsAreNotMaterialized()
    {
        await OpenPageAsync();

        var farHeader = await App.WaitForElementOrDefaultAsync("Header E", TimeSpan.FromSeconds(2));
        farHeader.Should().BeNull("section E is outside the viewport and must be virtualized out");
    }

    [Fact]
    public async Task ScrollToSectionAlignsHeaderToViewportTop()
    {
        await OpenPageAsync();

        await App.FillAsync("SectionEntry", "C");
        await App.TapAsync("ScrollToSectionButton");

        // The two-phase section scroll settles asynchronously (~100ms after the jump).
        var scrollBounds = await App.GetBoundsAsync("GroupedScroll");
        var headerBounds = await App.WaitForBoundsAsync("Header C", b => Math.Abs(b.Y - scrollBounds.Y) < 5);

        headerBounds.Y.Should().BeApproximately(scrollBounds.Y, 5);
    }

    [Fact]
    public async Task ScrollToLastSectionClampsToEnd()
    {
        await OpenPageAsync();

        await App.FillAsync("SectionEntry", "E");
        await App.TapAsync("ScrollToSectionButton");

        var header = await App.WaitForElementAsync("Header E");
        header.IsVisible.Should().BeTrue();

        // At maximum scroll the global footer is on screen as well.
        (await App.WaitForElementAsync("GroupedFooter")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task ScrollToItemObjectBringsItIntoView()
    {
        await OpenPageAsync();

        await App.FillAsync("NameEntry", "D3");
        await App.TapAsync("ScrollToItemButton");

        var item = await App.WaitForElementAsync("D3");
        item.IsVisible.Should().BeTrue();

        var scrollBounds = await App.GetBoundsAsync("GroupedScroll");
        var itemBounds = await App.GetBoundsAsync("D3");
        itemBounds.Y.Should().BeInRange(scrollBounds.Y - 5, scrollBounds.Bottom, "the item must be inside the viewport");
    }

    [Fact]
    public async Task AddSectionAppendsIt()
    {
        await OpenPageAsync();

        await App.FillAsync("SectionEntry", "X");
        await App.TapAsync("AddSectionButton");
        await App.WaitForTextAsync("SectionCountLabel", "Sections: 6");

        // The new section is appended at the end: scroll to it to materialize it.
        await App.TapAsync("ScrollToSectionButton");

        (await App.WaitForElementAsync("Header X")).Text.Should().Be("Section X");
        (await App.WaitForElementAsync("X1")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveSectionRemovesHeaderAndItems()
    {
        await OpenPageAsync();

        var headerABefore = await App.GetBoundsAsync("Header A");

        await App.FillAsync("SectionEntry", "A");
        await App.TapAsync("RemoveSectionButton");

        await App.WaitForTextAsync("SectionCountLabel", "Sections: 4");

        // Recycled cells linger in the tree: section B taking section A's place
        // proves the removal reached the UI.
        await App.WaitForBoundsAsync("Header B", b => Math.Abs(b.Y - headerABefore.Y) < 2);
        (await App.WaitForElementAsync("B1")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task AddItemToSectionShowsIt()
    {
        await OpenPageAsync();

        await App.FillAsync("SectionEntry", "A");
        await App.FillAsync("NameEntry", "A9");
        await App.TapAsync("AddItemButton");

        var item = await App.WaitForElementAsync("A9");
        item.IsVisible.Should().BeTrue();

        // Appended to section A: it must sit between A5 and the section footer.
        var a5 = await App.GetBoundsAsync("A5");
        var a9 = await App.GetBoundsAsync("A9");
        a9.Y.Should().BeGreaterThan(a5.Y);
    }

    [Fact]
    public async Task RemoveItemFromSectionRemovesIt()
    {
        await OpenPageAsync();

        var a2Before = await App.GetBoundsAsync("A2");

        await App.FillAsync("SectionEntry", "A");
        await App.FillAsync("NameEntry", "A2");
        await App.TapAsync("RemoveItemButton");

        // A3 moves up into A2's slot (recycled cells linger in the tree).
        await App.WaitForBoundsAsync("A3", b => Math.Abs(b.Y - a2Before.Y) < 2);
        (await App.WaitForElementAsync("A1")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task AddManySectionsAndScrollToOne()
    {
        await OpenPageAsync();

        await App.TapAsync("AddManySectionsButton");
        await App.WaitForTextAsync("SectionCountLabel", "Sections: 25");

        await App.FillAsync("SectionEntry", "S15");
        await App.TapAsync("ScrollToSectionButton");

        var scrollBounds = await App.GetBoundsAsync("GroupedScroll");
        var headerBounds = await App.WaitForBoundsAsync("Header S15", b => Math.Abs(b.Y - scrollBounds.Y) < 5);
        headerBounds.Y.Should().BeApproximately(scrollBounds.Y, 5);

        (await App.WaitForElementAsync("S151")).IsVisible.Should().BeTrue();
    }
}
