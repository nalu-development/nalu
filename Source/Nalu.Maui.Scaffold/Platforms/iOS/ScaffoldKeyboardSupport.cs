using Microsoft.Maui.Handlers;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Platform;
using UIKit;

namespace Nalu;

/// <summary>
/// App-level keyboard wiring for scaffold-hosted apps on iOS: MAUI's built-in
/// <see cref="KeyboardAutoManagerScroll"/> is disconnected at launch. The scaffold positions its
/// overlays (bottom sheets, popups) against <c>UIView.keyboardLayoutGuide</c> itself, and the MAUI
/// manager — which scrolls/pans the presented view controller's hierarchy under the keyboard —
/// fights that (it does not know the overlay layer and moves content the scaffold just placed).
/// </summary>
internal static class ScaffoldKeyboardSupport
{
    private static bool _editorMapperAppended;

    public static void Configure(MauiAppBuilder builder)
    {
        builder.ConfigureLifecycleEvents(events => events.AddiOS(ios => ios.FinishedLaunching((_, _) =>
        {
            KeyboardAutoManagerScroll.Disconnect();

            return true;
        })));

        AppendEditorAutoSizeMapping();
    }

    /// <summary>
    /// An auto-sizing <see cref="Editor"/> grows with its text and never scrolls itself, but MAUI
    /// leaves the native <c>UITextView.scrollEnabled</c> on: UIKit then keeps the caret visible
    /// by scrolling <em>inside</em> the (never-overflowing) text view and leaves the ancestor scroll
    /// views alone — the caret walks under the keyboard as lines are added. With scrolling disabled
    /// UIKit reveals the caret through the ancestors (ScrollView, collection views) exactly as it does
    /// for a text field. Editors with an explicit maximum height keep scrolling (they can overflow).
    /// </summary>
    private static void AppendEditorAutoSizeMapping()
    {
        if (_editorMapperAppended)
        {
            return;
        }

        _editorMapperAppended = true;
        EditorHandler.Mapper.AppendToMapping(nameof(Editor.AutoSize), UpdateScrollEnabled);
        EditorHandler.Mapper.AppendToMapping(nameof(VisualElement.MaximumHeightRequest), UpdateScrollEnabled);
    }

    private static void UpdateScrollEnabled(IEditorHandler handler, IEditor view)
    {
        if (view is not Editor editor || handler.PlatformView is not UITextView textView)
        {
            return;
        }

        var maxHeight = editor.MaximumHeightRequest;
        var bounded = maxHeight >= 0 && !double.IsPositiveInfinity(maxHeight);
        textView.ScrollEnabled = editor.AutoSize == EditorAutoSizeOption.Disabled || bounded;
    }
}
