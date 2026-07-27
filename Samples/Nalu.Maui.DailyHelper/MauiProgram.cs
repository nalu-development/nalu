using Microsoft.Extensions.Logging;
using Microsoft.Maui.DevFlow.Agent;
using Nalu.Maui.DailyHelper.Services;

namespace Nalu.Maui.DailyHelper;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseNaluNavigation<App>(nav => nav.AddPages())
            .UseNaluScaffold()
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
        // to the `maui devflow` CLI / MCP server (port 9223).
        builder.AddMauiDevFlowAgent();
#endif

        return builder.Build();
    }
}
