using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class AmPlainRootPageModel(INavigationService navigationService) : ObservableObject, IAppearingAware
{
    public Task PushModal() => navigationService.GoToAsync(Navigation.Relative().Push<AmModalPageModel>());

    public Task GoTabRoot() => navigationService.GoToAsync(Navigation.Absolute().Root<AmTabOnePageModel>());

    /// <summary>Armed by the modal on its way out: the switch is dispatched from APPEARING.</summary>
    internal static bool SwitchRequested { get; set; }

    /// <summary>
    /// The shape a real app hits: the modal hands a result back, and the page it uncovers reacts
    /// to it in Appearing by navigating somewhere else — a navigation dispatched from INSIDE the
    /// lifecycle of the one still committing.
    /// </summary>
    public async ValueTask OnAppearingAsync()
    {
        if (!SwitchRequested)
        {
            return;
        }

        SwitchRequested = false;

        // INLINE, not dispatched: the switch begins while the pop that raised this callback is
        // still committing — a navigation re-entering the engine from inside another one.
        await navigationService.GoToAsync(Navigation.Absolute().Root<AmTabOnePageModel>());
    }
}

[UsedImplicitly]
public partial class AmModalPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Close() => navigationService.GoToAsync(Navigation.Relative().Pop());

    /// <summary>Closes with a "result": the page below switches area from its Appearing.</summary>
    public async Task CloseWithResult()
    {
        AmPlainRootPageModel.SwitchRequested = true;

        await navigationService.GoToAsync(Navigation.Relative().Pop());
    }

    /// <summary>Straight from the modal to the other area's root: one navigation, not two.</summary>
    public Task GoTabRoot() => navigationService.GoToAsync(Navigation.Absolute().Root<AmTabOnePageModel>());

    /// <summary>Pop, then switch — the two chained back to back, as an app closing a modal does.</summary>
    public async Task CloseThenGoTabRoot()
    {
        await navigationService.GoToAsync(Navigation.Relative().Pop());
        await navigationService.GoToAsync(Navigation.Absolute().Root<AmTabOnePageModel>());
    }
}

/// <summary>Records the lifecycle a page model actually receives, for the harness label.</summary>
[UsedImplicitly]
public partial class AmTabOnePageModel : ObservableObject, IEnteringAware, IAppearingAware
{
    [ObservableProperty]
    public partial string Lifecycle { get; set; } = "E0 A0";

    private int _entering;
    private int _appearing;

    public ValueTask OnEnteringAsync()
    {
        _entering++;
        Lifecycle = $"E{_entering} A{_appearing}";

        return ValueTask.CompletedTask;
    }

    public ValueTask OnAppearingAsync()
    {
        _appearing++;
        Lifecycle = $"E{_entering} A{_appearing}";

        return ValueTask.CompletedTask;
    }
}

[UsedImplicitly]
public partial class AmTabTwoPageModel : ObservableObject;

/// <summary>Root of the FIRST area, which is a plain root (no tab bar of its own).</summary>
[UsedImplicitly]
public class AmPlainRootPage : ContentPage
{
    public AmPlainRootPage(AmPlainRootPageModel model)
    {
        BindingContext = model;
        Title = "Plain root";
        BackgroundColor = Colors.WhiteSmoke;

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "AmPlainRootPage", AutomationId = "AmPlainRootPage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Push modal", "PushAmModal", model.PushModal));
        stack.Add(NavPageFactory.MakeButton("Go tab root", "GoAmTabRoot", model.GoTabRoot));

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitAmPlainRoot", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }
}

/// <summary>The modal pushed on the plain root, closed programmatically.</summary>
[UsedImplicitly]
public class AmModalPage : ContentPage
{
    public AmModalPage(AmModalPageModel model)
    {
        BindingContext = model;
        Title = "Modal";
        Scaffold.SetPageMode(this, ScaffoldPageMode.Modal);
        BackgroundColor = Colors.Lavender;

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "AmModalPage", AutomationId = "AmModalPage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Close", "CloseAmModal", model.Close));
        stack.Add(NavPageFactory.MakeButton("Go tab root", "GoAmTabRootFromModal", model.GoTabRoot));
        stack.Add(NavPageFactory.MakeButton("Close then go", "CloseThenGoAmTabRoot", model.CloseThenGoTabRoot));
        stack.Add(NavPageFactory.MakeButton("Close with result", "CloseAmModalWithResult", model.CloseWithResult));

        Content = stack;
    }
}

/// <summary>First root of the TAB BAR area — the page that must appear after the cross-area switch.</summary>
[UsedImplicitly]
public class AmTabOnePage : ContentPage
{
    public AmTabOnePage(AmTabOnePageModel model)
    {
        BindingContext = model;
        Title = "Tab one";
        BackgroundColor = Colors.LightGoldenrodYellow;

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "AmTabOnePage", AutomationId = "AmTabOnePage", FontSize = 22, FontAttributes = FontAttributes.Bold });

        // The lifecycle the page model actually received: the symptom under test is Entering
        // firing while Appearing never does, with the page nowhere on screen.
        var lifecycle = new Label { AutomationId = "AmTabOneLifecycle", FontSize = 16 };
        lifecycle.SetBinding(Label.TextProperty, new Binding(nameof(AmTabOnePageModel.Lifecycle)));
        stack.Add(lifecycle);

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitAmTabOne", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }
}

[UsedImplicitly]
public class AmTabTwoPage : ContentPage
{
    public AmTabTwoPage(AmTabTwoPageModel model)
    {
        BindingContext = model;
        Title = "Tab two";
        BackgroundColor = Colors.LightCyan;

        Content = new VerticalStackLayout
                  {
                      Spacing = 12,
                      Padding = 16,
                      Children = { new Label { Text = "AmTabTwoPage", AutomationId = "AmTabTwoPage", FontSize = 22 } }
                  };
    }
}

/// <summary>
/// Harness for a cross-area switch that follows a MODAL round trip, the shape a real app hits at
/// startup: the first area is a PLAIN root (no tab bar), which pushes a modal; once the modal is
/// popped, the app navigates to the first root of a TAB BAR area. The page must actually appear —
/// Entering firing while Appearing never does, with nothing on screen, is the failure this
/// harness exists to catch.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Area Modal Tests")]
public class AreaModalScaffold : Scaffold
{
    public AreaModalScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Plain", PageType = typeof(AmPlainRootPage) });

        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "One", PageType = typeof(AmTabOnePage) },
                    new ScaffoldRoot { Title = "Two", PageType = typeof(AmTabTwoPage) }
                }
            }
        );
    }
}
