using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;
using Microsoft.Maui.Controls.Shapes;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public class SheetHomePageModel : ObservableObject;

[UsedImplicitly]
public class SheetOtherPageModel : ObservableObject;

/// <summary>
/// Bottom sheet harness page: a content-hugging sheet (close button, popup-over-sheet,
/// navigation), a two-detent sheet with programmatic snap buttons, and lifecycle labels driven
/// by the handles' Closed task.
/// </summary>
[UsedImplicitly]
public class SheetHomePage : ContentPage
{
    private readonly Label _sheetState;
    private readonly Label _detentState;
    private readonly Label _popupState;
    private IScaffoldPopup? _sheet;
    private IScaffoldPopup? _detentSheet;
    private IScaffoldPopup? _popup;

    public SheetHomePage(SheetHomePageModel model)
    {
        BindingContext = model;
        Title = "SheetHome";

        _sheetState = new Label { AutomationId = "SheetState", Text = "sheet:idle", FontSize = 12 };
        _detentState = new Label { AutomationId = "DetentSheetState", Text = "detent:idle", FontSize = 12 };
        _popupState = new Label { AutomationId = "SheetPopupState", Text = "popup:idle", FontSize = 12 };

        var showSheetButton = new Button { Text = "Show content sheet", AutomationId = "ShowContentSheetButton", FontSize = 12 };
        showSheetButton.Clicked += async (_, _) => await ShowContentSheetAsync();

        var showDetentSheetButton = new Button { Text = "Show detent sheet", AutomationId = "ShowDetentSheetButton", FontSize = 12 };
        showDetentSheetButton.Clicked += async (_, _) => await ShowDetentSheetAsync();

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitSheetTests", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 16,
            Children =
            {
                new Label { Text = "Sheet Home", AutomationId = "SheetHomePage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                showSheetButton,
                showDetentSheetButton,
                _sheetState,
                _detentState,
                _popupState,
                exitButton
            }
        };
    }

    private async Task ShowContentSheetAsync()
    {
        var closeButton = new Button { Text = "Close sheet", AutomationId = "CloseSheetButton", FontSize = 12 };
        closeButton.Clicked += async (_, _) => await (_sheet?.CloseAsync() ?? Task.CompletedTask);

        var popupButton = new Button { Text = "Popup over sheet", AutomationId = "OpenPopupOverSheetButton", FontSize = 12 };
        popupButton.Clicked += async (_, _) => await ShowPopupOverSheetAsync();

        var navigateButton = new Button { Text = "Navigate", AutomationId = "NavigateFromSheetButton", FontSize = 12 };
        navigateButton.Clicked += async (_, _) =>
        {
            if (this.GetScaffoldOrDefault() is { } scaffold
                && scaffold.Areas.OfType<ScaffoldTabBar>().First() is { } tabBar)
            {
                await tabBar.SelectRootAsync(tabBar.Roots[1]);
            }
        };

        var content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(16, 0, 16, 16),
            Children =
            {
                new Label { Text = "Content sheet", AutomationId = "ContentSheetLabel", FontSize = 16, FontAttributes = FontAttributes.Bold },
                closeButton,
                popupButton,
                navigateButton
            }
        };

        var scaffold = this.GetScaffold();
        _sheet = await scaffold.ShowBottomSheetAsync(content);
        _sheetState.Text = _sheet.IsOpen ? "sheet:open" : "sheet:failed";
        ObserveClose(_sheet, _sheetState, "sheet");
    }

    private async Task ShowDetentSheetAsync()
    {
        var expandButton = new Button { Text = "Expand", AutomationId = "ExpandSheetButton", FontSize = 12 };
        var collapseButton = new Button { Text = "Collapse", AutomationId = "CollapseSheetButton", FontSize = 12 };

        var content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(16, 0, 16, 16),
            Children =
            {
                new Label { Text = "Detent sheet", AutomationId = "DetentSheetLabel", FontSize = 16, FontAttributes = FontAttributes.Bold },
                expandButton,
                collapseButton
            }
        };

        for (var i = 0; i < 20; i++)
        {
            content.Add(new Label { Text = $"Sheet filler {i}", FontSize = 11 });
        }

        // The sheet chrome is the content's parent chain — the public surface for programmatic
        // detent changes.
        expandButton.Clicked += async (_, _) => await SnapAsync(content, 1);
        collapseButton.Clicked += async (_, _) => await SnapAsync(content, 0);

        var scaffold = this.GetScaffold();

        _detentSheet = await scaffold.ShowBottomSheetAsync(
            content,
            new ScaffoldBottomSheetOptions
            {
                Detents = [ScaffoldSheetDetent.Height(220), ScaffoldSheetDetent.Fraction(0.85)],
                InitialDetent = 0
            }
        );

        _detentState.Text = _detentSheet.IsOpen ? "detent:open" : "detent:failed";
        ObserveClose(_detentSheet, _detentState, "detent");
    }

    private static Task SnapAsync(View sheetContent, int detentIndex)
        => sheetContent.Parent?.Parent is ScaffoldBottomSheetView sheetView
            ? sheetView.SnapToDetentAsync(detentIndex)
            : Task.CompletedTask;

    private async Task ShowPopupOverSheetAsync()
    {
        var closeButton = new Button { Text = "Close popup", AutomationId = "ClosePopupOverSheetButton", FontSize = 12 };
        closeButton.Clicked += async (_, _) => await (_popup?.CloseAsync() ?? Task.CompletedTask);

        var content = new Border
        {
            AutomationId = "PopupOverSheetContent",
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Background = new SolidColorBrush(Colors.White),
            Padding = 16,
            WidthRequest = 220,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = "Popup over sheet", FontSize = 14 },
                    closeButton
                }
            }
        };

        var scaffold = this.GetScaffold();
        _popup = await scaffold.ShowPopupAsync(content);
        _popupState.Text = _popup.IsOpen ? "popup:open" : "popup:failed";
        ObserveClose(_popup, _popupState, "popup");
    }

    private void ObserveClose(IScaffoldPopup popup, Label stateLabel, string name)
        => popup.Closed.ContinueWith(
            _ => Dispatcher.Dispatch(() => stateLabel.Text = $"{name}:closed"),
            TaskScheduler.Default
        );
}

[UsedImplicitly]
public class SheetOtherPage : ContentPage
{
    public SheetOtherPage(SheetOtherPageModel model)
    {
        BindingContext = model;
        Title = "SheetOther";

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitSheetOther", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 16,
            Children =
            {
                new Label { Text = "Sheet Other", AutomationId = "SheetOtherPage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                exitButton
            }
        };
    }
}

/// <summary>Scaffold harness of the bottom sheet system: two roots so navigation-dismissal is testable.</summary>
[UsedImplicitly]
[TestPage("Scaffold Sheet Tests")]
public class SheetScaffold : Scaffold
{
    public SheetScaffold()
    {
        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "SheetHome", PageType = typeof(SheetHomePage) },
                    new ScaffoldRoot { Title = "SheetOther", PageType = typeof(SheetOtherPage) }
                }
            }
        );
    }
}
