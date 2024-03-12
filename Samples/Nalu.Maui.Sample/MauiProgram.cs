namespace Nalu.Maui.Sample;

using Microsoft.Extensions.Logging;

using CommunityToolkit.Maui;
using PopupModels;
using Popups;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseNaluNavigation<App>(nav => nav
                .AddPages()
                .WithBackImage(new FontImageSource
                {
                    FontFamily = "MaterialFilled",
                    Glyph = "\uE5C4",
                    Size = 24
                })
            )
            .UseMauiCommunityToolkit()
            .ConfigureMauiHandlers(handlers =>
            {
#if ANDROID
                // Fix for https://github.com/dotnet/maui/issues/7045
                handlers.AddHandler<Shell, PatchedShellRenderer>();
#endif
            })
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

        builder.Services.AddTransientPopup<CanLeavePopup, CanLeavePopupModel>();

        return builder.Build();
    }
}
