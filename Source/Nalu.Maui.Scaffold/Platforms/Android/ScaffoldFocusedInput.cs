using Microsoft.Maui.Platform;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>Locates the focused text input (the window focus) inside a surface — what a Pan-mode surface keeps above the keyboard.</summary>
internal static class ScaffoldFocusedInput
{
    /// <summary>
    /// The bottom edge (dp, relative to <paramref name="surface"/>'s top edge as laid out — its
    /// children's translations included) of the focused view inside the surface; null when the
    /// window focus is elsewhere.
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

        return context.FromPixels(focusLocation[1] - surfaceLocation[1] + focus.Height);
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
