using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// DevFlow tests for the VirtualScroll component with a flat (single-section) source:
/// rendering, virtualization, ObservableCollection mutations, template selector,
/// ScrollTo, visible range and ItemsSource swapping.
/// </summary>
public class VirtualScrollListTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Virtual Scroll List Tests";

    /// <summary>Opens the inner test page (a fresh instance per call: state never leaks between tests).</summary>
    private async Task OpenPageAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.TapAsync("OpenTestPage");
        await App.WaitForElementAsync("Item 1");
    }

    [Fact]
    public async Task TestPageOpens()
    {
        await App.OpenTestPageAsync(PageName);

        var openButton = await App.WaitForElementAsync("OpenTestPage");
        openButton.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task HeaderIsVisible()
    {
        await OpenPageAsync();

        var header = await App.WaitForElementAsync("HeaderLabel");
        header.IsVisible.Should().BeTrue();
        header.Text.Should().Be("The header");
    }

    [Fact]
    public async Task FirstItemsAreMaterialized()
    {
        await OpenPageAsync();

        var firstItem = await App.WaitForElementAsync("Item 1");
        firstItem.IsVisible.Should().BeTrue();

        var secondItem = await App.WaitForElementAsync("Item 2");
        secondItem.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task ItemsAreLaidOutInOrder()
    {
        await OpenPageAsync();

        var first = await App.GetBoundsAsync("Item 1");
        var second = await App.GetBoundsAsync("Item 2");

        second.Y.Should().BeGreaterThan(first.Y, "Item 2 must be laid out below Item 1");
        second.X.Should().Be(first.X);
    }

    [Fact]
    public async Task OffscreenItemsAreNotMaterialized()
    {
        await OpenPageAsync();

        // Grow the list to 110 items: far-away items must not exist in the visual tree.
        await App.TapAsync("AddManyItemsButton");
        await App.WaitForTextAsync("ItemCountLabel", "Count: 110");

        var farItem = await App.WaitForElementOrDefaultAsync("Item 95", TimeSpan.FromSeconds(2));
        farItem.Should().BeNull("item 95 is far outside the viewport and must be virtualized out");
    }

    [Fact]
    public async Task ScrollToMaterializesTargetItem()
    {
        await OpenPageAsync();

        await App.TapAsync("AddManyItemsButton");
        await App.WaitForTextAsync("ItemCountLabel", "Count: 110");

        await App.FillAsync("PositionEntry", "94");
        await App.FillAsync("ExtraEntry", "Start");
        await App.TapAsync("ScrollToItemButton");

        var item = await App.WaitForElementAsync("Item 95");
        item.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task ScrollToStartPositionsItemAtViewportTop()
    {
        await OpenPageAsync();

        await App.TapAsync("AddManyItemsButton");
        await App.WaitForTextAsync("ItemCountLabel", "Count: 110");

        await App.FillAsync("PositionEntry", "49");
        await App.FillAsync("ExtraEntry", "Start");
        await App.TapAsync("ScrollToItemButton");
        await App.WaitForElementAsync("Item 50");

        var scrollBounds = await App.GetBoundsAsync("ListScroll");
        var itemBounds = await App.WaitForBoundsAsync("Item 50", b => Math.Abs(b.Y - scrollBounds.Y) < 30);

        // The label has a 10-unit margin inside its cell; the cell itself must sit at the top.
        itemBounds.Y.Should().BeApproximately(scrollBounds.Y + 10, 15);
    }

    [Fact]
    public async Task FooterIsVisibleAfterScrollingToEnd()
    {
        await OpenPageAsync();

        // The footer is virtualized out until the list is scrolled to the end.
        await App.FillAsync("PositionEntry", "19");
        await App.FillAsync("ExtraEntry", "End");
        await App.TapAsync("ScrollToItemButton");

        // Reaching the last item leaves the footer just outside the viewport: one swipe completes it.
        var footer = await App.WaitForElementOrDefaultAsync("FooterLabel", TimeSpan.FromSeconds(2));

        for (var attempt = 0; footer is null && attempt < 5; attempt++)
        {
            await App.SwipeAsync("ListScroll", "up", 400);
            footer = await App.WaitForElementOrDefaultAsync("FooterLabel", TimeSpan.FromSeconds(1));
        }

        footer.Should().NotBeNull("the footer should exist once the list is scrolled to the end");
        footer!.IsVisible.Should().BeTrue();
        footer.Text.Should().Be("The footer");
    }

    [Fact]
    public async Task SwipeScrollsTheList()
    {
        await OpenPageAsync();

        await App.SwipeAsync("ListScroll", "up", 400);

        var item20 = await App.WaitForElementAsync("Item 20");
        item20.IsVisible.Should().BeTrue("swiping up must reveal the bottom of the 20-item list");
    }

    [Fact]
    public async Task AddItemInsertsAtPosition()
    {
        await OpenPageAsync();

        await App.FillAsync("PositionEntry", "0");
        await App.FillAsync("ExtraEntry", "Added");
        await App.TapAsync("AddItemButton");

        await App.WaitForTextAsync("ItemCountLabel", "Count: 21");
        var added = await App.WaitForElementAsync("Added");
        var item1 = await App.GetBoundsAsync("Item 1");

        (await App.GetBoundsAsync("Added")).Y.Should().BeLessThan(item1.Y, "the new item was inserted at position 0");
        added.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task TemplateSelectorPicksSpecialTemplate()
    {
        await OpenPageAsync();

        await App.FillAsync("PositionEntry", "0");
        await App.FillAsync("ExtraEntry", "Special 1");
        await App.TapAsync("AddItemButton");

        // The special template renders with a star prefix.
        await App.WaitForTextAsync("Special 1", "★ Special 1");
    }

    [Fact]
    public async Task RemoveItemRemovesIt()
    {
        await OpenPageAsync();

        var item1Before = await App.GetBoundsAsync("Item 1");

        await App.FillAsync("PositionEntry", "0");
        await App.TapAsync("RemoveItemButton");

        await App.WaitForTextAsync("ItemCountLabel", "Count: 19");

        // Recycled cells linger in the visual tree, so "element gone" cannot be asserted:
        // the surviving items shifting up proves the removal reached the UI.
        await App.WaitForBoundsAsync("Item 2", b => Math.Abs(b.Y - item1Before.Y) < 2);
    }

    [Fact]
    public async Task MoveItemChangesItsPosition()
    {
        await OpenPageAsync();

        // Move "Item 1" from position 0 to position 9.
        await App.FillAsync("PositionEntry", "0");
        await App.FillAsync("ExtraEntry", "9");
        await App.TapAsync("SwapItemButton");

        var item1 = await App.WaitForBoundsAsync("Item 1", b => b.Y > 0);
        var item2 = await App.GetBoundsAsync("Item 2");

        item1.Y.Should().BeGreaterThan(item2.Y, "Item 1 was moved below Item 2");
    }

    [Fact]
    public async Task ReplaceItemSwapsContent()
    {
        await OpenPageAsync();

        var item1Before = await App.GetBoundsAsync("Item 1");

        await App.FillAsync("PositionEntry", "0");
        await App.FillAsync("ExtraEntry", "Replaced");
        await App.TapAsync("ReplaceItemButton");

        // The replacement takes the replaced item's slot; the count is unchanged.
        await App.WaitForBoundsAsync("Replaced", b => Math.Abs(b.Y - item1Before.Y) < 2);
        await App.WaitForTextAsync("ItemCountLabel", "Count: 20");
    }

    [Fact]
    public async Task ClearRemovesAllItemsButKeepsHeaderAndFooter()
    {
        await OpenPageAsync();

        await App.TapAsync("ClearItemsButton");

        await App.WaitForTextAsync("ItemCountLabel", "Count: 0");

        // With no items left, the footer moves up right below the header
        // (recycled item cells linger in the tree, so "gone" cannot be asserted).
        var header = await App.GetBoundsAsync("HeaderLabel");
        await App.WaitForBoundsAsync("FooterLabel", b => b.Y - header.Bottom < 10);
    }

    [Fact]
    public async Task AddAfterClearShowsItem()
    {
        await OpenPageAsync();

        await App.TapAsync("ClearItemsButton");
        await App.WaitForTextAsync("ItemCountLabel", "Count: 0");

        await App.FillAsync("ExtraEntry", "Fresh");
        await App.TapAsync("AddItemButton");

        var fresh = await App.WaitForElementAsync("Fresh");
        fresh.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task SwitchingToStaticSourceRendersNewItems()
    {
        await OpenPageAsync();

        // A plain List (non-observable) goes through the static list adapter.
        await App.TapAsync("SwitchSourceButton");

        var staticItem = await App.WaitForElementAsync("Static 1");
        staticItem.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task VisibleRangeStartsAtGlobalHeader()
    {
        await OpenPageAsync();

        await App.TapAsync("VisibleRangeButton");

        var range = await App.WaitForTextMatchAsync("VisibleRangeLabel", t => t is not null && t != "-");
        range.Should().StartWith("GH:", "the global header is visible at the top of the list");
    }

    [Fact]
    public async Task VisibleRangeEndsAtGlobalFooterAfterScrollToEnd()
    {
        await OpenPageAsync();

        await App.FillAsync("PositionEntry", "19");
        await App.FillAsync("ExtraEntry", "End");
        await App.TapAsync("ScrollToItemButton");

        // The swipe settles asynchronously (bounce): keep nudging until the footer is in range.
        string? range = null;

        for (var attempt = 0; attempt < 6 && range?.EndsWith("GF:0") != true; attempt++)
        {
            await App.SwipeAsync("ListScroll", "up", 300);

            if (await App.WaitForElementOrDefaultAsync("FooterLabel", TimeSpan.FromSeconds(1)) is not null)
            {
                await App.WaitForStableBoundsAsync("FooterLabel", TimeSpan.FromSeconds(3));
            }

            await App.TapAsync("VisibleRangeButton");
            range = await App.WaitForTextMatchAsync("VisibleRangeLabel", t => t is not null && t != "-");
        }

        range.Should().EndWith("GF:0", "the global footer is visible at the bottom of the list");
    }
}
