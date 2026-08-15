using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Platform;

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
    public static void Configure(MauiAppBuilder builder)
        => builder.ConfigureLifecycleEvents(events => events.AddiOS(ios => ios.FinishedLaunching((_, _) =>
        {
            KeyboardAutoManagerScroll.Disconnect();

            return true;
        })));
}
