namespace Nalu.Maui.Sample;

using Microsoft.Extensions.Logging;

using CommunityToolkit.Maui;
using PopupModels;
using Popups;
using Microsoft.Maui.LifecycleEvents;

#if WINDOWS
using Microsoft.Maui.Platform;
#endif


public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(windowsLifecycleBuilder =>
                {
                    // See https://github.com/dotnet/maui/issues/20976 and
                    windowsLifecycleBuilder.OnWindowCreated(window =>
                    {
                        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                        var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
                        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
                        var titleBar = appWindow.TitleBar;
                        var color = Color.FromRgba("#2C479D");
                        titleBar.BackgroundColor = color.ToWindowsColor();
                        titleBar.ButtonBackgroundColor = color.ToWindowsColor();
                        titleBar.InactiveBackgroundColor = color.ToWindowsColor();
                        titleBar.ButtonInactiveBackgroundColor = color.ToWindowsColor();
                    });
                });
#endif
            })
            .UseNaluNavigation<App>(nav => nav
                .AddPages()
                .WithNavigationIntentBehavior(NavigationIntentBehavior.Fallthrough)
                .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
            )
            .UseNaluLayouts()
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
