using CommunityToolkit.Maui;
using SkiaSharp.Views.Maui.Controls.Hosting;

#if DEBUG
using Microsoft.Extensions.Logging;
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
            .UseNaluNavigation<App>(nav => nav
                                           .AddPages()
                                           .WithNavigationIntentBehavior(NavigationIntentBehavior.Fallthrough)
                                           .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
            )
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

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddSimpleConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        return builder.Build();
    }
}
