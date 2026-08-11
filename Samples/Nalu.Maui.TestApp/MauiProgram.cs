using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;

#if DEBUG
using Microsoft.Extensions.Logging;
using Microsoft.Maui.DevFlow.Agent;
#endif

namespace Nalu.Maui.TestApp;

#if !(IOS || ANDROID || WINDOWS || MACCATALYST)
#pragma warning disable CA1416
#endif

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        AppContext.SetSwitch("System.Reflection.NullabilityInfoContext.IsSupported", true);

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // AddPages() is SOURCE-GENERATED: every ContentPage in this assembly is registered,
            // including the view-only harness pages (no page model) that previously needed
            // explicit AddPage<T>() calls.
            .UseNaluNavigation<App>(nav => nav
                                           .AddPages()
                                           .WithNavigationIntentBehavior(NavigationIntentBehavior.Fallthrough)
                                           .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
            )
            // Navigation-state snapshot & restore, exercised by the "Scaffold Restore
            // Tests" harness. DISABLED at launch: the harness scaffold toggles Enabled
            // around its own lifetime (ctor on / Dispose off), so other suites never
            // capture or restore. AddIntents() is source-generated (discovers every
            // IEnteringAware<T>/IAppearingAware<T> intent, e.g. RestoreDetailIntent).
            .UseNaluNavigationRestore(restore =>
                {
                    Tests.ScaffoldRestoreTestSupport.Options = restore;
                    restore.Enabled = false;
                    restore.AddIntents();
                }
            )
            .UseNaluTabBar()
            // Model-first overlays exercised by the "Scaffold Overlay Service Tests" harness.
            // AddOverlays() is source-generated (anchors on IOverlayRef ctor parameters).
            .UseNaluScaffold(scaffold => scaffold.AddOverlays())
            .UseSkiaSharp()
            .UseNaluLayouts()
            .UseNaluControls()
            .UseNaluVirtualScroll()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "Regular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "SemiBold");
                    fonts.AddFont("MaterialIcons-Filled.ttf", "Material");
                }
            );

        builder.Services
               .AddSingleton<TimeProvider>(TimeProvider.System)
               .AddSingleton<IPreferences>(Preferences.Default);

#if IOS && !MACCATALYST
        // Receives background HTTP responses whose originating request no longer exists
        // (app relaunched): displayed by the manual "Background Http Tests" page.
        builder.Services.AddSingleton<INSUrlBackgroundSessionLostMessageHandler, Tests.BackgroundHttpLostMessageHandler>();
#endif

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddSimpleConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        // DevFlow in-app agent: exposes visual tree, screenshots, interactions and logs
        // to the `maui devflow` CLI / MCP server and to the UITests.DevFlow test project.
        // Deterministic per-platform ports: the iOS simulator and Mac Catalyst bind the HOST
        // loopback directly, so they must not share a port with each other nor with the
        // Android `adb forward` (which claims the host side of its port). With this split,
        // emulator and simulator sessions coexist and tests/MCP can target deterministically.
        builder.AddMauiDevFlowAgent(options =>
        {
#if ANDROID
            options.Port = 9223; // reached via `adb forward tcp:9223 tcp:9223`
#elif IOS
            options.Port = 9224;
#elif MACCATALYST
            options.Port = 9225;
#elif WINDOWS
            options.Port = 9226;
#endif
        });
#endif

        return builder.Build();
    }
}
