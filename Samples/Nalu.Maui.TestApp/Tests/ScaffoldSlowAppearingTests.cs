using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public class SlowAppearHomePageModel(INavigationService navigation) : ObservableObject
{
    public Task PushSlow() => navigation.GoToAsync(Navigation.Relative().Push<SlowAppearDetailPageModel>());
}

/// <summary>A page whose OnAppearingAsync takes 2.5 s: the navigation is IN FLIGHT for that long after the page is on screen.</summary>
[UsedImplicitly]
public partial class SlowAppearDetailPageModel(INavigationService navigation) : ObservableObject, IAppearingAware
{
    [ObservableProperty]
    private string _state = "slow:entering";

    public async ValueTask OnAppearingAsync()
    {
        State = "slow:appearing";
        await Task.Delay(2500);
        State = "slow:appeared";
    }

    public Task Pop() => navigation.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public class SlowAppearHomePage : ContentPage
{
    public SlowAppearHomePage(SlowAppearHomePageModel model)
    {
        BindingContext = model;
        Title = "SlowHome";

        var exit = new Button { Text = "Exit", AutomationId = "ExitSlowAppearTests", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exit.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 16,
            Children =
            {
                new Label { Text = "Slow appear home", AutomationId = "SlowAppearHomePage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                NavPageFactory.MakeButton("Push slow page", "PushSlowAppearButton", model.PushSlow),
                exit
            }
        };
    }
}

[UsedImplicitly]
public class SlowAppearDetailPage : ContentPage
{
    public SlowAppearDetailPage(SlowAppearDetailPageModel model)
    {
        BindingContext = model;
        Title = "SlowDetail";

        var state = new Label { AutomationId = "SlowAppearState", FontSize = 14 };
        state.SetBinding(Label.TextProperty, nameof(SlowAppearDetailPageModel.State));

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 16,
            BackgroundColor = Colors.LightGoldenrodYellow,
            Children =
            {
                new Label { Text = "Slow appear detail", AutomationId = "SlowAppearDetailPage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                state,
                NavPageFactory.MakeButton("Pop", "PopSlowAppearButton", model.Pop)
            }
        };
    }
}

/// <summary>Harness: a pushed page whose appearing takes seconds — the back PREVIEW must not run while the navigation is in flight.</summary>
[UsedImplicitly]
[TestPage("Scaffold Slow Appearing Tests")]
public class SlowAppearScaffold : Scaffold
{
    public SlowAppearScaffold()
    {
        Areas.Add(new ScaffoldRoot { Title = "Home", PageType = typeof(SlowAppearHomePage) });
    }
}
