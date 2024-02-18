using Microsoft.Extensions.Logging;

namespace Nalu.Maui.DefaultShellSample;

using CommunityToolkit.Maui;
using Sample.PageModels;
using Sample.Pages;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("MaterialIcons-Filled.ttf", "MaterialFilled");
                fonts.AddFont("MaterialIcons-Outlined.otf", "MaterialOutlined");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        builder.Services
            .AddTransientWithShellRoute<OnePage, OnePageModel>("//One")
            .AddTransientWithShellRoute<ThreePage, ThreePageModel>("//One/Three")
            .AddTransientWithShellRoute<FourPage, FourPageModel>("//One/Four")
            .AddTransientWithShellRoute<TwoPage, TwoPageModel>("//Two")
            .AddTransientWithShellRoute<FivePage, FivePageModel>("//Five");

        return builder.Build();
    }
}
