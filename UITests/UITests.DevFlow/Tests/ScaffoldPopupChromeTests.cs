using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the Scaffold popup system (§5.6 overlay stack) against the "Scaffold Popup Tests"
/// harness: center popup, anchored dropdown with a transparent scrim, popup-over-popup
/// stacking, and the dismissal policies (close button, scrim tap, navigation-closes-all) —
/// each observed through the handle's Closed task mirrored into the per-popup state labels.
/// </summary>
public class ScaffoldPopupChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Popup Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(_pageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    [Fact]
    public async Task CenterPopupShowsCenteredAndClosesFromContent()
    {
        await WaitDisplayedAsync("PopupHomePage");

        await App.TapAsync("ShowCenterPopupButton");
        await App.WaitForTextAsync("CenterPopupState", "center:open");
        var popup = await App.WaitForStableBoundsAsync("CenterPopupContent");

        // Centered within the fullscreen scrim (the popup area ignores chrome, only system insets).
        var scrim = await App.WaitForStableBoundsAsync("PopupScrim");
        (popup.X + popup.Width / 2).Should().BeApproximately(scrim.X + scrim.Width / 2, 8, "center placement centers the popup");

        await App.TapAsync("CloseCenterPopupButton");
        await App.WaitForTextAsync("CenterPopupState", "center:closed");
    }

    /// <summary>
    /// Regression: on Android the popup content used to be displaced by a system-bar padding at
    /// presentation (MAUI's inset listener evaluating the subtree before it was laid out at its
    /// place) — randomly, depending on frame timing. Repeated presentations must place the
    /// content identically, every child inside the surface, nothing pushed down or cut off.
    /// </summary>
    [Fact]
    public async Task RepeatedPresentationsPlaceTheContentIdentically()
    {
        await WaitDisplayedAsync("PopupHomePage");
        double? firstOffset = null;

        for (var i = 0; i < 8; i++)
        {
            await App.TapAsync("ShowCenterPopupButton");
            await App.WaitForTextAsync("CenterPopupState", "center:open");
            var content = await App.WaitForStableBoundsAsync("CenterPopupContent");
            var close = await App.GetBoundsAsync("CloseCenterPopupButton");
            var navigate = await App.GetBoundsAsync("NavigateFromPopupButton");

            // The first control sits right under the title, the last one inside the surface.
            var offset = close.Y - content.Y;
            offset.Should().BeLessThan(80, $"presentation #{i}: no system-bar padding pushed the content down");
            navigate.Bottom.Should().BeLessThanOrEqualTo(content.Bottom + 1, $"presentation #{i}: the last control is not cut off");

            firstOffset ??= offset;
            offset.Should().BeApproximately(firstOffset.Value, 2, $"presentation #{i}: same placement as the first");

            await App.TapAsync("CloseCenterPopupButton");
            await App.WaitForTextAsync("CenterPopupState", "center:closed");
        }
    }

    [Fact]
    public async Task ScrimTapClosesPopup()
    {
        await WaitDisplayedAsync("PopupHomePage");

        await App.TapAsync("ShowCenterPopupButton");
        await App.WaitForTextAsync("CenterPopupState", "center:open");

        await App.TapAsync("PopupScrim");
        await App.WaitForTextAsync("CenterPopupState", "center:closed");
    }

    [Fact]
    public async Task DropdownAnchorsBelowItsAnchorWithTransparentScrim()
    {
        await WaitDisplayedAsync("PopupHomePage");

        var anchor = await App.WaitForStableBoundsAsync("ShowDropdownButton");

        await App.TapAsync("ShowDropdownButton");
        await App.WaitForTextAsync("DropdownPopupState", "dropdown:open");

        // Dropdown shape: start-aligned with the anchor, right below it.
        var dropdown = await App.WaitForStableBoundsAsync("DropdownContent");
        dropdown.Y.Should().BeApproximately(anchor.Y + anchor.Height, 8, "AnchorBelow places the popup under the anchor");
        dropdown.X.Should().BeApproximately(anchor.X, 8, "AnchorBelow start-aligns with the anchor");

        // The page below stays visible through the transparent scrim, and a scrim tap closes.
        await App.TapAsync("PopupScrim");
        await App.WaitForTextAsync("DropdownPopupState", "dropdown:closed");
    }

    [Fact]
    public async Task PopupsStackAndCloseIndependently()
    {
        await WaitDisplayedAsync("PopupHomePage");

        await App.TapAsync("ShowCenterPopupButton");
        await App.WaitForTextAsync("CenterPopupState", "center:open");

        await App.TapAsync("OpenStackedPopupButton");
        await App.WaitForTextAsync("StackedPopupState", "stacked:open");
        await WaitDisplayedAsync("StackedPopupContent");

        // Closing the TOP popup leaves the one below open and interactive.
        await App.TapAsync("CloseStackedPopupButton");
        await App.WaitForTextAsync("StackedPopupState", "stacked:closed");
        (await App.WaitForElementAsync("CenterPopupContent")).Should().NotBeNull();
        await App.WaitForTextAsync("CenterPopupState", "center:open");

        await App.TapAsync("CloseCenterPopupButton");
        await App.WaitForTextAsync("CenterPopupState", "center:closed");
    }

    [Fact]
    public async Task NavigationClosesAllOpenPopups()
    {
        await WaitDisplayedAsync("PopupHomePage");

        await App.TapAsync("ShowCenterPopupButton");
        await App.WaitForTextAsync("CenterPopupState", "center:open");

        // Engine-routed root selection from within the popup: the navigation commit dismisses
        // the whole overlay stack before presenting the target root.
        await App.TapAsync("NavigateFromPopupButton");
        await WaitDisplayedAsync("PopupOtherPage");
        await App.WaitForTextAsync("CenterPopupState", "center:closed");
    }
}
