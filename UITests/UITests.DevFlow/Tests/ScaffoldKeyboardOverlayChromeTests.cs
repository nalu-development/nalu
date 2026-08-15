using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Keyboard-aware overlays (the "Scaffold Keyboard Overlay Tests" harness): a bottom sheet or a
/// popup hosting an <c>Entry</c> must be re-placed in the area ABOVE the soft keyboard when it
/// shows (iOS: <c>keyboardLayoutGuide</c>; Android: IME window insets under the edge-to-edge,
/// adjustResize window the scaffold configures) and go back where it was when it hides.
/// </summary>
/// <remarks>
/// <para>
/// The keyboard's overlap is read from a PLATFORM probe the harness page hosts
/// (<c>KeyboardOverlayKeyboardHeight</c>: UIKit keyboard-frame notifications / the DecorView's
/// root IME insets — never the library under test), so the assertions compare the overlay's
/// content against the keyboard's real top edge (on iOS the keyboard frame includes MAUI's
/// transparent text-input accessory band, and so does the layout guide).
/// </para>
/// <para>
/// The keyboard is raised programmatically (agent focus) and lowered through the overlay's own
/// "hide keyboard" button (programmatic unfocus): a real tap outside would land on the scrim and
/// dismiss the overlay.
/// </para>
/// </remarks>
public class ScaffoldKeyboardOverlayChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Keyboard Overlay Tests";
    private const string KeyboardProbe = "KeyboardOverlayKeyboardProbe";
    private const string KeyboardHeightProbe = "KeyboardOverlayKeyboardHeight";
    private const string SheetId = "ScaffoldBottomSheet";

    /// <summary>The smallest overlap that counts as a real keyboard (not the iOS hardware-keyboard accessory bar).</summary>
    private const double MinKeyboardHeight = 100;

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForBoundsAsync("KeyboardOverlayHomePage", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    /// <summary>The keyboard's overlap with the window in dp, from the harness probe.</summary>
    private async Task<double> GetKeyboardHeightAsync()
    {
        var text = await App.GetPropertyAsync(KeyboardHeightProbe, "Text") ?? "kb:0";

        return double.Parse(text["kb:".Length..], System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<double> RaiseKeyboardAsync(string entryId)
    {
        for (var attempt = 0; ; attempt++)
        {
            await App.FocusAsync(entryId);

            try
            {
                await App.WaitForSoftKeyboardAsync(visible: true, KeyboardProbe, TimeSpan.FromSeconds(3));
                await App.WaitForTextMatchAsync(KeyboardHeightProbe, text => text is not null && text != "kb:0", TimeSpan.FromSeconds(3));

                break;
            }
            catch (TimeoutException) when (attempt < 2)
            {
            }
        }

        // Let the keyboard animation (and the overlay riding it) settle.
        await App.WaitForStableBoundsAsync(SheetOrPopupId(entryId));

        var height = await GetKeyboardHeightAsync();
        Assert.SkipWhen(height < MinKeyboardHeight, $"No full soft keyboard on this device (overlap {height:0}dp — a hardware keyboard is probably connected).");

        return height;
    }

    private static string SheetOrPopupId(string entryId)
        => entryId.Contains("Sheet", StringComparison.Ordinal)
            ? SheetId
            : entryId.Replace("BottomEntry", "Content", StringComparison.Ordinal).Replace("Entry", "Content", StringComparison.Ordinal);

    private async Task LowerKeyboardAsync(string hideButtonId, string overlayId)
    {
        await App.TapAsync(hideButtonId);
        await App.WaitForSoftKeyboardAsync(visible: false, KeyboardProbe);
        await App.WaitForTextAsync(KeyboardHeightProbe, "kb:0");
        await App.WaitForStableBoundsAsync(overlayId);
    }

    [Fact]
    public async Task ContentSheetPadsItsContentAboveTheKeyboardAndReturns()
    {
        var (_, windowHeight) = await App.GetWindowSizeAsync();

        await App.TapAsync("ShowKeyboardSheetButton");
        await App.WaitForTextAsync("KeyboardOverlayState", "overlay:open");
        var resting = await App.WaitForStableBoundsAsync(SheetId);
        resting.Bottom.Should().BeApproximately(windowHeight, 1, "a content sheet rests on the window's bottom edge");
        var restingEntry = await App.GetBoundsAsync("KeyboardSheetEntry");

        var keyboard = await RaiseKeyboardAsync("KeyboardSheetEntry");

        // The keyboard is a bigger bottom inset REPLACING the system one: the sheet surface STAYS
        // on the bottom edge (continuous behind the keyboard) and grows by (keyboard − system
        // bottom inset), so its content ends up padded above the keyboard's top edge.
        var padded = await App.WaitForBoundsAsync(SheetId, b => b.Y <= resting.Y - keyboard + 60);
        padded.Bottom.Should().BeApproximately(windowHeight, 1, "the sheet surface stays anchored to the bottom edge");
        var growth = resting.Y - padded.Y;
        growth.Should().BeInRange(keyboard - 60, keyboard + 1, "the sheet grows by the keyboard overlap minus the (≤60dp) system inset it replaces");

        var entry = await App.WaitForBoundsAsync("KeyboardSheetEntry", b => b.Bottom <= windowHeight - keyboard + 1);
        entry.Y.Should().BeApproximately(restingEntry.Y - growth, 2, "the content moved up with the padding");
        entry.Y.Should().BeGreaterThanOrEqualTo(padded.Y);

        await LowerKeyboardAsync("KeyboardSheetHideButton", SheetId);
        var back = await App.WaitForBoundsAsync(SheetId, b => Math.Abs(b.Y - resting.Y) <= 1.5);
        back.Bottom.Should().BeApproximately(windowHeight, 1);
        (await App.GetBoundsAsync("KeyboardSheetEntry")).Y.Should().BeApproximately(restingEntry.Y, 1.5, "the content returns exactly where it rested");
    }

    [Fact]
    public async Task TallSheetKeepsItsDetentAndBringsTheBottomEntryAboveTheKeyboard()
    {
        var (_, windowHeight) = await App.GetWindowSizeAsync();

        await App.TapAsync("ShowKeyboardTallSheetButton");
        await App.WaitForTextAsync("KeyboardOverlayState", "overlay:open");
        var resting = await App.WaitForStableBoundsAsync(SheetId);
        resting.Height.Should().BeGreaterThan(windowHeight * 0.6, "the 85% detent resolves against the window height");

        // Focus the entry at the very BOTTOM of the scrollable content: the detent height is
        // unchanged (the sheet surface stays anchored, the keyboard is a bigger bottom inset), the
        // scrollable content area shrinks by the keyboard, and the entry must end up on-screen
        // above the keyboard.
        var keyboard = await RaiseKeyboardAsync("KeyboardTallSheetBottomEntry");
        var padded = await App.WaitForStableBoundsAsync(SheetId);

        padded.Height.Should().BeApproximately(resting.Height, 2, "a Fraction detent resolves against the same available height");
        padded.Bottom.Should().BeApproximately(windowHeight, 1);

        var bottomEntry = await App.WaitForBoundsAsync("KeyboardTallSheetBottomEntry", b => b.Bottom <= windowHeight - keyboard + 1 && b.Y >= padded.Y);
        bottomEntry.Bottom.Should().BeLessThanOrEqualTo(windowHeight - keyboard + 1, "the focused entry is above the keyboard");

        await LowerKeyboardAsync("KeyboardTallSheetHideButton", SheetId);
        var back = await App.WaitForStableBoundsAsync(SheetId);
        back.Height.Should().BeApproximately(resting.Height, 1.5);
        back.Bottom.Should().BeApproximately(windowHeight, 1);
    }

    [Fact]
    public async Task CenteredPopupRecentersInTheAreaAboveTheKeyboard()
    {
        var (_, windowHeight) = await App.GetWindowSizeAsync();
        const string content = "KeyboardPopupContent";

        await App.TapAsync("ShowKeyboardPopupButton");
        await App.WaitForTextAsync("KeyboardOverlayState", "overlay:open");
        var resting = await App.WaitForStableBoundsAsync(content);
        resting.CenterY.Should().BeApproximately(windowHeight / 2, windowHeight * 0.15, "centered in the full window");

        var keyboard = await RaiseKeyboardAsync("KeyboardPopupEntry");
        var lifted = await App.WaitForBoundsAsync(content, b => b.Bottom <= windowHeight - keyboard + 1);

        // Re-centered in what is left above the keyboard (bounds are approximate: the area also
        // excludes the safe-area insets and the 16dp margin).
        var areaCenter = (windowHeight - keyboard) / 2;
        lifted.CenterY.Should().BeLessThan(resting.CenterY, "the popup moved up");
        lifted.CenterY.Should().BeApproximately(areaCenter, 60);

        await LowerKeyboardAsync("KeyboardPopupHideButton", content);
        var back = await App.WaitForBoundsAsync(content, b => Math.Abs(b.Y - resting.Y) <= 1.5);
        back.Height.Should().BeApproximately(resting.Height, 1.5);
    }

    [Fact]
    public async Task AnchoredPopupIsPushedAboveTheKeyboard()
    {
        var (_, windowHeight) = await App.GetWindowSizeAsync();
        const string content = "KeyboardAnchoredPopupContent";

        await App.TapAsync("ShowKeyboardAnchoredPopupButton");
        await App.WaitForTextAsync("KeyboardOverlayState", "overlay:open");
        var resting = await App.WaitForStableBoundsAsync(content);
        var anchor = await App.GetBoundsAsync("ShowKeyboardAnchoredPopupButton");

        // The anchor sits at the bottom of the page: "below" never fits, so the popup already
        // hangs above its anchor.
        resting.Bottom.Should().BeLessThanOrEqualTo(anchor.Y + 1);

        var keyboard = await RaiseKeyboardAsync("KeyboardAnchoredPopupEntry");
        var lifted = await App.WaitForBoundsAsync(content, b => b.Bottom <= windowHeight - keyboard + 1);

        // Clamped into the placement area above the keyboard: its bottom edge lands within the
        // margin band right above the keyboard (the anchor itself is now under the keyboard).
        lifted.Bottom.Should().BeLessThanOrEqualTo(windowHeight - keyboard + 1);
        lifted.Bottom.Should().BeGreaterThan(windowHeight - keyboard - 60);
        lifted.Y.Should().BeLessThan(resting.Y, "the popup moved up");

        await LowerKeyboardAsync("KeyboardAnchoredPopupHideButton", content);
        await App.WaitForBoundsAsync(content, b => Math.Abs(b.Y - resting.Y) <= 1.5);
    }

    [Fact]
    public async Task PanSheetSlidesUpJustEnoughForTheFocusedEntryAndKeepsItsSize()
    {
        var (_, windowHeight) = await App.GetWindowSizeAsync();

        await App.TapAsync("ShowKeyboardPanSheetButton");
        await App.WaitForTextAsync("KeyboardOverlayState", "overlay:open");
        var resting = await App.WaitForStableBoundsAsync(SheetId);
        resting.Bottom.Should().BeApproximately(windowHeight, 1);
        var restingBottomEntry = await App.GetBoundsAsync("KeyboardPanSheetBottomEntry");
        var restingTopEntry = await App.GetBoundsAsync("KeyboardPanSheetEntry");

        // Focus the BOTTOM entry: the sheet keeps its size and slides up by the least that puts
        // that entry above the keyboard.
        var keyboard = await RaiseKeyboardAsync("KeyboardPanSheetBottomEntry");
        var panned = await App.WaitForBoundsAsync(SheetId, b => b.Y < resting.Y - 1);
        panned.Height.Should().BeApproximately(resting.Height, 1.5, "Pan never resizes the surface");

        var pan = resting.Y - panned.Y;
        pan.Should().BeLessThanOrEqualTo(keyboard + 1, "the pan never exceeds the keyboard's overlap");

        var bottomEntry = await App.WaitForBoundsAsync("KeyboardPanSheetBottomEntry", b => b.Bottom <= windowHeight - keyboard + 1);
        bottomEntry.Bottom.Should().BeGreaterThan(windowHeight - keyboard - 40, "the pan is the LEAST needed: the entry sits right above the keyboard (8dp gap)");
        bottomEntry.Y.Should().BeApproximately(restingBottomEntry.Y - pan, 1.5, "content moved with the sheet, not inside it");

        // Move the focus to the TOP entry: the sheet re-pans for it (less, or not at all).
        await App.FocusAsync("KeyboardPanSheetEntry");
        var repanned = await App.WaitForBoundsAsync(SheetId, b => b.Y > panned.Y + 1 || Math.Abs(b.Y - panned.Y) <= 1);
        var repan = resting.Y - repanned.Y;
        repan.Should().BeLessThanOrEqualTo(pan + 1);
        (await App.GetBoundsAsync("KeyboardPanSheetEntry")).Bottom.Should().BeLessThanOrEqualTo(windowHeight - keyboard + 1);
        (await App.GetBoundsAsync("KeyboardPanSheetEntry")).Y.Should().BeApproximately(restingTopEntry.Y - repan, 1.5);

        await LowerKeyboardAsync("KeyboardPanSheetHideButton", SheetId);
        var back = await App.WaitForBoundsAsync(SheetId, b => Math.Abs(b.Y - resting.Y) <= 1.5);
        back.Height.Should().BeApproximately(resting.Height, 1.5);
    }

    [Fact]
    public async Task PanPopupSlidesUpJustEnoughAndKeepsItsSize()
    {
        var (_, windowHeight) = await App.GetWindowSizeAsync();
        const string content = "KeyboardPanPopupContent";

        await App.TapAsync("ShowKeyboardPanPopupButton");
        await App.WaitForTextAsync("KeyboardOverlayState", "overlay:open");
        var resting = await App.WaitForStableBoundsAsync(content);

        var keyboard = await RaiseKeyboardAsync("KeyboardPanPopupBottomEntry");
        var panned = await App.WaitForStableBoundsAsync(content);
        panned.Height.Should().BeApproximately(resting.Height, 1.5, "Pan never resizes the surface");
        panned.Width.Should().BeApproximately(resting.Width, 1.5);

        var entry = await App.GetBoundsAsync("KeyboardPanPopupBottomEntry");
        entry.Bottom.Should().BeLessThanOrEqualTo(windowHeight - keyboard + 1, "the focused entry is above the keyboard");

        // Minimal pan: either the popup did not need to move (entry already above the keyboard) or
        // it moved by exactly what the entry needed (gap included).
        var pan = resting.Y - panned.Y;
        pan.Should().BeGreaterThanOrEqualTo(-1);
        var restingEntryBottom = entry.Bottom + pan;
        var needed = Math.Max(0, restingEntryBottom - (windowHeight - keyboard) + 8);
        pan.Should().BeApproximately(needed, 2);

        await LowerKeyboardAsync("KeyboardPanPopupHideButton", content);
        await App.WaitForBoundsAsync(content, b => Math.Abs(b.Y - resting.Y) <= 1.5);
    }
}
