using Microsoft.Extensions.Logging;

namespace Nalu.Maui.Sample;

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
            .UseNaluNavigation<App>(nav => nav.AddPages())
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

        builder.Services.AddTransientPopup<CanLeavePopup, CanLeavePopupModel>();

        return builder.Build();
    }
}
