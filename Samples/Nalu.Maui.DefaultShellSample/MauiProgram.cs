namespace Nalu.Maui.DefaultShellSample;

using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using PageModels;
using Pages;
using FourPageModel = PageModels.FourPageModel;
using OnePageModel = PageModels.OnePageModel;
using ThreePageModel = PageModels.ThreePageModel;
using TwoPageModel = PageModels.TwoPageModel;

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
            .AddTransientWithShellRoute<FourPage, FourPageModel>("//One/Three/Four")
            .AddTransientWithShellRoute<TwoPage, TwoPageModel>("//Two")
            .AddTransientWithShellRoute<SixPage, SixPageModel>("//Two/Six")
            .AddTransientWithShellRoute<SevenPage, SevenPageModel>("//Seven")
            .AddTransientWithShellRoute<FivePage, FivePageModel>("//Five");

        return builder.Build();
    }
}
