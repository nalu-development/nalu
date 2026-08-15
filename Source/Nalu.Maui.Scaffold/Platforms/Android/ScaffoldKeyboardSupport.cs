using Android.Views;
using AndroidX.Activity;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.LifecycleEvents;
using AndroidApplication = Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application;

namespace Nalu;

/// <summary>
/// App-level keyboard wiring for scaffold-hosted apps on Android: the activity goes
/// edge-to-edge (<see cref="EdgeToEdge.Enable(ComponentActivity)"/>) and its window uses
/// <c>adjustResize</c>. Together they make the soft keyboard a WINDOW INSET
/// (<c>WindowInsetsCompat.Type.Ime()</c>) instead of a window resize or pan: the framework only
/// reports IME insets under adjustResize, and only an edge-to-edge window keeps its full size
/// while they arrive. The scaffold's overlays (bottom sheets, popups) are then positioned against
/// the IME by <see cref="ScaffoldLayout"/> (see <c>ScaffoldLayout.ImeBottomInsetPx</c>).
/// </summary>
/// <remarks>
/// The soft-input mode is FORCED through the window mapper rather than set once at activity
/// creation: MAUI applies <c>Application.On&lt;Android&gt;().WindowSoftInputModeAdjust</c> (default
/// <c>Pan</c>) to the window whenever it maps it, which happens AFTER the activity's OnCreate
/// lifecycle event and would silently put the window back into adjustPan — where the framework
/// pans the whole window under the keyboard and never reports IME insets.
/// </remarks>
internal static class ScaffoldKeyboardSupport
{
    public static void Configure(MauiAppBuilder builder)
    {
        builder.ConfigureLifecycleEvents(events => events.AddAndroid(android => android.OnCreate((activity, _) =>
        {
            if (activity is ComponentActivity componentActivity)
            {
                EdgeToEdge.Enable(componentActivity);
            }
        })));

        // Runs after MAUI's own mapping of the same property (append), on every application.
        WindowHandler.Mapper.AppendToMapping(AndroidApplication.WindowSoftInputModeAdjustProperty.PropertyName, static (handler, _) =>
        {
            if (handler.PlatformView.Window is { } window)
            {
                // Replace only the ADJUST part: the visibility/state flags stay whatever the
                // manifest or the app configured.
                var current = window.Attributes?.SoftInputMode ?? SoftInput.AdjustUnspecified;
                window.SetSoftInputMode((current & ~SoftInput.MaskAdjust) | SoftInput.AdjustResize);
            }
        });
    }
}
