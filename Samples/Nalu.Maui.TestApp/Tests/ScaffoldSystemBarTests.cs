using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;
#if IOS
using UIKit;
#endif

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Reads the PLATFORM ground truth of the status-bar icon style into a label: on iOS the
/// effective <c>UIStatusBarManager.StatusBarStyle</c> (proving the scaffold controller's
/// PreferredStatusBarStyle actually flows through MAUI's root VC chain), on Android the
/// window's <c>AppearanceLightStatusBars</c> flag. Values: "light-icons" / "dark-icons".
/// </summary>
file static class SysBarsProbeFactory
{
    public static View Make(string marker)
    {
        var label = new Label { Text = "unread", AutomationId = $"SysBarsValue{marker}", FontSize = 12 };
        var button = new Button { Text = "Probe", AutomationId = $"SysBarsProbe{marker}", FontSize = 11 };
        button.Clicked += (_, _) => label.Text = ReadPlatformTruth();

        return new VerticalStackLayout
        {
            Spacing = 2,
            Children = { button, label }
        };
    }

    private static string ReadPlatformTruth()
    {
#if IOS
        var style = (UIApplication.SharedApplication.KeyWindow?.WindowScene?.StatusBarManager?.StatusBarStyle)
            ?? UIStatusBarStyle.Default;

        return style switch
        {
            UIStatusBarStyle.LightContent => "light-icons",
            UIStatusBarStyle.DarkContent => "dark-icons",
            _ => "default"
        };
#elif ANDROID
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not { Window: { } window })
        {
            return "no-window";
        }

        var controller = new AndroidX.Core.View.WindowInsetsControllerCompat(window, window.DecorView);

        // AppearanceLight* = true means DARK icons over a light bar.
        return controller.AppearanceLightStatusBars ? "dark-icons" : "light-icons";
#else
        return "unsupported";
#endif
    }
}

file static class SysBarsPageFactory
{
    /// <summary>The app-reset escape hatch NaluApp.ResetAsync relies on for Scaffold-hosted pages (no decorated ResetButton).</summary>
    public static Button MakeExitButton(string marker)
    {
        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{marker}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        return exitButton;
    }
}

[UsedImplicitly]
public partial class SysBarsHomePageModel(INavigationService navigationService, IScaffoldFlyoutController flyoutController) : ObservableObject
{
    public Task PushDarkBar() => navigationService.GoToAsync(Navigation.Relative().Push<SysBarsDarkBarPageModel>());

    public Task PushDeclared() => navigationService.GoToAsync(Navigation.Relative().Push<SysBarsDeclaredPageModel>());

    public Task PushSurface() => navigationService.GoToAsync(Navigation.Relative().Push<SysBarsSurfacePageModel>());

    public Task PushBare() => navigationService.GoToAsync(Navigation.Relative().Push<SysBarsBarePageModel>());

    public Task OpenFlyout() => flyoutController.OpenAsync(ScaffoldFlyoutSide.Start);
}

[UsedImplicitly]
public partial class SysBarsDarkBarPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class SysBarsDeclaredPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class SysBarsSurfacePageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class SysBarsBarePageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

/// <summary>Root page: the scaffold-level LIGHT opaque bar resolves to dark icons.</summary>
[UsedImplicitly]
public class SysBarsHomePage : ContentPage
{
    public SysBarsHomePage(SysBarsHomePageModel model)
    {
        BindingContext = model;
        Title = "SysBars Home";

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = 16,
            Children =
            {
                new Label { Text = "SysBarsHome", AutomationId = "SysBarsHome", FontSize = 22, FontAttributes = FontAttributes.Bold },
                SysBarsProbeFactory.Make("Home"),
                NavPageFactory.MakeButton("Push dark bar", "PushSysBarsDarkBar", model.PushDarkBar),
                NavPageFactory.MakeButton("Push declared", "PushSysBarsDeclared", model.PushDeclared),
                NavPageFactory.MakeButton("Push surface", "PushSysBarsSurface", model.PushSurface),
                NavPageFactory.MakeButton("Push bare", "PushSysBarsBare", model.PushBare),
                NavPageFactory.MakeButton("Open flyout", "OpenSysBarsFlyout", model.OpenFlyout),
                SysBarsPageFactory.MakeExitButton("SysBarsHome")
            }
        };
    }
}

/// <summary>Pushed page with a DARK opaque page-level bar background: light icons by bar luminance.</summary>
[UsedImplicitly]
public class SysBarsDarkBarPage : ContentPage
{
    public SysBarsDarkBarPage(SysBarsDarkBarPageModel model)
    {
        BindingContext = model;
        Title = "Dark Bar";

        Scaffold.SetNavBarAppearance(
            this,
            new ScaffoldNavBarAppearance
            {
                Background = new SolidColorBrush(Colors.MidnightBlue),
                Foreground = Colors.White
            }
        );

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = 16,
            Children =
            {
                new Label { Text = "SysBarsDarkBar", AutomationId = "SysBarsDarkBar", FontSize = 22, FontAttributes = FontAttributes.Bold },
                SysBarsProbeFactory.Make("DarkBar"),
                NavPageFactory.MakeButton("Pop", "PopSysBarsDarkBar", model.Pop),
                SysBarsPageFactory.MakeExitButton("SysBarsDarkBar")
            }
        };
    }
}

/// <summary>
/// Full-bleed page: transparent bar in overlap mode over dark content + an EXPLICIT
/// <see cref="ScaffoldSystemBarStyle.LightContent"/> declaration (the transparent-bar surface is
/// unknowable). A button then materializes the bar (live appearance mutation, the scroll-driven
/// channel): the now-opaque LIGHT bar must OUTRANK the declaration and flip the icons dark.
/// </summary>
[UsedImplicitly]
public class SysBarsDeclaredPage : ContentPage
{
    public SysBarsDeclaredPage(SysBarsDeclaredPageModel model)
    {
        BindingContext = model;
        Title = "Declared";

        Scaffold.SetNavBarOverlapsContent(this, true);
        Scaffold.SetSystemBarStyle(this, ScaffoldSystemBarStyle.LightContent);

        var appearance = new ScaffoldNavBarAppearance
        {
            Background = new SolidColorBrush(Colors.Transparent),
            Foreground = Colors.White
        };
        Scaffold.SetNavBarAppearance(this, appearance);

        var materializeButton = new Button { Text = "Materialize bar", AutomationId = "SysBarsMaterialize", FontSize = 11 };
        materializeButton.Clicked += (_, _) => appearance.Background = new SolidColorBrush(Colors.White);

        var grid = new Grid
        {
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None),
            BackgroundColor = Colors.DarkSlateBlue
        };

        grid.Add(new VerticalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(16, 140, 16, 16),
            Children =
            {
                new Label { Text = "SysBarsDeclared", AutomationId = "SysBarsDeclared", TextColor = Colors.White, FontSize = 22, FontAttributes = FontAttributes.Bold },
                SysBarsProbeFactory.Make("Declared"),
                materializeButton,
                NavPageFactory.MakeButton("Pop", "PopSysBarsDeclared", model.Pop),
                SysBarsPageFactory.MakeExitButton("SysBarsDeclared")
            }
        });

        Content = grid;
    }
}

/// <summary>
/// Bar-less page whose FIRST CHILD spans the top edge (SafeAreaEdges None) with a DARK
/// background: Auto derives light icons from the page surface. A button then declares
/// <see cref="ScaffoldSystemBarStyle.DarkContent"/> at runtime — the declaration outranks the
/// surface and must flip the icons live.
/// </summary>
[UsedImplicitly]
public class SysBarsSurfacePage : ContentPage
{
    public SysBarsSurfacePage(SysBarsSurfacePageModel model)
    {
        BindingContext = model;
        Title = "Surface";

        Scaffold.SetIsNavBarVisible(this, false);

        var declareButton = new Button { Text = "Declare dark", AutomationId = "SysBarsDeclareDark", FontSize = 11 };
        declareButton.Clicked += (_, _) => Scaffold.SetSystemBarStyle(this, ScaffoldSystemBarStyle.DarkContent);

        var grid = new Grid
        {
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None),
            BackgroundColor = Colors.MidnightBlue
        };

        grid.Add(new VerticalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(16, 80, 16, 16),
            Children =
            {
                new Label { Text = "SysBarsSurface", AutomationId = "SysBarsSurface", TextColor = Colors.White, FontSize = 22, FontAttributes = FontAttributes.Bold },
                SysBarsProbeFactory.Make("Surface"),
                declareButton,
                NavPageFactory.MakeButton("Pop", "PopSysBarsSurface", model.Pop),
                SysBarsPageFactory.MakeExitButton("SysBarsSurface")
            }
        });

        Content = grid;
    }
}

/// <summary>
/// Bar-less page with NO usable surface: Auto falls back to the app theme. A button toggles
/// <see cref="Application.UserAppTheme"/> — the icons must follow the theme flip live.
/// </summary>
[UsedImplicitly]
public class SysBarsBarePage : ContentPage
{
    public SysBarsBarePage(SysBarsBarePageModel model)
    {
        BindingContext = model;
        Title = "Bare";

        Scaffold.SetIsNavBarVisible(this, false);

        var themeButton = new Button { Text = "Toggle theme", AutomationId = "SysBarsToggleTheme", FontSize = 11 };
        themeButton.Clicked += (_, _) =>
        {
            var application = Application.Current!;
            application.UserAppTheme = application.UserAppTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        };

        var resetThemeButton = new Button { Text = "Reset theme", AutomationId = "SysBarsResetTheme", FontSize = 11 };
        resetThemeButton.Clicked += (_, _) => Application.Current!.UserAppTheme = AppTheme.Unspecified;

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(16, 80, 16, 16),
            Children =
            {
                new Label { Text = "SysBarsBare", AutomationId = "SysBarsBare", FontSize = 22, FontAttributes = FontAttributes.Bold },
                SysBarsProbeFactory.Make("Bare"),
                themeButton,
                resetThemeButton,
                NavPageFactory.MakeButton("Pop", "PopSysBarsBare", model.Pop),
                SysBarsPageFactory.MakeExitButton("SysBarsBare")
            }
        };
    }
}

/// <summary>
/// Scaffold harness for the system-bar icon style (§ system bars): a light scaffold-level bar
/// (dark icons at home), a dark page bar (light icons), an overlap page whose declaration holds
/// until the bar materializes, a bar-less page resolved from its own surface then overridden by
/// a live declaration, a theme-fallback page, and a DARK start flyout whose opening must flip
/// the icons light over the light home bar.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold System Bar Tests")]
public class SystemBarScaffold : Scaffold
{
    public SystemBarScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Home", PageType = typeof(SysBarsHomePage) });

        // Drawers are opt-in (modes default to Disabled).
        SetFlyoutStartMode(this, ScaffoldFlyoutMode.Flyout);

        SetNavBarAppearance(
            this,
            new ScaffoldNavBarAppearance
            {
                Background = new SolidColorBrush(Colors.WhiteSmoke)
            }
        );

        var closeFlyoutButton = new Button { Text = "Close", AutomationId = "SysBarsFlyoutClose", FontSize = 11 };
        closeFlyoutButton.Clicked += (_, _) => _ = CloseFlyoutAsync();

        FlyoutStart = new VerticalStackLayout
        {
            BackgroundColor = Color.FromArgb("#1E1E28"),
            Spacing = 6,
            Padding = new Thickness(16, 80, 16, 16),
            Children =
            {
                new Label { Text = "SysBarsFlyout", AutomationId = "SysBarsFlyout", TextColor = Colors.White, FontSize = 18 },
                SysBarsProbeFactory.Make("Flyout"),
                closeFlyoutButton
            }
        };
    }
}
