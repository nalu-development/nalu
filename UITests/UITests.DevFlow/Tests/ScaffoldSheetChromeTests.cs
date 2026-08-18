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
    private const string _pageName = "Scaffold Sheet Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(_pageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    private async Task<bool> IsAndroidAsync()
        => (await App.GetPlatformAsync()).Contains("android", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The cooperative drag/scroll contract with a TALL ScrollView inside the sheet
    /// (real gestures — Android only, injected host-side via adb):
    /// dragging up first EXPANDS the sheet to its top detent, then SCROLLS the content;
    /// dragging down first scrolls back to the top, then COLLAPSES the sheet.
    /// </summary>
    [Fact]
    public async Task ScrollableSheetHandsTheGestureOverCooperatively()
    {
        Assert.SkipWhen(!await IsAndroidAsync(), "Real drags are injected host-side via adb.");

        await WaitDisplayedAsync("SheetHomePage");
        await App.TapAsync("ShowScrollSheetButton");
        await App.WaitForTextAsync("ScrollSheetState", "scroll:open");

        // At the 300dp detent the sheet top sits low; capture it. Gestures anchor on the
        // VISIBLE part of the scrollable (the element's own center sits offscreen while the
        // sheet is collapsed — the tall sheet is mostly translated below the screen).
        var label = await App.WaitForStableBoundsAsync("ScrollSheetOffset");
        var collapsedTop = label.Y;

        // Drag UP over the scrollable: the sheet must EXPAND first (content not scrolled).
        await App.AndroidRealSwipeAtPointAsync(label.CenterX, label.Bottom + 80, 0, -450, durationMs: 400);
        await App.WaitForBoundsAsync("ScrollSheetOffset", b => b.Y < collapsedTop - 100);

        // The settle animation is still running when the threshold trips: capture the top
        // detent position only once the bounds are stable.
        var expanded = await App.WaitForStableBoundsAsync("ScrollSheetOffset");
        var expandedTop = expanded.Y;
        (await App.GetPropertyAsync("ScrollSheetOffset", "Text")).Should().Be("off:0", "expanding must not scroll the content");

        // Drag UP again: now the CONTENT scrolls (sheet already at its top detent).
        await App.AndroidRealSwipeAtPointAsync(expanded.CenterX, expanded.Bottom + 300, 0, -300, durationMs: 400);
        await App.WaitForTextMatchAsync("ScrollSheetOffset", text => text is not null && text != "off:0");
        (await App.GetBoundsAsync("ScrollSheetOffset")).Y.Should().BeApproximately(expandedTop, 8, "the sheet stays at the top detent while content scrolls");

        // Drag DOWN: content scrolls back to its top FIRST; any surplus drag in the same
        // gesture then legitimately starts pulling the sheet (which settles back to the
        // nearest detent — the top one — on release).
        await App.AndroidRealSwipeAtPointAsync(expanded.CenterX, expanded.Bottom + 100, 0, 500, durationMs: 500);
        await App.WaitForTextAsync("ScrollSheetOffset", "off:0");
        await App.WaitForStableBoundsAsync("ScrollSheetOffset");

        // Drag DOWN again from the scroll top: now the SHEET collapses towards the small detent.
        await App.AndroidRealSwipeAtPointAsync(expanded.CenterX, expanded.Bottom + 100, 0, 350, durationMs: 400);
        await App.WaitForBoundsAsync("ScrollSheetOffset", b => b.Y > expandedTop + 100);
    }

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
