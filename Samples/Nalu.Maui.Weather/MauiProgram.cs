namespace Nalu.Maui.Weather;

using Microsoft.Extensions.Logging;

using CommunityToolkit.Maui;
using Services;
using ViewModels;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        AppContext.SetSwitch("System.Reflection.NullabilityInfoContext.IsSupported", true);

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseNaluNavigation<App>(nav => nav
                .AddPages()
                .WithNavigationIntentBehavior(NavigationIntentBehavior.Fallthrough)
                .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
            )
            .UseNaluLayouts()
            .UseMauiCommunityToolkit()
            .UseOpenMeteo()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "Regular");
                fonts.AddFont("OpenSans-Semibold.ttf", "SemiBold");
                fonts.AddFont("MaterialIcons-Filled.ttf", "MaterialFilled");
                fonts.AddFont("MaterialIcons-Outlined.otf", "MaterialOutlined");
            });

        builder.Services
            .AddSingleton<TimeProvider>(TimeProvider.System)
            .AddSingleton<IGeolocation>(Geolocation.Default)
            .AddSingleton<IWeatherService, WeatherService>();

        builder.Services
            .AddSingleton<WeatherState>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddSimpleConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        return builder.Build();
    }
}
