using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using Nalu.Maui.Sample.PopupModels;
using Nalu.Maui.Sample.Popups;
using Nalu.Maui.Sample.Services;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace Nalu.Maui.Sample;

#if WINDOWS
using Microsoft.Maui.Platform;
#endif

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        AppContext.SetSwitch("System.Reflection.NullabilityInfoContext.IsSupported", true);

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
                                }
                            );
                        }
                    );
#endif
                }
            )
            .UseNaluNavigation<App>(nav => nav
                                           .AddPages()
                                           .WithNavigationIntentBehavior(NavigationIntentBehavior.Fallthrough)
                                           .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
            )
            .UseNaluSoftKeyboardManager()
            .UseSkiaSharp()
            .UseNaluLayouts()
            .UseNaluControls()
            .UseMauiCommunityToolkit()
            .ConfigureMauiHandlers(handlers =>
                {
#if IOS
                    // handlers.AddHandler<CollectionView, Microsoft.Maui.Controls.Handlers.Items2.CollectionViewHandler2>();
#endif
#if ANDROID
                    // Fix for https://github.com/dotnet/maui/issues/7045
                    handlers.AddHandler<Shell, PatchedShellRenderer>();
#endif
                }
            )
            .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("MaterialIcons-Filled.ttf", "MaterialFilled");
                    fonts.AddFont("MaterialIcons-Outlined.otf", "MaterialOutlined");
                }
            );

#if DEBUG
        builder.Logging.AddDebug();
        builder.Logging.AddSimpleConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        builder.Services.AddTransientPopup<CanLeavePopup, CanLeavePopupModel>();

        builder.Services.AddKeyedSingleton<HttpClient>(
            "dummyjson",
            (_, _) =>
            {
#if IOS
                var client = DeviceInfo.DeviceType == DeviceType.Virtual
                    ? new HttpClient()
                    : new(new NSUrlBackgroundSessionHttpMessageHandler());
#else
                HttpClient client = new();
#endif
                client.BaseAddress = new Uri("https://dummyjson.com/");

                return client;
            }
        );

        builder.Services.AddSingleton<StartupEventsHandler>();
#if IOS
        builder.Services.AddSingleton<INSUrlBackgroundSessionLostMessageHandler>(sp => sp.GetRequiredService<StartupEventsHandler>());
#endif

        return builder.Build();
    }
}
