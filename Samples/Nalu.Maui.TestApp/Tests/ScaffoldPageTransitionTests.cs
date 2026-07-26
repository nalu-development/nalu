using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class PtRootPageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushSlideUp() => navigationService.GoToAsync(Navigation.Relative().Push<PtSlideUpPageModel>());

    public Task PushZoom() => navigationService.GoToAsync(Navigation.Relative().Push<PtZoomPageModel>());
}

[UsedImplicitly]
public partial class PtSlideUpPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class PtZoomPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

/// <summary>Root of the page-transition harness (default spec).</summary>
[UsedImplicitly]
public class PtRootPage : ContentPage
{
    public PtRootPage(PtRootPageModel model)
    {
        BindingContext = model;
        Title = "Transitions";

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "PtRootPage", AutomationId = "PtRootPage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Push slide-up", "PushPtSlideUp", model.PushSlideUp));
        stack.Add(NavPageFactory.MakeButton("Push zoom", "PushPtZoom", model.PushZoom));

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitPtRoot", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = new ScrollView { Content = stack };
    }
}

/// <summary>Pushed page declaring <see cref="ScaffoldPageTransition.SlideUpFade"/> (§8.2).</summary>
[UsedImplicitly]
public class PtSlideUpPage : ContentPage
{
    public PtSlideUpPage(PtSlideUpPageModel model)
    {
        BindingContext = model;
        Title = "Slide up";
        Scaffold.SetPageTransition(this, ScaffoldPageTransition.SlideUpFade);
        BackgroundColor = Colors.LightSteelBlue;

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "PtSlideUpPage", AutomationId = "PtSlideUpPage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Pop", "PopPtSlideUp", model.Pop));

        Content = new ScrollView { Content = stack };
    }
}

/// <summary>Pushed page declaring <see cref="ScaffoldPageTransition.ZoomFade"/> (§8.2).</summary>
[UsedImplicitly]
public class PtZoomPage : ContentPage
{
    public PtZoomPage(PtZoomPageModel model)
    {
        BindingContext = model;
        Title = "Zoom";
        Scaffold.SetPageTransition(this, ScaffoldPageTransition.ZoomFade);
        BackgroundColor = Colors.PapayaWhip;

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "PtZoomPage", AutomationId = "PtZoomPage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Pop", "PopPtZoom", model.Pop));

        Content = new ScrollView { Content = stack };
    }
}

/// <summary>
/// Scaffold harness exercising the §8.2 declarative page-transition spec: per-page
/// SlideUpFade and ZoomFade overrides on pushed pages (push plays Enter, pop replays it
/// reversed; the behind page plays the Behind motion both ways).
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Page Transition Tests")]
public class PageTransitionScaffold : Scaffold
{
    public PageTransitionScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Transitions", PageType = typeof(PtRootPage) });
    }
}
