using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the Scaffold bottom sheet (§5.6 overlay stack) against the "Scaffold Sheet Tests"
/// harness: content-hugging sheet, two-detent sheet with programmatic snapping (DevFlow's
/// synthetic gestures cannot drive the MAUI pan recognizer — real drag/pull-down-to-close was
/// verified manually on the simulator), popup-over-sheet stacking, and the dismissal policies.
/// </summary>
public class ScaffoldSheetChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Sheet Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    [Fact]
    public async Task ContentSheetShowsBottomAnchoredAndClosesFromContent()
    {
        await WaitDisplayedAsync("SheetHomePage");

        await App.TapAsync("ShowContentSheetButton");
        await App.WaitForTextAsync("SheetState", "sheet:open");

        // Bottom-anchored, full width, hugging its content (well under half the screen).
        var sheet = await App.WaitForStableBoundsAsync("ScaffoldBottomSheet");
        var scrim = await App.WaitForStableBoundsAsync("SheetScrim");
        (sheet.Y + sheet.Height).Should().BeApproximately(scrim.Y + scrim.Height, 4, "the sheet is bottom-anchored");
        sheet.Width.Should().BeApproximately(scrim.Width, 4, "the sheet spans the full width");
        sheet.Height.Should().BeLessThan(scrim.Height / 2, "a Content detent hugs the content");

        await App.TapAsync("CloseSheetButton");
        await App.WaitForTextAsync("SheetState", "sheet:closed");
    }

    [Fact]
    public async Task ScrimTapClosesSheet()
    {
        await WaitDisplayedAsync("SheetHomePage");

        await App.TapAsync("ShowContentSheetButton");
        await App.WaitForTextAsync("SheetState", "sheet:open");

        await App.TapAsync("SheetScrim");
        await App.WaitForTextAsync("SheetState", "sheet:closed");
    }

    [Fact]
    public async Task DetentSheetOpensAtInitialDetentAndSnapsBetweenDetents()
    {
        await WaitDisplayedAsync("SheetHomePage");

        await App.TapAsync("ShowDetentSheetButton");
        await App.WaitForTextAsync("DetentSheetState", "detent:open");

        // Initial detent: Height(220) visible (the sheet itself is as tall as the LARGEST
        // detent, translated down — the visible portion is what the window bounds report).
        var scrim = await App.WaitForStableBoundsAsync("SheetScrim");
        var screenBottom = scrim.Y + scrim.Height;
        await App.WaitForBoundsAsync("ScaffoldBottomSheet", b => Math.Abs(screenBottom - b.Y - 220) < 12);

        // Programmatic snap to the Fraction(0.85) detent, then back.
        await App.TapAsync("ExpandSheetButton");
        await App.WaitForBoundsAsync("ScaffoldBottomSheet", b => screenBottom - b.Y > 500);

        await App.TapAsync("CollapseSheetButton");
        await App.WaitForBoundsAsync("ScaffoldBottomSheet", b => Math.Abs(screenBottom - b.Y - 220) < 12);

        await App.TapAsync("SheetScrim");
        await App.WaitForTextAsync("DetentSheetState", "detent:closed");
    }

    [Fact]
    public async Task PopupOverSheetStacksAndClosesIndependently()
    {
        await WaitDisplayedAsync("SheetHomePage");

        await App.TapAsync("ShowContentSheetButton");
        await App.WaitForTextAsync("SheetState", "sheet:open");

        await App.TapAsync("OpenPopupOverSheetButton");
        await App.WaitForTextAsync("SheetPopupState", "popup:open");
        await WaitDisplayedAsync("PopupOverSheetContent");

        // Closing the popup leaves the sheet presented and interactive.
        await App.TapAsync("ClosePopupOverSheetButton");
        await App.WaitForTextAsync("SheetPopupState", "popup:closed");
        await App.WaitForTextAsync("SheetState", "sheet:open");

        await App.TapAsync("CloseSheetButton");
        await App.WaitForTextAsync("SheetState", "sheet:closed");
    }

    [Fact]
    public async Task NavigationClosesSheet()
    {
        await WaitDisplayedAsync("SheetHomePage");

        await App.TapAsync("ShowContentSheetButton");
        await App.WaitForTextAsync("SheetState", "sheet:open");

        await App.TapAsync("NavigateFromSheetButton");
        await WaitDisplayedAsync("SheetOtherPage");
        await App.WaitForTextAsync("SheetState", "sheet:closed");
    }
}
