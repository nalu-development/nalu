using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Proof-of-concept DevFlow tests for the VirtualScroll component.
/// Mirrors (and extends) the old Appium suite: the DevFlow agent lets us assert
/// on real MAUI visual-tree state instead of the native accessibility tree.
/// </summary>
public class VirtualScrollListTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Virtual Scroll List Tests";

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
        await App.OpenTestPageAsync(PageName);
        await App.TapAsync("OpenTestPage");

        var header = await App.WaitForElementAsync("HeaderLabel");
        header.IsVisible.Should().BeTrue();
        header.Text.Should().Be("The header");
    }

    [Fact]
    public async Task FirstItemsAreMaterialized()
    {
        await App.OpenTestPageAsync(PageName);
        await App.TapAsync("OpenTestPage");

        var firstItem = await App.WaitForElementAsync("Item 1");
        firstItem.IsVisible.Should().BeTrue();

        var secondItem = await App.WaitForElementAsync("Item 2");
        secondItem.IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task FooterIsVisibleAfterScrollingToEnd()
    {
        await App.OpenTestPageAsync(PageName);
        await App.TapAsync("OpenTestPage");
        await App.WaitForElementAsync("Item 1");

        // The footer may be virtualized out until the list is scrolled to the end.
        var footer = await App.WaitForElementOrDefaultAsync("FooterLabel", TimeSpan.FromSeconds(2));

        for (var attempt = 0; footer is null && attempt < 5; attempt++)
        {
            await App.ScrollAsync(deltaY: 1000);
            footer = await App.WaitForElementOrDefaultAsync("FooterLabel", TimeSpan.FromSeconds(1));
        }

        footer.Should().NotBeNull("the footer should exist once the list is scrolled to the end");
        footer!.IsVisible.Should().BeTrue();
        footer.Text.Should().Be("The footer");
    }
}
