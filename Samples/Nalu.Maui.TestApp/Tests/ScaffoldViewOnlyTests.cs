using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

// View-only navigation harness: NO page has a BindingContext — the pages themselves are the
// navigation lifecycle targets (IEnteringAware/IAppearingAware/ILeavingGuard/intents implemented
// directly on the page). Pages are registered with the view-only AddPage<TPage>() overload in
// MauiProgram and created by the engine through DI (constructor-injected INavigationService).
// Navigations use the shorthand entry points through the `Nav` alias (see GlobalUsings.cs):
// `Nav.Push<T>(intent)` ≡ `Navigation.Relative().Push<T>().WithIntent(intent)`.

/// <summary>
/// View-only root page: lifecycle counters rendered by the page itself, a push-with-intent and
/// a push-to-guard-page button.
/// </summary>
[UsedImplicitly]
public class ViewOnlyOnePage : ContentPage, IEnteringAware, IAppearingAware, IDisappearingAware
{
    private readonly Label _lifecycleLabel;
    private int _entering;
    private int _appearing;
    private int _disappearing;

    public ViewOnlyOnePage(INavigationService navigationService)
    {
        Title = "One";

        _lifecycleLabel = new Label { AutomationId = "ViewOnlyOneLifecycle", FontSize = 14 };

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = 16,
            Children =
            {
                new Label { Text = "View-only ONE root", AutomationId = "ViewOnlyOnePage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                _lifecycleLabel,
                NavPageFactory.MakeButton("Push detail (intent 42)", "PushViewOnlyDetail", () => navigationService.GoToAsync(Nav.Push<ViewOnlyDetailPage>(42))),
                NavPageFactory.MakeButton("Push guard page", "PushViewOnlyGuard", () => navigationService.GoToAsync(Nav.Push<ViewOnlyGuardPage>())),
                MakeExitButton("ExitViewOnlyOne")
            }
        };
    }

    public ValueTask OnEnteringAsync()
    {
        ++_entering;
        UpdateLifecycleLabel();

        return default;
    }

    public ValueTask OnAppearingAsync()
    {
        ++_appearing;
        UpdateLifecycleLabel();

        return default;
    }

    public ValueTask OnDisappearingAsync()
    {
        ++_disappearing;
        UpdateLifecycleLabel();

        return default;
    }

    private void UpdateLifecycleLabel() => _lifecycleLabel.Text = $"E{_entering} A{_appearing} D{_disappearing}";

    internal static Button MakeExitButton(string automationId)
    {
        var exitButton = new Button { Text = "Exit", AutomationId = automationId, FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        return exitButton;
    }
}

/// <summary>Second view-only root: the tab-switch target for stack-preservation checks.</summary>
[UsedImplicitly]
public class ViewOnlyTwoPage : ContentPage
{
    public ViewOnlyTwoPage()
    {
        Title = "Two";

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = 16,
            Children =
            {
                new Label { Text = "View-only TWO root", AutomationId = "ViewOnlyTwoPage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                ViewOnlyOnePage.MakeExitButton("ExitViewOnlyTwo")
            }
        };
    }
}

/// <summary>
/// View-only pushed page receiving a typed intent directly on the page
/// (<see cref="IEnteringAware{TIntent}"/> with no page model).
/// </summary>
[UsedImplicitly]
public class ViewOnlyDetailPage : ContentPage, IEnteringAware<int>
{
    private readonly Label _intentLabel;

    public ViewOnlyDetailPage(INavigationService navigationService)
    {
        Title = "Detail";

        _intentLabel = new Label { AutomationId = "ViewOnlyDetailIntent", FontSize = 14 };

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = 16,
            Children =
            {
                new Label { Text = "View-only detail", AutomationId = "ViewOnlyDetailPage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                _intentLabel,
                NavPageFactory.MakeButton("Pop", "PopViewOnlyDetail", () => navigationService.GoToAsync(Nav.Pop())),
                ViewOnlyOnePage.MakeExitButton("ExitViewOnlyDetail")
            }
        };
    }

    public ValueTask OnEnteringAsync(int intent)
    {
        _intentLabel.Text = intent.ToString();

        return default;
    }
}

/// <summary>
/// View-only pushed page implementing <see cref="ILeavingGuard"/> directly: every leave path
/// (pop button, system back, edge swipe) must consult the PAGE's guard. Starts in DENY mode.
/// </summary>
[UsedImplicitly]
public class ViewOnlyGuardPage : ContentPage, ILeavingGuard
{
    private readonly Label _checksLabel;
    private readonly Label _allowLabel;
    private int _checks;
    private bool _allowLeave;

    public ViewOnlyGuardPage(INavigationService navigationService)
    {
        Title = "Guard";

        _checksLabel = new Label { AutomationId = "ViewOnlyGuardChecks", Text = "0", FontSize = 14 };
        _allowLabel = new Label { AutomationId = "ViewOnlyGuardAllow", Text = "deny", FontSize = 14 };

        var toggleButton = new Button { Text = "Toggle allow", AutomationId = "ViewOnlyGuardToggle", FontSize = 11 };
        toggleButton.Clicked += (_, _) =>
        {
            _allowLeave = !_allowLeave;
            _allowLabel.Text = _allowLeave ? "allow" : "deny";
        };

        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = 16,
            Children =
            {
                new Label { Text = "View-only guard", AutomationId = "ViewOnlyGuardPage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                _checksLabel,
                _allowLabel,
                toggleButton,
                NavPageFactory.MakeButton("Pop", "PopViewOnlyGuard", () => navigationService.GoToAsync(Nav.Pop())),
                ViewOnlyOnePage.MakeExitButton("ExitViewOnlyGuard")
            }
        };
    }

    public ValueTask<bool> CanLeaveAsync()
    {
        _checksLabel.Text = (++_checks).ToString();

        return ValueTask.FromResult(_allowLeave);
    }
}

/// <summary>
/// Scaffold harness for view-only navigation: two tab roots, both plain pages with no page
/// models — lifecycle, intents, guards and tab-stack preservation all ride the page-as-target
/// path.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold View Only Tests")]
public class ViewOnlyScaffold : Scaffold
{
    public ViewOnlyScaffold()
    {
        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    MakeRoot<ViewOnlyOnePage>("One", "\ue88a"), // home
                    MakeRoot<ViewOnlyTwoPage>("Two", "\ue8b6") // search
                }
            }
        );
    }

    private static ScaffoldRoot MakeRoot<TPage>(string title, string glyph)
        where TPage : Page
        => new()
        {
            Title = title,
            PageType = typeof(TPage),
            Icon = new FontImageSource
            {
                FontFamily = "Material",
                Glyph = glyph,
                Color = Color.FromArgb("#8A8A8E"),
                Size = 24
            }
        };
}
