using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers <c>PopupPageBase</c>: opening, closing and the <c>CloseOnScrimTapped</c> behavior.
/// </summary>
public class PopupTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Popup Tests";

    [Fact]
    public async Task PopupOpensAndShowsContent()
    {
        await App.OpenTestPageAsync(PageName);

        await App.TapAsync("OpenPopupButton");

        var content = await App.WaitForElementAsync("PopupContentLabel");
        content.IsVisible.Should().BeTrue();
        content.Text.Should().Be("Popup is open");
    }

    [Fact]
    public async Task PopupClosesFromContentButton()
    {
        await App.OpenTestPageAsync(PageName);
        await App.TapAsync("OpenPopupButton");
        await App.WaitForElementAsync("PopupContentLabel");

        await App.TapAsync("ClosePopupButton");

        await App.WaitForElementGoneAsync("PopupContentLabel");

        // Back on the test page.
        (await App.FindElementAsync("OpenPopupButton")).Should().NotBeNull();
    }

    [Fact]
    public async Task ScrimTapClosesPopupWhenEnabled()
    {
        await App.OpenTestPageAsync(PageName);
        await App.TapAsync("OpenPopupButton");
        await App.WaitForElementAsync("PopupContentLabel");

        await App.TapAsync("PopupScrimTapArea");

        await App.WaitForElementGoneAsync("PopupContentLabel");
    }

    [Fact]
    public async Task ScrimTapDoesNotClosePopupWhenDisabled()
    {
        await App.OpenTestPageAsync(PageName);
        await App.TapAsync("OpenStubbornPopupButton");
        await App.WaitForElementAsync("PopupContentLabel");

        await App.TapAsync("PopupScrimTapArea");

        // Give the (unwanted) close a chance to happen, then verify it did not.
        await Task.Delay(1000, TestContext.Current.CancellationToken);
        (await App.FindElementAsync("PopupContentLabel")).Should().NotBeNull("CloseOnScrimTapped=false must keep the popup open");

        await App.TapAsync("ClosePopupButton");
        await App.WaitForElementGoneAsync("PopupContentLabel");
    }
}
