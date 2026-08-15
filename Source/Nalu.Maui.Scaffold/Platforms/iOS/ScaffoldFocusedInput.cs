using CoreGraphics;
using UIKit;

namespace Nalu;

/// <summary>Locates the focused text input (the first responder) inside a surface — what a Pan-mode surface keeps above the keyboard.</summary>
internal static class ScaffoldFocusedInput
{
    /// <summary>
    /// The bottom edge (in <paramref name="surface"/> coordinates, i.e. unaffected by the surface's
    /// own transform) of the first responder inside the surface; null when nothing inside it is editing.
    /// </summary>
    public static double? BottomIn(UIView surface)
    {
        var responder = FindFirstResponder(surface);

        return responder is null ? null : (double)responder.ConvertRectToView(responder.Bounds, surface).GetMaxY();
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
