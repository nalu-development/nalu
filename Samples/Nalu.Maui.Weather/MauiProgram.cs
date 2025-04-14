using CommunityToolkit.Maui;
using Nalu.Maui.Weather.Popups;
using Nalu.Maui.Weather.Services;
using Nalu.Maui.Weather.ViewModels;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace Nalu.Maui.Weather;

#if DEBUG
using Microsoft.Extensions.Logging;
#endif

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
            .UseNaluNavigation<App>(
                nav => nav
                       .AddPages()
                       .WithNavigationIntentBehavior(NavigationIntentBehavior.Fallthrough)
                       .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
            )
            .UseSkiaSharp()
            .UseNaluLayouts()
            .UseNaluControls()
            .UseMauiCommunityToolkit()
            .UseOpenMeteo()
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
               .AddSingleton<IPreferences>(Preferences.Default)
               .AddSingleton<IWeatherService, WeatherService>();

        builder.Services
               .AddSingleton<WeatherState>();
        
        builder.Services.AddTransientPopup<DurationEditPopup, DurationEdit>();

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddSimpleConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        return builder.Build();
    }
}
