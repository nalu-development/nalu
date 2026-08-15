using CoreGraphics;
using UIKit;

namespace Nalu;

/// <summary>Locates the focused text input (the first responder) inside a surface — what a Pan-mode surface keeps above the keyboard.</summary>
internal static class ScaffoldFocusedInput
{
    /// <summary>
    /// The bottom edge (in <paramref name="surface"/> coordinates, i.e. unaffected by the surface's
    /// own transform) of what must stay above the keyboard: the caret line of a multi-line text view
    /// (an editor may be far taller than the room above the keyboard — panning it whole would be
    /// wrong, and the caret is what the user is looking at), otherwise the whole first responder;
    /// null when nothing inside the surface is editing.
    /// </summary>
    public static double? BottomIn(UIView surface)
    {
        var responder = FindFirstResponder(surface);

        if (responder is null)
        {
            return null;
        }

        var rect = responder.Bounds;

        if (responder is UITextView textView && textView.SelectedTextRange?.End is { } caretPosition)
        {
            var caret = textView.GetCaretRectForPosition(caretPosition);

            if (!caret.IsNull() && !caret.IsInfinite() && !caret.IsEmpty)
            {
                // Include the text view's bottom inset so the pan lands on the padded line, not on the glyph box.
                var caretBottom = caret.GetMaxY() + textView.TextContainerInset.Bottom;
                rect = new CGRect(rect.X, rect.Y, rect.Width, Math.Clamp(caretBottom - rect.Y, 0, rect.Height));
            }
        }

        return (double)responder.ConvertRectToView(rect, surface).GetMaxY();
    }

    private static UIView? FindFirstResponder(UIView view)
    {
        if (view.IsFirstResponder)
        {
            return view;
        }

        foreach (var subview in view.Subviews)
        {
            if (FindFirstResponder(subview) is { } responder)
            {
                return responder;
            }
        }

        return null;
    }
}
