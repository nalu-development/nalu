using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;
using Microsoft.Maui.Controls.Shapes;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class TransitionGridPageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushDetail() => navigationService.GoToAsync(Navigation.Relative().Push<TransitionDetailPageModel>());
}

[UsedImplicitly]
public partial class TransitionDetailPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

/// <summary>
/// "Grid" page of the shared-element scenario (§8 PoC shape): a small rounded hero image
/// (AspectFill) and a small title, both carrying <see cref="Scaffold.TransitionNameProperty"/>.
/// </summary>
[UsedImplicitly]
public class TransitionGridPage : ContentPage
{
    public TransitionGridPage(TransitionGridPageModel model)
    {
        BindingContext = model;
        Title = "Transitions";

        var heroImage = new Image
        {
            Source = "banner.png",
            Aspect = Aspect.AspectFill,
            WidthRequest = 120,
            HeightRequest = 80,
            AutomationId = "GridHeroImage"
        };
        Scaffold.SetTransitionName(heroImage, "heroImage");

        var imageCard = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            HorizontalOptions = LayoutOptions.Start,
            Content = heroImage
        };

        var heroTitle = new Label
        {
            Text = "Bot hero",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            // Start, not the stack's default Fill: a shared element flies its VIEW bounds, and a
            // Fill label is mostly empty space, so the pair would differ only in height and the
            // glyphs would stretch vertically instead of scaling.
            HorizontalOptions = LayoutOptions.Start,
            AutomationId = "GridHeroTitle"
        };
        Scaffold.SetTransitionName(heroTitle, "heroTitle");

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "TransitionGridPage", AutomationId = "TransitionGridPage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(imageCard);
        stack.Add(heroTitle);
        stack.Add(NavPageFactory.MakeButton("Push detail", "PushTransitionDetail", model.PushDetail));

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitTransitionGrid", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = new ScrollView
        {
            Content = stack
        };
    }
}

/// <summary>
/// Detail page of the shared-element scenario: the same hero image full-width (AspectFit —
/// exercising the aspect morph) and the title large.
/// </summary>
[UsedImplicitly]
public class TransitionDetailPage : ContentPage
{
    public TransitionDetailPage(TransitionDetailPageModel model)
    {
        BindingContext = model;
        Title = "Detail";

        var heroImage = new Image
        {
            Source = "banner.png",
            Aspect = Aspect.AspectFit,
            HeightRequest = 220,
            AutomationId = "DetailHeroImage"
        };
        Scaffold.SetTransitionName(heroImage, "heroImage");

        var heroTitle = new Label
        {
            Text = "Bot hero",
            FontSize = 30,
            FontAttributes = FontAttributes.Bold,
            // Start on BOTH sides of the pair — see the grid page.
            HorizontalOptions = LayoutOptions.Start,
            AutomationId = "DetailHeroTitle"
        };
        Scaffold.SetTransitionName(heroTitle, "heroTitle");

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(heroImage);
        stack.Add(heroTitle);
        stack.Add(new Label { Text = "TransitionDetailPage", AutomationId = "TransitionDetailPage", FontSize = 14 });
        stack.Add(NavPageFactory.MakeButton("Pop", "PopTransitionDetail", model.Pop));

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitTransitionDetail", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);


        Content = new ScrollView
        {
            Content = stack
        };
    }
}

/// <summary>
/// Scaffold harness exercising shared-element transitions (§8): grid → detail with a matching
/// image pair (fill→fit aspect morph, corner radius from the live views) and a matching label
/// pair (transform-match cross-fade), riding the standard push/pop slides.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Transition Tests")]
public class TransitionScaffold : Scaffold
{
    public TransitionScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Transitions", PageType = typeof(TransitionGridPage) });
    }
}
