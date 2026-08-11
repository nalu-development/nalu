using Microsoft.Extensions.Logging;

namespace MauiNaluApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            // AddPages() is source-generated (trim/AOT-safe): every ContentPage in this assembly
            // is registered with its inferred page model (ctor BindingContext assignment, single
            // INPC ctor parameter, or the MyPage -> MyPageModel naming convention).
            .UseNaluNavigation<App>(nav => nav.AddPages())
            // The Scaffold hosts Nalu navigation and draws all the chrome (tab bar, nav bar,
            // transitions). Overlays (popups/bottom sheets) can be registered here too.
            .UseNaluScaffold()
            .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "Regular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "SemiBold");
                    fonts.AddFont("MaterialIcons-Filled.ttf", "Material");
                }
            );

        builder.Services.AddSingleton<AppScaffold>();

//-:cnd:noEmit
#if DEBUG
        builder.Logging.AddDebug();
#endif
//+:cnd:noEmit

        ApplyAndroidScrollUnderChromeWorkaround();

        return builder.Build();
    }

    /// <summary>
    /// WORKAROUND for https://github.com/dotnet/maui/issues/37306 — remove once fixed.
    /// Android applies the safe-area inset (system bars + the tab bar's footprint) as PADDING
    /// on the native scroll container and clips children at it, so scrolling content hits an
    /// invisible wall above the floating tab bar instead of sliding underneath it (iOS
    /// content-inset semantics draw under; only the resting position clears the inset).
    /// Disabling clipToPadding restores the iOS behavior — the padding still sizes the scroll
    /// range, so content comes to rest clear of the bar.
    /// </summary>
    private static void ApplyAndroidScrollUnderChromeWorkaround()
    {
//-:cnd:noEmit
#if ANDROID
        Microsoft.Maui.Handlers.ScrollViewHandler.Mapper.AppendToMapping("NoClipToPadding", (handler, _) =>
        {
            if (handler.PlatformView is Android.Views.ViewGroup viewGroup)
            {
                viewGroup.SetClipToPadding(false);
            }
        });
#endif
//+:cnd:noEmit
    }
}
