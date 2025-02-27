using CommunityToolkit.Maui;
using FFImageLoading.Maui;
using Nalu.Maui.Weather.Services;
using Nalu.Maui.Weather.ViewModels;

namespace Nalu.Maui.Weather;

#if DEBUG
using Microsoft.Extensions.Logging;
#endif

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        AppContext.SetSwitch("System.Reflection.NullabilityInfoContext.IsSupported", true);

        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseNaluNavigation<App>(
                nav => nav
                       .AddPages()
                       .WithNavigationIntentBehavior(NavigationIntentBehavior.Fallthrough)
                       .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
            )
            .UseNaluLayouts()
            .UseMauiCommunityToolkit()
            .UseOpenMeteo()
            .UseFFImageLoading()
            .ConfigureFonts(
                fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "Regular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "SemiBold");
                    fonts.AddFont("MaterialIcons-Filled.ttf", "Material");
                }
            );

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
