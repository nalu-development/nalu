using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>Locates the focused text input (the window focus) inside a surface — what a Pan-mode surface keeps above the keyboard.</summary>
internal static class ScaffoldFocusedInput
{
    /// <summary>
    /// The bottom edge (dp, relative to <paramref name="surface"/>'s top edge as laid out — its
    /// children's translations included) of what must stay above the keyboard: the caret line of a
    /// multi-line text field (an editor may be far taller than the room above the keyboard — panning
    /// it whole would be wrong, and the caret is what the user is looking at), otherwise the whole
    /// focused view; null when the window focus is elsewhere.
    /// </summary>
    public static double? BottomIn(AView surface, Android.Content.Context context)
    {
        if (context.GetActivity()?.CurrentFocus is not { } focus || !IsDescendantOf(focus, surface))
        {
            return null;
        }

        var focusLocation = new int[2];
        var surfaceLocation = new int[2];
        focus.GetLocationInWindow(focusLocation);
        surface.GetLocationInWindow(surfaceLocation);

        return context.FromPixels(focusLocation[1] - surfaceLocation[1] + CaretBottomPx(focus));
    }

    /// <summary>
    /// The window-relative bottom (px) of the caret line (or whole view) of the window focus, if any —
    /// a cheap fingerprint of "what a Pan surface follows" that changes when the caret moves,
    /// the input grows or the surface itself is re-placed.
    /// </summary>
    public static int? CaretBottomInWindowPx(Android.Content.Context context)
    {
        if (context.GetActivity()?.CurrentFocus is not { } focus)
        {
            return null;
        }

        var location = new int[2];
        focus.GetLocationInWindow(location);

        return location[1] + CaretBottomPx(focus);
    }

    /// <summary>The bottom (px, relative to the view's top) of the line holding the selection end — the view's height when that cannot be resolved.</summary>
    private static int CaretBottomPx(AView focus)
    {
        if (focus is Android.Widget.TextView { Layout: { } layout } textView && textView.LineCount > 1)
        {
            var line = layout.GetLineForOffset(Math.Max(0, textView.SelectionEnd));
            var bottom = layout.GetLineBottom(line) + textView.TotalPaddingTop - textView.ScrollY;

            return Math.Clamp(bottom, 0, focus.Height);
        }

        return focus.Height;
    }

    private static bool IsDescendantOf(AView view, AView ancestor)
    {
        for (var parent = view.Parent; parent is not null; parent = parent.Parent)
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
