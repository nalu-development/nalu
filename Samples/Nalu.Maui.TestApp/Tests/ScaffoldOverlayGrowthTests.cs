using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public class OverlayGrowthHomePageModel : ObservableObject;

/// <summary>
/// Overlay content whose size changes AFTER presentation (a deferred image, an expanding
/// section, a loaded list): the popup must be re-placed at its new natural size and a
/// <c>Content</c>-detent sheet must re-resolve its height — without any call from the app.
/// Growth is deterministic (a button raises a spacer's HeightRequest) and also asynchronous
/// (a timer, like an image that finishes loading).
/// </summary>
[UsedImplicitly]
public class OverlayGrowthHomePage : ContentPage
{
    private IScaffoldPopup? _popup;
    private IScaffoldPopup? _sheet;

    public OverlayGrowthHomePage(OverlayGrowthHomePageModel model)
    {
        BindingContext = model;
        Title = "OverlayGrowth";

        var showPopup = new Button { Text = "Show growing popup", AutomationId = "ShowGrowingPopupButton", FontSize = 12 };
        showPopup.Clicked += async (_, _) => await ShowPopupAsync();

        var showSheet = new Button { Text = "Show growing sheet", AutomationId = "ShowGrowingSheetButton", FontSize = 12 };
        showSheet.Clicked += async (_, _) => await ShowSheetAsync();

        var exit = new Button { Text = "Exit", AutomationId = "ExitOverlayGrowthTests", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exit.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        Content = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 16,
            Children =
            {
                new Label { Text = "Overlay growth", AutomationId = "OverlayGrowthHomePage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                showPopup,
                showSheet,
                exit
            }
        };
    }

    /// <summary>Content with a spacer that grows on demand (button) and once by itself after 1.5 s (deferred load).</summary>
    private static View BuildGrowingContent(string marker, Func<Task> closeAsync)
    {
        var spacer = new BoxView { AutomationId = $"{marker}Spacer", HeightRequest = 40, Color = Colors.LightSkyBlue };
        var state = new Label { AutomationId = $"{marker}State", Text = "size:40", FontSize = 12 };

        var grow = new Button { Text = "Grow", AutomationId = $"{marker}GrowButton", FontSize = 12 };
        grow.Clicked += (_, _) =>
        {
            spacer.HeightRequest += 120;
            state.Text = $"size:{spacer.HeightRequest:0}";
        };

        var shrink = new Button { Text = "Shrink", AutomationId = $"{marker}ShrinkButton", FontSize = 12 };
        shrink.Clicked += (_, _) =>
        {
            spacer.HeightRequest = 40;
            state.Text = "size:40";
        };

        var close = new Button { Text = "Close", AutomationId = $"{marker}CloseButton", FontSize = 12 };
        close.Clicked += async (_, _) => await closeAsync();

        var content = new VerticalStackLayout
        {
            AutomationId = $"{marker}Content",
            Spacing = 8,
            Padding = 16,
            BackgroundColor = Colors.White,
            Children =
            {
                new Label { Text = marker, FontSize = 16, FontAttributes = FontAttributes.Bold },
                // Nested one level down: the invalidation must bubble up to the overlay root.
                new Grid { Children = { spacer } },
                state,
                grow,
                shrink,
                close
            }
        };

        // Deferred growth without user input (an image that finishes decoding, a section that
        // loads): the overlay must follow on its own.
        content.Loaded += async (_, _) =>
        {
            await Task.Delay(1500);

            if (content.IsLoaded && spacer.HeightRequest < 100)
            {
                spacer.HeightRequest = 100;
                state.Text = "size:100";
            }
        };

        return content;
    }

    private async Task ShowPopupAsync()
    {
        var content = BuildGrowingContent("GrowingPopup", () => _popup?.CloseAsync() ?? Task.CompletedTask);
        _popup = await this.GetScaffold().ShowPopupAsync(content);
        await _popup.Closed;
        _popup = null;
    }

    private async Task ShowSheetAsync()
    {
        var content = BuildGrowingContent("GrowingSheet", () => _sheet?.CloseAsync() ?? Task.CompletedTask);
        _sheet = await this.GetScaffold().ShowBottomSheetAsync(content, new ScaffoldBottomSheetOptions { Detents = [ScaffoldSheetDetent.Content] });
        await _sheet.Closed;
        _sheet = null;
    }
}

[UsedImplicitly]
[TestPage("Scaffold Overlay Growth Tests")]
public class OverlayGrowthScaffold : Scaffold
{
    public OverlayGrowthScaffold()
    {
        Areas.Add(new ScaffoldRoot { Title = "Home", PageType = typeof(OverlayGrowthHomePage) });
    }
}
