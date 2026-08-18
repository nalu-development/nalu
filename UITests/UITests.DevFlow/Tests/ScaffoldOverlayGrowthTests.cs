using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Overlay content that changes size AFTER presentation (the "Scaffold Overlay Growth Tests"
/// harness): the popup must re-fit and re-center at its new natural size, and a Content-detent
/// bottom sheet must re-resolve its height — for a deferred change (a timer, like an image that
/// finishes loading) and for a change nested below the overlay root (a Grid holding the spacer).
/// </summary>
public class ScaffoldOverlayGrowthTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Overlay Growth Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForBoundsAsync("OverlayGrowthHomePage", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    [Fact]
    public async Task PopupRefitsWhenItsContentGrowsAndShrinks()
    {
        var (windowWidth, windowHeight) = await App.GetWindowSizeAsync();

        await App.TapAsync("ShowGrowingPopupButton");
        var initial = await App.WaitForStableBoundsAsync("GrowingPopupContent");
        (await App.GetPropertyAsync("GrowingPopupState", "Text")).Should().Be("size:40", "the initial bounds must be captured before the deferred growth (1.5 s after load)");

        // Deferred growth (1.5 s after load): the popup follows without any app call.
        await App.WaitForTextAsync("GrowingPopupState", "size:100");
        var deferred = await App.WaitForBoundsAsync("GrowingPopupContent", b => b.Height >= initial.Height + 55, TimeSpan.FromSeconds(5));
        deferred.Height.Should().BeApproximately(initial.Height + 60, 2, "the spacer grew from 40 to 100");
        deferred.CenterY.Should().BeApproximately(windowHeight / 2, 60, "a centered popup re-centers at its new size");

        // Explicit growth (nested spacer): the popup grows again.
        await App.TapAsync("GrowingPopupGrowButton");
        await App.WaitForTextAsync("GrowingPopupState", "size:220");
        var grown = await App.WaitForBoundsAsync("GrowingPopupContent", b => b.Height >= deferred.Height + 115, TimeSpan.FromSeconds(5));
        grown.Height.Should().BeApproximately(deferred.Height + 120, 2);
        grown.CenterX.Should().BeApproximately(windowWidth / 2, 2);

        // Shrink back: the popup follows down as well.
        await App.TapAsync("GrowingPopupShrinkButton");
        await App.WaitForTextAsync("GrowingPopupState", "size:40");
        var shrunk = await App.WaitForBoundsAsync("GrowingPopupContent", b => b.Height <= initial.Height + 2, TimeSpan.FromSeconds(5));
        shrunk.Height.Should().BeApproximately(initial.Height, 2);

        await App.TapAsync("GrowingPopupCloseButton");
        await App.WaitForElementGoneAsync("GrowingPopupContent");
    }

    [Fact]
    public async Task ContentDetentSheetFollowsItsContentSize()
    {
        var (_, windowHeight) = await App.GetWindowSizeAsync();

        await App.TapAsync("ShowGrowingSheetButton");
        var initial = await App.WaitForStableBoundsAsync("ScaffoldBottomSheet");
        (await App.GetPropertyAsync("GrowingSheetState", "Text")).Should().Be("size:40", "the initial bounds must be captured before the deferred growth (1.5 s after load)");
        initial.Bottom.Should().BeApproximately(windowHeight, 1);

        // Deferred growth: the Content detent re-resolves; the sheet stays bottom-anchored.
        await App.WaitForTextAsync("GrowingSheetState", "size:100");
        var deferred = await App.WaitForBoundsAsync("ScaffoldBottomSheet", b => b.Height >= initial.Height + 55, TimeSpan.FromSeconds(5));
        deferred.Height.Should().BeApproximately(initial.Height + 60, 2);
        deferred.Bottom.Should().BeApproximately(windowHeight, 1);

        await App.TapAsync("GrowingSheetGrowButton");
        await App.WaitForTextAsync("GrowingSheetState", "size:220");
        var grown = await App.WaitForBoundsAsync("ScaffoldBottomSheet", b => b.Height >= deferred.Height + 115, TimeSpan.FromSeconds(5));
        grown.Height.Should().BeApproximately(deferred.Height + 120, 2);
        grown.Bottom.Should().BeApproximately(windowHeight, 1);

        // Every control stays inside the sheet surface (nothing is cut off at the bottom).
        var close = await App.GetBoundsAsync("GrowingSheetCloseButton");
        close.Bottom.Should().BeLessThanOrEqualTo(grown.Bottom + 1);

        await App.TapAsync("GrowingSheetShrinkButton");
        await App.WaitForTextAsync("GrowingSheetState", "size:40");
        var shrunk = await App.WaitForBoundsAsync("ScaffoldBottomSheet", b => b.Height <= initial.Height + 2, TimeSpan.FromSeconds(5));
        shrunk.Height.Should().BeApproximately(initial.Height, 2);
        shrunk.Bottom.Should().BeApproximately(windowHeight, 1);

        await App.TapAsync("GrowingSheetCloseButton");
        await App.WaitForElementGoneAsync("ScaffoldBottomSheet");
    }
}
