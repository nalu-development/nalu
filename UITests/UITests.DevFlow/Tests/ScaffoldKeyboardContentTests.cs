using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Text inputs INSIDE scrollable content (the "Scaffold Keyboard Content Tests" harness): a
/// single-line <c>Entry</c> and a multi-line, auto-sizing <c>Editor</c> hosted in a
/// <c>ScrollView</c> and in a <c>VirtualScroll</c>. Focusing an input, and then TYPING new lines
/// into the editor while the keyboard is up (the editor grows under the caret), must keep the caret
/// line above the keyboard under both page keyboard modes: <c>Resize</c> (the platform's scroll
/// containers reveal the caret — iOS only does so once the scaffold turns the auto-sizing text
/// view's own scrolling off) and <c>Pan</c> (the scaffold follows the caret, not the whole editor).
/// </summary>
/// <remarks>
/// New lines are typed through the harness "AddLine" button, which inserts a newline at the caret
/// through the platform text input (UIKit <c>insertText</c> / Android <c>Editable.insert</c>) — the
/// same path the soft keyboard takes; the DevFlow agent's own key injection places text at index 0.
/// </remarks>
public class ScaffoldKeyboardContentTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Keyboard Content Tests";
    private const double _minKeyboardHeight = 100;

    /// <summary>
    /// How far below the keyboard's top edge the EDITOR's bottom may sit while its caret line is
    /// still fully visible: the platforms reveal the caret rect (the glyph box of the last 14pt
    /// line), not the editor's bottom padding, and that padding differs per platform. 21 was
    /// calibrated on iOS; Android's editor carries a deeper bottom — measured 21.7dp below the
    /// keyboard top in Pan mode, where PanGap already holds the caret 8dp ABOVE it, so ~30dp of
    /// padding and descender below the caret line against iOS's ~20.
    /// Kept tight enough to matter: an editor that genuinely stayed under the keyboard misses by
    /// tens of dp, not by two.
    /// </summary>
    private const double _caretPaddingTolerance = 24;

    /// <summary>Top padding + one 14pt line of the harness editors — where the caret of an empty editor sits.</summary>
    private const double _firstLineHeight = 30;

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForBoundsAsync("KeyboardScrollFormPage", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private async Task<double> GetKeyboardHeightAsync(string marker)
    {
        var text = await App.GetPropertyAsync($"{marker}KeyboardHeight", "Text") ?? "kb:0";

        return double.Parse(text["kb:".Length..], System.Globalization.CultureInfo.InvariantCulture);
    }

    private async Task<double> RaiseKeyboardAsync(string marker, string inputId)
    {
        for (var attempt = 0; ; attempt++)
        {
            await App.FocusAsync(inputId);

            try
            {
                await App.WaitForSoftKeyboardAsync(visible: true, $"{marker}KeyboardProbe", TimeSpan.FromSeconds(3));
                await App.WaitForTextMatchAsync($"{marker}KeyboardHeight", text => text is not null && text != "kb:0", TimeSpan.FromSeconds(3));

                break;
            }
            catch (TimeoutException) when (attempt < 2)
            {
            }
        }

        await App.WaitForStableBoundsAsync(inputId);

        var height = await GetKeyboardHeightAsync(marker);
        Assert.SkipWhen(height < _minKeyboardHeight, $"No full soft keyboard on this device (overlap {height:0}dp — a hardware keyboard is probably connected).");

        return height;
    }

    /// <summary>Types <paramref name="lines"/> newlines into the focused editor and waits for it to have grown.</summary>
    private async Task AddLinesAsync(string marker, string editorId, int lines)
    {
        var before = await App.GetBoundsAsync(editorId);

        for (var i = 0; i < lines; i++)
        {
            await App.TapAsync($"{marker}AddLineButton");
        }

        await App.WaitForBoundsAsync(editorId, b => b.Height >= before.Height + lines * 10, TimeSpan.FromSeconds(5));
        await App.WaitForStableBoundsAsync(editorId);
    }

    private static void AssertCaretLineAboveKeyboard(ElementBounds editor, double windowHeight, double keyboard, string because)
    {
        var keyboardTop = windowHeight - keyboard;
        editor.Bottom.Should().BeLessThanOrEqualTo(keyboardTop + _caretPaddingTolerance, because);
        editor.Bottom.Should().BeGreaterThan(keyboardTop - 200, "the editor's last line was not scrolled far above the keyboard either");
    }

    private async Task EditorGrowthKeepsTheCaretAboveTheKeyboardAsync(string marker, string editorId, ScaffoldKeyboardModeName mode)
    {
        var (_, windowHeight) = await App.GetWindowSizeAsync();

        if (mode != ScaffoldKeyboardModeName.Resize)
        {
            await App.TapAsync($"{marker}Mode{mode}Button");
            await App.WaitForTextAsync($"{marker}Mode", $"page:{mode}");
        }

        await App.TapAsync($"{marker}ToEndButton");
        await App.WaitForStableBoundsAsync(editorId);
        var resting = await App.GetBoundsAsync(editorId);

        var keyboard = await RaiseKeyboardAsync(marker, editorId);

        // Focus: the (still empty, minimum-height) editor's caret sits on its FIRST line — that line
        // must be brought above the keyboard (the editor's padded bottom may still be under it).
        var focused = await App.WaitForBoundsAsync(editorId, b => b.Y + _firstLineHeight <= windowHeight - keyboard && b.Y >= 0, TimeSpan.FromSeconds(5));
        (focused.Y + _firstLineHeight).Should().BeLessThanOrEqualTo(windowHeight - keyboard, $"[{mode}] focusing the editor reveals its caret line above the keyboard");

        // Typing: the editor grows under the caret — the caret line must stay above the keyboard.
        await AddLinesAsync(marker, editorId, 4);
        var grown = await App.WaitForBoundsAsync(editorId, b => b.Bottom <= windowHeight - keyboard + _caretPaddingTolerance, TimeSpan.FromSeconds(5));
        grown.Height.Should().BeGreaterThan(resting.Height + 40, "the auto-sizing editor grew with the typed lines");
        AssertCaretLineAboveKeyboard(grown, windowHeight, keyboard, $"[{mode}] the caret line follows the typing above the keyboard");

        // Once more: growth keeps being followed, not just the first time.
        await AddLinesAsync(marker, editorId, 2);
        var grownAgain = await App.WaitForBoundsAsync(editorId, b => b.Bottom <= windowHeight - keyboard + _caretPaddingTolerance, TimeSpan.FromSeconds(5));
        AssertCaretLineAboveKeyboard(grownAgain, windowHeight, keyboard, $"[{mode}] the caret line keeps following further typing");

        await App.TapAsync($"{marker}HideButton");
        await App.WaitForSoftKeyboardAsync(visible: false, $"{marker}KeyboardProbe");
        await App.WaitForTextAsync($"{marker}KeyboardHeight", "kb:0");
    }

    private enum ScaffoldKeyboardModeName
    {
        Resize,
        Pan
    }

    [Fact]
    public Task ScrollViewEditorGrowthKeepsTheCaretAboveTheKeyboard_Resize()
        => EditorGrowthKeepsTheCaretAboveTheKeyboardAsync("KbScroll", "KbScrollEditor", ScaffoldKeyboardModeName.Resize);

    [Fact]
    public Task ScrollViewEditorGrowthKeepsTheCaretAboveTheKeyboard_Pan()
        => EditorGrowthKeepsTheCaretAboveTheKeyboardAsync("KbScroll", "KbScrollEditor", ScaffoldKeyboardModeName.Pan);

    [Fact]
    public async Task VirtualScrollEditorGrowthKeepsTheCaretAboveTheKeyboard_Resize()
    {
        await OpenVirtualFormAsync();
        await EditorGrowthKeepsTheCaretAboveTheKeyboardAsync("KbVirtual", "KbVirtualEditor", ScaffoldKeyboardModeName.Resize);
    }

    [Fact]
    public async Task VirtualScrollEditorGrowthKeepsTheCaretAboveTheKeyboard_Pan()
    {
        await OpenVirtualFormAsync();
        await EditorGrowthKeepsTheCaretAboveTheKeyboardAsync("KbVirtual", "KbVirtualEditor", ScaffoldKeyboardModeName.Pan);
    }

    [Fact]
    public async Task ScrollViewEntryIsRevealedAboveTheKeyboardOnFocus()
    {
        var (_, windowHeight) = await App.GetWindowSizeAsync();

        await App.TapAsync("KbScrollToEndButton");
        await App.WaitForStableBoundsAsync("KbScrollEntry");

        var keyboard = await RaiseKeyboardAsync("KbScroll", "KbScrollEntry");
        var entry = await App.WaitForBoundsAsync("KbScrollEntry", b => b.Bottom <= windowHeight - keyboard + 1, TimeSpan.FromSeconds(5));
        entry.Bottom.Should().BeLessThanOrEqualTo(windowHeight - keyboard + 1, "the focused single-line entry sits above the keyboard");
        entry.Y.Should().BeGreaterThan(0);

        await App.TapAsync("KbScrollHideButton");
        await App.WaitForSoftKeyboardAsync(visible: false, "KbScrollKeyboardProbe");
    }

    [Fact]
    public async Task VirtualScrollEntryCellIsRevealedAboveTheKeyboardOnFocus()
    {
        var (_, windowHeight) = await App.GetWindowSizeAsync();
        await OpenVirtualFormAsync();

        // A row that is on screen at rest but would end up UNDER the keyboard.
        var candidate = await App.GetBoundsAsync("KbVirtualEntry9");
        candidate.Bottom.Should().BeGreaterThan(windowHeight * 0.55, "the harness row must start out low enough to be covered by the keyboard");

        var keyboard = await RaiseKeyboardAsync("KbVirtual", "KbVirtualEntry9");
        var entry = await App.WaitForBoundsAsync("KbVirtualEntry9", b => b.Bottom <= windowHeight - keyboard + 1, TimeSpan.FromSeconds(5));
        entry.Bottom.Should().BeLessThanOrEqualTo(windowHeight - keyboard + 1, "the focused entry cell was scrolled above the keyboard");
        entry.Y.Should().BeGreaterThan(0);

        await App.TapAsync("KbVirtualHideButton");
        await App.WaitForSoftKeyboardAsync(visible: false, "KbVirtualKeyboardProbe");
    }

    private async Task OpenVirtualFormAsync()
    {
        await App.TapAsync("TabVirtualForm");
        await App.WaitForBoundsAsync("KeyboardVirtualFormPage", b => b.Y > 0);
        await App.WaitForElementAsync("KbVirtualEntry1");
    }
}
