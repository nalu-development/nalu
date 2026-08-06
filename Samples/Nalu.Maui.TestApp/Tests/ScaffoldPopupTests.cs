using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;
using Microsoft.Maui.Controls.Shapes;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public class PopupHomePageModel : ObservableObject;

[UsedImplicitly]
public class PopupOtherPageModel : ObservableObject;

/// <summary>
/// Popup harness page: center popup (stacking + navigation buttons inside), anchored dropdown
/// with a transparent scrim, and per-popup lifecycle labels driven by the handles' Closed task —
/// the UI-test observable of every close path (button, scrim tap, navigation).
/// </summary>
[UsedImplicitly]
public class PopupHomePage : ContentPage
{
    private readonly Label _centerState;
    private readonly Label _stackedState;
    private readonly Label _dropdownState;
    private readonly Button _dropdownAnchor;
    private IScaffoldPopup? _centerPopup;
    private IScaffoldPopup? _stackedPopup;
    private IScaffoldPopup? _dropdownPopup;

    public PopupHomePage(PopupHomePageModel model)
    {
        BindingContext = model;
        Title = "PopupHome";

        _centerState = new Label { AutomationId = "CenterPopupState", Text = "center:idle", FontSize = 12 };
        _stackedState = new Label { AutomationId = "StackedPopupState", Text = "stacked:idle", FontSize = 12 };
        _dropdownState = new Label { AutomationId = "DropdownPopupState", Text = "dropdown:idle", FontSize = 12 };

        var showCenterButton = new Button { Text = "Show center popup", AutomationId = "ShowCenterPopupButton", FontSize = 12 };
        showCenterButton.Clicked += async (_, _) => await ShowCenterPopupAsync();

        _dropdownAnchor = new Button { Text = "Show dropdown", AutomationId = "ShowDropdownButton", FontSize = 12, HorizontalOptions = LayoutOptions.Start };
        _dropdownAnchor.Clicked += async (_, _) => await ShowDropdownAsync();

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitPopupTests", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 16,
            Children =
            {
                new Label { Text = "Popup Home", AutomationId = "PopupHomePage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                showCenterButton,
                _dropdownAnchor,
                _centerState,
                _stackedState,
                _dropdownState,
                exitButton
            }
        };
    }

    private async Task ShowCenterPopupAsync()
    {
        var closeButton = new Button { Text = "Close", AutomationId = "CloseCenterPopupButton", FontSize = 12 };
        closeButton.Clicked += async (_, _) => await (_centerPopup?.CloseAsync() ?? Task.CompletedTask);

        var stackButton = new Button { Text = "Stack another", AutomationId = "OpenStackedPopupButton", FontSize = 12 };
        stackButton.Clicked += async (_, _) => await ShowStackedPopupAsync();

        var navigateButton = new Button { Text = "Navigate", AutomationId = "NavigateFromPopupButton", FontSize = 12 };
        navigateButton.Clicked += async (_, _) =>
        {
            // Engine-routed root selection: the navigation must dismiss every open popup.
            if (this.GetScaffoldOrDefault() is { } scaffold
                && scaffold.Areas.OfType<ScaffoldTabBar>().First() is { } tabBar)
            {
                await tabBar.SelectRootAsync(tabBar.Roots[1]);
            }
        };

        var content = BuildPopupSurface(
            "CenterPopupContent",
            new Label { Text = "Center popup", FontSize = 16, FontAttributes = FontAttributes.Bold },
            closeButton,
            stackButton,
            navigateButton
        );

        var scaffold = this.GetScaffold();
        _centerPopup = await scaffold.ShowPopupAsync(content);
        _centerState.Text = _centerPopup.IsOpen ? "center:open" : "center:failed";
        ObserveClose(_centerPopup, _centerState, "center");
    }

    private async Task ShowStackedPopupAsync()
    {
        var closeButton = new Button { Text = "Close stacked", AutomationId = "CloseStackedPopupButton", FontSize = 12 };
        closeButton.Clicked += async (_, _) => await (_stackedPopup?.CloseAsync() ?? Task.CompletedTask);

        var content = BuildPopupSurface(
            "StackedPopupContent",
            new Label { Text = "Stacked popup", FontSize = 16 },
            closeButton
        );

        var scaffold = this.GetScaffold();
        _stackedPopup = await scaffold.ShowPopupAsync(content);
        _stackedState.Text = _stackedPopup.IsOpen ? "stacked:open" : "stacked:failed";
        ObserveClose(_stackedPopup, _stackedState, "stacked");
    }

    private async Task ShowDropdownAsync()
    {
        var content = BuildPopupSurface(
            "DropdownContent",
            new Label { Text = "Option one", AutomationId = "DropdownOptionOne", FontSize = 14 },
            new Label { Text = "Option two", FontSize = 14 }
        );

        var scaffold = this.GetScaffold();

        _dropdownPopup = await scaffold.ShowPopupAsync(
            content,
            new ScaffoldPopupOptions
            {
                Placement = ScaffoldPopupPlacement.AnchorBelow,
                Anchor = _dropdownAnchor,
                Scrim = new SolidColorBrush(Colors.Transparent)
            }
        );

        _dropdownState.Text = _dropdownPopup.IsOpen ? "dropdown:open" : "dropdown:failed";
        ObserveClose(_dropdownPopup, _dropdownState, "dropdown");
    }

    private void ObserveClose(IScaffoldPopup popup, Label stateLabel, string name)
        => popup.Closed.ContinueWith(
            _ => Dispatcher.Dispatch(() => stateLabel.Text = $"{name}:closed"),
            TaskScheduler.Default
        );

    private static Border BuildPopupSurface(string automationId, params View[] children)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };

        foreach (var child in children)
        {
            stack.Add(child);
        }

        return new Border
        {
            AutomationId = automationId,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Background = new SolidColorBrush(Colors.White),
            Padding = 16,
            WidthRequest = 240,
            Content = stack
        };
    }
}

[UsedImplicitly]
public class PopupOtherPage : ContentPage
{
    public PopupOtherPage(PopupOtherPageModel model)
    {
        BindingContext = model;
        Title = "PopupOther";

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitPopupOther", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 16,
            Children =
            {
                new Label { Text = "Popup Other", AutomationId = "PopupOtherPage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                exitButton
            }
        };
    }
}

/// <summary>Scaffold harness of the popup system (§5.6 stack): two roots so navigation-dismissal is testable.</summary>
[UsedImplicitly]
[TestPage("Scaffold Popup Tests")]
public class PopupScaffold : Scaffold
{
    public PopupScaffold()
    {
        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "PopupHome", PageType = typeof(PopupHomePage) },
                    new ScaffoldRoot { Title = "PopupOther", PageType = typeof(PopupOtherPage) }
                }
            }
        );
    }
}
