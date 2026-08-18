using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

file static class AppearancePageFactory
{
    /// <summary>App-scope theme flip (UserAppTheme, like an in-app switcher) + reset.</summary>
    public static View MakeThemeButtons(string marker)
    {
        var toggle = new Button { Text = "Toggle app theme", AutomationId = $"ToggleTheme{marker}", FontSize = 11 };
        toggle.Clicked += (_, _) =>
        {
            var application = Application.Current!;
            application.UserAppTheme = (application.UserAppTheme == AppTheme.Unspecified ? application.RequestedTheme : application.UserAppTheme) == AppTheme.Dark
                ? AppTheme.Light
                : AppTheme.Dark;
        };

        var reset = new Button { Text = "Reset app theme", AutomationId = $"ResetTheme{marker}", FontSize = 11 };
        reset.Clicked += (_, _) => Application.Current!.UserAppTheme = AppTheme.Unspecified;

        return new HorizontalStackLayout { Spacing = 6, Children = { toggle, reset } };
    }

    /// <summary>The app-reset escape hatch NaluApp.ResetAsync relies on for Scaffold-hosted pages (no decorated ResetButton).</summary>
    public static Button MakeExitButton(string marker)
    {
        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{marker}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        return exitButton;
    }
}

[UsedImplicitly]
public partial class AppearanceHomePageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushStyled() => navigationService.GoToAsync(Navigation.Relative().Push<AppearanceStyledPageModel>());

    public Task PushOverlap() => navigationService.GoToAsync(Navigation.Relative().Push<AppearanceOverlapPageModel>());

    public Task PushScroll() => navigationService.GoToAsync(Navigation.Relative().Push<AppearanceScrollPageModel>());
}

[UsedImplicitly]
public partial class AppearanceScrollPageModel(INavigationService navigationService) : ObservableObject, IDisposable
{
    /// <summary>Lets the page register its own island (page/scroll view) at dispose time.</summary>
    public Action? DisposedCallback { get; set; }

    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());

    public void Dispose()
    {
        // The model covers its page (BindingContext) and, through it, the tracked ScrollView:
        // collection proves the scroll observation (iOS KVO token retains the UIScrollView)
        // was disposed on pop — a leaked observer would keep this whole chain alive.
        LeakTracker.ExpectCollected(this);
        DisposedCallback?.Invoke();
        GC.SuppressFinalize(this);
    }
}

[UsedImplicitly]
public partial class AppearanceOverlapPageModel(INavigationService navigationService) : ObservableObject, IDisposable
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());

    public void Dispose()
    {
        // Control sample: this page has NO scroll tracker and NO TitleView — if it leaks on
        // pop too, the retention is generic to the pop path, not the scroll observation.
        LeakTracker.ExpectCollected(this);
        GC.SuppressFinalize(this);
    }
}

[UsedImplicitly]
public partial class AppearanceStyledPageModel(INavigationService navigationService) : ObservableObject, IDisposable
{
    [ObservableProperty]
    private string _heading = "Model Heading";

    /// <summary>Lets the page register its TitleView at dispose time (leak diagnostics).</summary>
    public Action? DisposedCallback { get; set; }

    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());

    public void Dispose()
    {
        // Control sample: this TitleView binds the PAGE MODEL (no NavBarBinding) — if it
        // collects while the scroll page's NavBarBinding-bound title does not, the
        // relative-source binding is the retainer.
        LeakTracker.ExpectCollected(this);
        DisposedCallback?.Invoke();
        GC.SuppressFinalize(this);
    }
}

/// <summary>Root page: rides the scaffold-level appearance untouched.</summary>
[UsedImplicitly]
public class AppearanceHomePage : ContentPage
{
    public AppearanceHomePage(AppearanceHomePageModel model)
    {
        BindingContext = model;
        Title = "Appearance Home";

        // Resolved title color (hex): the theme-change tests read the scaffold-level
        // AppThemeBinding through it.
        var titleForegroundProbe = new Label { AutomationId = "AppearanceHomeTitleForeground", FontSize = 11 };
        titleForegroundProbe.SetBinding(Label.TextProperty, NavBarBindings.Create(nameof(ScaffoldNavBarContext.TitleForeground), converter: new ColorHexConverter()));

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = 16,
            Children =
            {
                new Label { Text = "AppearancePageHome", AutomationId = "AppearancePageHome", FontSize = 22, FontAttributes = FontAttributes.Bold },
                titleForegroundProbe,
                NavPageFactory.MakeButton("Push styled", "PushAppearanceStyled", model.PushStyled),
                NavPageFactory.MakeButton("Push overlap", "PushAppearanceOverlap", model.PushOverlap),
                NavPageFactory.MakeButton("Push scroll", "PushAppearanceScroll", model.PushScroll),
                AppearancePageFactory.MakeExitButton("AppearanceHome")
            }
        };
    }
}

/// <summary>
/// Pushed page in OVERLAP mode: the bar contributes no top inset — content starts at the very
/// top edge under a transparent bar (the full-bleed header recipe).
/// </summary>
[UsedImplicitly]
public class AppearanceOverlapPage : ContentPage
{
    public AppearanceOverlapPage(AppearanceOverlapPageModel model)
    {
        BindingContext = model;
        Title = "Overlap Title";

        Scaffold.SetNavBarOverlapsContent(this, true);

        Scaffold.SetNavBarAppearance(
            this,
            new ScaffoldNavBarAppearance
            {
                Background = new SolidColorBrush(Colors.Transparent),
                Foreground = Colors.White,
                // Title-only channel: buttons follow Foreground (white), the title goes gold.
                TitleForeground = Colors.Gold
            }
        );

        var header = new BoxView { Color = Colors.DarkSlateBlue, HeightRequest = 220, VerticalOptions = LayoutOptions.Start };

        var topMarker = new Label
        {
            Text = "AppearanceOverlapTop",
            AutomationId = "AppearanceOverlapTop",
            TextColor = Colors.White,
            FontSize = 12,
            VerticalOptions = LayoutOptions.Start
        };

        // Probes of the resolved context colors (hex), so tests can assert the two channels apart.
        var foregroundProbe = new Label { AutomationId = "AppearanceOverlapForeground", FontSize = 11 };
        foregroundProbe.SetBinding(Label.TextProperty, NavBarBindings.Create(nameof(ScaffoldNavBarContext.Foreground), converter: new ColorHexConverter()));
        var titleForegroundProbe = new Label { AutomationId = "AppearanceOverlapTitleForeground", FontSize = 11 };
        titleForegroundProbe.SetBinding(Label.TextProperty, NavBarBindings.Create(nameof(ScaffoldNavBarContext.TitleForeground), converter: new ColorHexConverter()));

        var body = new VerticalStackLayout
        {
            Padding = new Thickness(16, 240, 16, 16),
            Children =
            {
                foregroundProbe,
                titleForegroundProbe,
                AppearancePageFactory.MakeThemeButtons("AppearanceOverlap"),
                NavPageFactory.MakeButton("Pop", "PopAppearanceOverlap", model.Pop),
                AppearancePageFactory.MakeExitButton("AppearanceOverlap")
            }
        };

        // SafeAreaEdges.None: the marker must sit at the ABSOLUTE top edge (y = 0) — proving
        // the bar footprint (and its status-bar share) no longer insets the page.
        var grid = new Grid
        {
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None)
        };

        grid.Add(header);
        grid.Add(topMarker);
        grid.Add(body);

        Content = grid;
    }
}

/// <summary>
/// Pushed page carrying a page-level appearance DELTA (opacity + foreground) and a TitleView
/// bound to the PAGE MODEL — the §5.2 revision contract: TitleView content binds the page,
/// not the nav bar context.
/// </summary>
[UsedImplicitly]
public class AppearanceStyledPage : ContentPage
{
    public AppearanceStyledPage(AppearanceStyledPageModel model)
    {
        BindingContext = model;
        Title = "Styled Title";

        var appearance = new ScaffoldNavBarAppearance
        {
            Opacity = 0.5,
            Foreground = Colors.Red
        };

        Scaffold.SetNavBarAppearance(this, appearance);

        var titleView = new Label
        {
            AutomationId = "AppearanceTitleView",
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };
        titleView.SetBinding(Label.TextProperty, nameof(AppearanceStyledPageModel.Heading));
        Scaffold.SetTitleView(this, titleView);
        model.DisposedCallback = () => LeakTracker.ExpectCollected(titleView);

        // Live mutation: the appearance object is observable — the strip reacts per property.
        var mutateButton = new Button { Text = "Dim bar", AutomationId = "MutateAppearance", FontSize = 11 };
        mutateButton.Clicked += (_, _) => appearance.Opacity = 0.25;

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = 16,
            Children =
            {
                new Label { Text = "AppearancePageStyled", AutomationId = "AppearancePageStyled", FontSize = 22, FontAttributes = FontAttributes.Bold },
                mutateButton,
                NavPageFactory.MakeButton("Pop", "PopAppearanceStyled", model.Pop),
                AppearancePageFactory.MakeExitButton("AppearanceStyled")
            }
        };
    }
}

/// <summary>
/// Pushed page tracking its ScrollView through <see cref="Scaffold.ScrollTrackerProperty"/>:
/// the TitleView shows the live <see cref="ScaffoldNavBarContext.ScrollOffset"/> through a
/// <see cref="NavBarBindingExtension"/> — covering the scroll channel AND the markup extension
/// in one page. A page-side button scrolls deterministically (synthetic swipes vary per platform).
/// </summary>
[UsedImplicitly]
public class AppearanceScrollPage : ContentPage
{
    public AppearanceScrollPage(AppearanceScrollPageModel model)
    {
        BindingContext = model;
        Title = "Scroll Title";

        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };

        var scrollDownButton = new Button { Text = "Scroll down", AutomationId = "ScrollTrackedDown", FontSize = 11 };

        stack.Add(new Label { Text = "AppearancePageScroll", AutomationId = "AppearancePageScroll", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(scrollDownButton);
        stack.Add(NavPageFactory.MakeButton("Pop", "PopAppearanceScroll", model.Pop));
        stack.Add(AppearancePageFactory.MakeExitButton("AppearanceScroll"));

        for (var i = 0; i < 30; i++)
        {
            stack.Add(new BoxView { HeightRequest = 80, Color = i % 2 == 0 ? Colors.LightGray : Colors.Silver });
        }

        var scrollView = new ScrollView { Content = stack };
        scrollDownButton.Clicked += (_, _) => _ = scrollView.ScrollToAsync(0, 400, animated: false);

        Scaffold.SetScrollTracker(this, scrollView);

        // The TitleView reads the context's live offset through the markup extension (built in
        // code: ProvideValue tolerates a null provider by design).
        var offsetLabel = new Label
        {
            AutomationId = "ScrollOffsetTitle",
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            VerticalTextAlignment = TextAlignment.Center
        };
        offsetLabel.SetBinding(Label.TextProperty, NavBarBindings.Create(nameof(ScaffoldNavBarContext.ScrollOffset), stringFormat: "{0:F0}"));
        Scaffold.SetTitleView(this, offsetLabel);

        // Diagnostic granularity: tracking the island members separately tells WHICH link leaks
        // (page/scroll/title view vs. just the model) when the check fails.
        model.DisposedCallback = () =>
        {
            LeakTracker.ExpectCollected(this);
            LeakTracker.ExpectCollected(scrollView);
            LeakTracker.ExpectCollected(offsetLabel);
        };

        Content = scrollView;
    }
}

/// <summary>
/// Scaffold harness exercising the nav bar appearance chain (§5.2 revision): a scaffold-level
/// appearance as the global surface, a page-level delta (opacity/foreground), live mutation of
/// a page appearance, the TitleView page-model binding contract, and the scroll channel.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold NavBar Appearance Tests")]
public class NavBarAppearanceScaffold : Scaffold
{
    public NavBarAppearanceScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Home", PageType = typeof(AppearanceHomePage) });

        // Theme-aware global surface, the DailyHelper/template shape: the brush Color and the
        // title color carry AppThemeBindings. They must keep following UserAppTheme even while a
        // page-level appearance (the overlap page's transparent bar) is the one presented.
        var surface = new SolidColorBrush();
        surface.SetAppThemeColor(SolidColorBrush.ColorProperty, Colors.LightSteelBlue, Colors.DarkSlateBlue);

        var appearance = new ScaffoldNavBarAppearance { Background = surface };
        appearance.SetAppThemeColor(ScaffoldNavBarAppearance.TitleForegroundProperty, Colors.Black, Colors.White);
        SetNavBarAppearance(this, appearance);
    }
}

file sealed class ColorHexConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is Color color ? color.ToArgbHex() : "null";

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture) => throw new NotSupportedException();
}
