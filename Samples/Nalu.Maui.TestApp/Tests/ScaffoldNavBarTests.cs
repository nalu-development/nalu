using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class NavBarHomePageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushDetail() => navigationService.GoToAsync(Navigation.Relative().Push<NavBarDetailPageModel>());

    public Task PushDrawerDetail() => navigationService.GoToAsync(Navigation.Relative().Push<NavBarDrawerDetailPageModel>());

    public Task PushCustomBar() => navigationService.GoToAsync(Navigation.Relative().Push<NavBarCustomPageModel>());
}

[UsedImplicitly]
public partial class NavBarDetailPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class NavBarDrawerDetailPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class NavBarCustomPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

file static class NavBarPageFactory
{
    public static View BuildContent(string marker, params View[] extraViews)
    {
        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };

        stack.Add(new Label { Text = marker, AutomationId = marker, FontSize = 22, FontAttributes = FontAttributes.Bold });

        foreach (var view in extraViews)
        {
            stack.Add(view);
        }

        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{marker}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        return new ScrollView
        {
            Content = stack
        };
    }
}

/// <summary>Root page: title, pushes, nav bar visibility toggle.</summary>
[UsedImplicitly]
public class NavBarHomePage : ContentPage
{
    public NavBarHomePage(NavBarHomePageModel model)
    {
        BindingContext = model;
        Title = "Home Title";

        var toggleButton = new Button { Text = "Toggle nav bar", AutomationId = "ToggleNavBar", FontSize = 11 };
        toggleButton.Clicked += (_, _) => Scaffold.SetIsNavBarVisible(this, !Scaffold.GetIsNavBarVisible(this));

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageHome",
            NavPageFactory.MakeButton("Push detail", "PushNavBarDetail", model.PushDetail),
            NavPageFactory.MakeButton("Push drawer detail", "PushNavBarDrawerDetail", model.PushDrawerDetail),
            NavPageFactory.MakeButton("Push custom bar", "PushNavBarCustom", model.PushCustomBar),
            toggleButton
        );
    }
}

/// <summary>Pushed page: back button appears; the start-drawer button yields (Auto policy).</summary>
[UsedImplicitly]
public class NavBarDetailPage : ContentPage
{
    public NavBarDetailPage(NavBarDetailPageModel model)
    {
        BindingContext = model;
        Title = "Detail Title";

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageDetail",
            NavPageFactory.MakeButton("Pop", "PopNavBarDetail", model.Pop)
        );
    }
}

/// <summary>
/// Pushed page opting the start-drawer button into <see cref="ScaffoldFlyoutButtonVisibility.Visible"/>:
/// drawer and back buttons render side by side.
/// </summary>
[UsedImplicitly]
public class NavBarDrawerDetailPage : ContentPage
{
    public NavBarDrawerDetailPage(NavBarDrawerDetailPageModel model)
    {
        BindingContext = model;
        Title = "Drawer Detail";

        Scaffold.SetFlyoutStartButtonVisibility(this, ScaffoldFlyoutButtonVisibility.Visible);

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageDrawerDetail",
            NavPageFactory.MakeButton("Pop", "PopNavBarDrawerDetail", model.Pop)
        );
    }
}

/// <summary>Pushed page installing a page-level CUSTOM nav bar built from the public primitives.</summary>
[UsedImplicitly]
public class NavBarCustomPage : ContentPage
{
    public NavBarCustomPage(NavBarCustomPageModel model)
    {
        BindingContext = model;
        Title = "Custom Title";

        var customBar = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions = { new RowDefinition(GridLength.Auto) },
            BackgroundColor = Colors.MediumPurple,
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container)
        };

        customBar.Add(new ScaffoldBackButton { AutomationId = "CustomNavBarBack" }, 0);

        customBar.Add(
            new Label
            {
                Text = "Custom bar",
                AutomationId = "CustomNavBarMarker",
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 44,
                VerticalTextAlignment = TextAlignment.Center
            },
            1
        );

        Scaffold.SetNavBarView(this, customBar);

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageCustom",
            NavPageFactory.MakeButton("Pop", "PopNavBarCustom", model.Pop)
        );
    }
}

/// <summary>
/// Scaffold harness exercising the default nav bar (§5.2): a single plain area (no tab bar),
/// a global start flyout feeding the drawer button, titles per page, back/pop through the
/// nav bar, per-page drawer-button policy, per-page custom bar, visibility toggling.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold NavBar Tests")]
public class NavBarScaffold : Scaffold
{
    public NavBarScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Home", PageType = typeof(NavBarHomePage) });

        // Closed flyouts keep stale element-tree bounds (only the platform view unmounts):
        // the close handler records deterministic completion for the UI tests instead.
        var stateLabel = new Label { Text = "-", AutomationId = "NavFlyoutState", FontSize = 11 };

        var closeButton = NavPageFactory.MakeButton(
            "Close",
            "CloseNavFlyout",
            async () =>
            {
                await CloseFlyoutAsync();
                stateLabel.Text = "closed";
            }
        );

        // Mode Flyout: available on every page (the drawer requires content + an enabling mode).
        Scaffold.SetFlyoutStartMode(this, ScaffoldFlyoutMode.Flyout);

        FlyoutStart = new VerticalStackLayout
        {
            AutomationId = "GlobalNavFlyout",
            BackgroundColor = Colors.White,
            Padding = 16,
            Spacing = 8,
            Children =
            {
                new Label { Text = "Nav flyout", AutomationId = "GlobalNavFlyoutLabel", FontSize = 18, FontAttributes = FontAttributes.Bold },
                closeButton,
                stateLabel
            }
        };
    }
}
