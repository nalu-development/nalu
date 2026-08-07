using Microsoft.Extensions.Logging;
using Microsoft.Maui.DevFlow.Agent;
using Nalu.Maui.DailyHelper.Overlays;
using Nalu.Maui.DailyHelper.PageModels;
using Nalu.Maui.DailyHelper.Services;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace Nalu.Maui.DailyHelper;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseNaluNavigation<App>(nav => nav
                .AddPages()

                // Navigation-state restoration: relaunch the app and land exactly where you
                // were — including inside the task editor, whose intent is rehydrated (see
                // TaskEditorIntent / IIntentHydrator on the Today & Tasks page models).
                // Enabled unconditionally here (it IS the demo); real apps usually gate it:
                //   restore.Enabled = isDebugBuild;
                .WithRestore(restore =>
                    {
                        restore.MaxAge = TimeSpan.FromDays(1);
                        restore.AddIntent<TaskEditorIntent>();
                    }
                )
            )

            // Model-first overlays (§7.2): the duration sheet is shown via IOverlayService
            // from the task editor's page model — no view references in the model.
            .UseNaluScaffold(scaffold => scaffold.AddOverlay<DurationSheetModel, DurationSheetView>())
            .UseSkiaSharp()
            .UseNaluControls()
            .UseNaluVirtualScroll()
            .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "Regular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "SemiBold");
                    fonts.AddFont("MaterialIcons-Filled.ttf", "Material");
                }
            );

        builder.Services
               .AddSingleton<WeatherStore>()
               .AddSingleton<TodoStore>()
               .AddSingleton<AppScaffold>();

#if DEBUG
        builder.Logging.AddDebug();

        // DevFlow in-app agent: exposes visual tree, screenshots, interactions and logs
        // to the `maui devflow` CLI / MCP server. Per-platform ports (same scheme as the
        // TestApp): Android 9223 (adb forward), iOS 9224, Mac Catalyst 9225 — the simulator
        // and Catalyst bind the host loopback directly and must not collide with each other
        // or with the Android forward.
        builder.AddMauiDevFlowAgent(options =>
        {
#if ANDROID
            options.Port = 9223;
#elif IOS
            options.Port = 9224;
#elif MACCATALYST
            options.Port = 9225;
#endif
        });
#endif

        return builder.Build();
    }
}
