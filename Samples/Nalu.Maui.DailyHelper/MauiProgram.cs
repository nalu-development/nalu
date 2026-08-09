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
            // AddPages() is SOURCE-GENERATED (trim/AOT-safe): every ContentPage in this assembly
            // is registered with its inferred page model (ctor BindingContext assignment, single
            // INPC ctor parameter, or the MyPage -> MyPageModel naming convention).
            .UseNaluNavigation<App>(nav => nav.AddPages())

            // Navigation-state restoration: relaunch the app and land exactly where you
            // were — including inside the task editor, whose intent is rehydrated (see
            // TaskEditorIntent / IIntentHydrator on the Today & Tasks page models).
            // AddIntents() is source-generated too: it registers every intent discovered via
            // IEnteringAware<T>/IAppearingAware<T>, honoring [AutoNavigationIntent].
            // Enabled unconditionally here (it IS the demo); real apps usually gate it:
            //   restore.Enabled = isDebugBuild;
            .UseNaluNavigationRestore(restore =>
                {
                    restore.MaxAge = TimeSpan.FromDays(1);
                    restore.AddIntents();
                }
            )

            // Model-first overlays (§7.2): the duration sheet is shown via IOverlayService
            // from the task editor's page model — no view references in the model.
            // AddOverlays() is source-generated: it discovers classes taking IOverlayRef in
            // their constructor and pairs each model with its view.
            .UseNaluScaffold(scaffold => scaffold.AddOverlays())
            .UseSkiaSharp()
            .UseNaluControls()
            .UseNaluLayouts()
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
