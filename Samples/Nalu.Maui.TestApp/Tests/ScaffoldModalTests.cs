using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class ModalHomePageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushDismissable() => navigationService.GoToAsync(Navigation.Relative().Push<DismissableModalPageModel>());

    public Task PushPlainModal() => navigationService.GoToAsync(Navigation.Relative().Push<PlainModalPageModel>());
}

[UsedImplicitly]
public partial class DismissableModalPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class PlainModalPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

/// <summary>Tab root of the modal harness.</summary>
[UsedImplicitly]
public class ModalHomePage : ContentPage
{
    public ModalHomePage(ModalHomePageModel model)
    {
        BindingContext = model;
        Title = "Modal Home";

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "ModalHomePage", AutomationId = "ModalHomePage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Push dismissable modal", "PushDismissableModal", model.PushDismissable));
        stack.Add(NavPageFactory.MakeButton("Push plain modal", "PushPlainModal", model.PushPlainModal));

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitModalHome", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = new ScrollView { Content = stack };
    }
}

/// <summary>Secondary tab root (present so the harness has a real tab bar to cover).</summary>
[UsedImplicitly]
public class ModalOtherPage : ContentPage
{
    public ModalOtherPage()
    {
        Title = "Other";

        Content = new VerticalStackLayout
        {
            Padding = 16,
            Children = { new Label { Text = "ModalOtherPage", AutomationId = "ModalOtherPage", FontSize = 22 } }
        };
    }
}

/// <summary>
/// <see cref="ScaffoldPageMode.DismissableModal"/> page: slides up from the bottom, covers the
/// tab bar, title-only nav bar with a trailing X.
/// </summary>
[UsedImplicitly]
public class DismissableModalPage : ContentPage
{
    public DismissableModalPage(DismissableModalPageModel model)
    {
        BindingContext = model;
        Title = "Dismissable";
        Scaffold.SetPageMode(this, ScaffoldPageMode.DismissableModal);
        BackgroundColor = Colors.MintCream;

        Content = new VerticalStackLayout
        {
            Spacing = 12,
            Padding = 16,
            Children =
            {
                new Label { Text = "DismissableModalPage", AutomationId = "DismissableModalPage", FontSize = 22, FontAttributes = FontAttributes.Bold }
            }
        };
    }
}

/// <summary>
/// <see cref="ScaffoldPageMode.Modal"/> page: no X — dismissal is programmatic only (or
/// Android system back through the engine).
/// </summary>
[UsedImplicitly]
public class PlainModalPage : ContentPage
{
    public PlainModalPage(PlainModalPageModel model)
    {
        BindingContext = model;
        Title = "Plain modal";
        Scaffold.SetPageMode(this, ScaffoldPageMode.Modal);
        BackgroundColor = Colors.Lavender;

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "PlainModalPage", AutomationId = "PlainModalPage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Close", "ClosePlainModal", model.Pop));

        Content = stack;
    }
}

/// <summary>
/// Scaffold harness exercising modal presentation (§7.1): a tab bar that modal pages must
/// cover, a DismissableModal (X button) and a plain Modal (programmatic close only).
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Modal Tests")]
public class ModalScaffold : Scaffold
{
    public ModalScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldTabBar
        {
            Roots =
            {
                new ScaffoldRoot { Title = "MHome", PageType = typeof(ModalHomePage) },
                new ScaffoldRoot { Title = "MOther", PageType = typeof(ModalOtherPage) }
            }
        });
    }
}
