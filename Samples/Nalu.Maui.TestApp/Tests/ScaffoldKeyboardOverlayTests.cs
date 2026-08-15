using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;
using Microsoft.Maui.Controls.Shapes;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public class KeyboardOverlayHomePageModel : ObservableObject;

/// <summary>
/// Keyboard-aware overlay harness page: a bottom sheet and popups (centered, anchored below a
/// button near the bottom edge) hosting an <see cref="Entry"/>, plus keyboard probes. Focusing
/// the entry raises the soft keyboard; the scaffold must keep the overlay's content in the area
/// ABOVE it (sheet: surface anchored to the bottom edge, content padded above the keyboard;
/// popup: re-centered / pushed above its anchor) and put it back when the keyboard hides. Every overlay carries a "hide keyboard" button (programmatic
/// unfocus) so tests never need a real tap outside the overlay (which would dismiss it).
/// </summary>
[UsedImplicitly]
public class KeyboardOverlayHomePage : ContentPage
{
    private readonly Label _state;
    private IScaffoldPopup? _overlay;

    public KeyboardOverlayHomePage(KeyboardOverlayHomePageModel model)
    {
        BindingContext = model;
        Title = "KeyboardOverlays";

        _state = new Label { AutomationId = "KeyboardOverlayState", Text = "overlay:idle", FontSize = 12 };

        var showSheetButton = new Button { Text = "Show entry sheet", AutomationId = "ShowKeyboardSheetButton", FontSize = 12 };
        showSheetButton.Clicked += async (_, _) => await ShowEntrySheetAsync();

        var showTallSheetButton = new Button { Text = "Show tall entry sheet", AutomationId = "ShowKeyboardTallSheetButton", FontSize = 12 };
        showTallSheetButton.Clicked += async (_, _) => await ShowTallEntrySheetAsync();

        var showPopupButton = new Button { Text = "Show entry popup", AutomationId = "ShowKeyboardPopupButton", FontSize = 12 };
        showPopupButton.Clicked += async (_, _) => await ShowEntryPopupAsync(anchor: null);

        var showPanSheetButton = new Button { Text = "Show pan sheet", AutomationId = "ShowKeyboardPanSheetButton", FontSize = 12 };
        showPanSheetButton.Clicked += async (_, _) => await ShowPanSheetAsync();

        var showPanPopupButton = new Button { Text = "Show pan popup", AutomationId = "ShowKeyboardPanPopupButton", FontSize = 12 };
        showPanPopupButton.Clicked += async (_, _) => await ShowPanPopupAsync();

        var anchorButton = new Button { Text = "Show anchored entry popup", AutomationId = "ShowKeyboardAnchoredPopupButton", FontSize = 12 };
        anchorButton.Clicked += async (_, _) => await ShowEntryPopupAsync(anchorButton);

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitKeyboardOverlayTests", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();

        var top = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = 16,
            Children =
            {
                new Label { Text = "Keyboard overlays", AutomationId = "KeyboardOverlayHomePage", FontSize = 22, FontAttributes = FontAttributes.Bold },
                showSheetButton,
                showTallSheetButton,
                showPopupButton,
                showPanSheetButton,
                showPanPopupButton,
                SoftKeyboardProbe.CreateLabel("KeyboardOverlayKeyboardProbe"),
                SoftKeyboardProbe.CreateHeightLabel("KeyboardOverlayKeyboardHeight"),
                _state,
                exitButton
            }
        };

        // PAGE-level keyboard policy (Scaffold.KeyboardMode on the page, live): an entry at the very
        // bottom of the page — Resize pads the page above the keyboard, Pan slides the page, None
        // leaves the entry under the keyboard — switched by the buttons.
        var pageEntry = new Entry { AutomationId = "KeyboardPageEntry", Placeholder = "page entry", FontSize = 14 };
        var pageModeLabel = new Label { AutomationId = "KeyboardPageMode", Text = "page:Resize", FontSize = 11 };
        var pageModes = new HorizontalStackLayout { Spacing = 6 };

        foreach (var mode in new[] { ScaffoldKeyboardMode.Resize, ScaffoldKeyboardMode.Pan, ScaffoldKeyboardMode.None })
        {
            var button = new Button { Text = mode.ToString(), AutomationId = $"KeyboardPageMode{mode}Button", FontSize = 11, Padding = new Thickness(8, 4) };
            button.Clicked += (_, _) =>
            {
                Scaffold.SetKeyboardMode(this, mode);
                pageModeLabel.Text = $"page:{mode}";
            };
            pageModes.Add(button);
        }

        var hidePageKeyboardButton = new Button { Text = "Hide", AutomationId = "KeyboardPageHideButton", FontSize = 11, Padding = new Thickness(8, 4) };
        hidePageKeyboardButton.Clicked += async (_, _) =>
        {
            pageEntry.Unfocus();
            await pageEntry.HideSoftInputAsync(CancellationToken.None);
        };
        pageModes.Add(hidePageKeyboardButton);
        pageModes.Add(pageModeLabel);

        // The anchor sits at the BOTTOM of the page: with the keyboard up, "below the anchor" no
        // longer fits and the popup must flip above.
        var bottom = new VerticalStackLayout { Padding = new Thickness(16, 0, 16, 16), Spacing = 6, Children = { pageModes, anchorButton, pageEntry } };

        var grid = new Grid
        {
            RowDefinitions = [new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto)]
        };

        // Scrollable: under the page-level Resize policy the star row shrinks by the keyboard.
        grid.Add(new ScrollView { AutomationId = "KeyboardPageScroll", Content = top });
        grid.Add(bottom, 0, 1);

        Content = grid;
    }

    private View MakeOverlayBody(string marker, params View[] extra)
    {
        var entry = new Entry { AutomationId = $"Keyboard{marker}Entry", Placeholder = "type here", FontSize = 14 };

        var hideKeyboardButton = new Button { Text = "Hide keyboard", AutomationId = $"Keyboard{marker}HideButton", FontSize = 12 };
        // Hides the keyboard for WHICHEVER entry of the overlay is focused (the tall sheet has a
        // second one at the bottom). Unfocus alone leaves the Android IME up when the click comes
        // from the agent (no real focus transfer): hide it explicitly through MAUI's soft-input
        // API as well.
        hideKeyboardButton.Clicked += async (_, _) =>
        {
            foreach (var input in extra.Prepend(entry).OfType<Entry>().ToArray())
            {
                input.Unfocus();
                await input.HideSoftInputAsync(CancellationToken.None);
            }
        };

        var closeButton = new Button { Text = "Close", AutomationId = $"Keyboard{marker}CloseButton", FontSize = 12 };
        closeButton.Clicked += async (_, _) => await (_overlay?.CloseAsync() ?? Task.CompletedTask);

        var body = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = $"{marker} with entry", AutomationId = $"Keyboard{marker}Label", FontSize = 16, FontAttributes = FontAttributes.Bold },
                entry,
                hideKeyboardButton,
                closeButton
            }
        };

        foreach (var view in extra)
        {
            body.Add(view);
        }

        return body;
    }

    private async Task ShowEntrySheetAsync()
    {
        var content = new VerticalStackLayout
        {
            Padding = new Thickness(16, 0, 16, 16),
            Children = { MakeOverlayBody("Sheet") }
        };

        _overlay = await this.GetScaffold().ShowBottomSheetAsync(content);
        _state.Text = _overlay.IsOpen ? "overlay:open" : "overlay:failed";
        ObserveClose(_overlay);
    }

    /// <summary>
    /// A sheet at an 85% detent: the keyboard eats most of its content area (the surface stays
    /// anchored, the keyboard becomes its bottom inset). The content is the shape a real form
    /// takes — a ScrollView with filler and a SECOND entry at the very bottom — so the reduced
    /// content area keeps that entry reachable by scrolling.
    /// </summary>
    private async Task ShowTallEntrySheetAsync()
    {
        var filler = new VerticalStackLayout { Spacing = 0 };

        for (var i = 0; i < 30; i++)
        {
            filler.Add(new Label { Text = $"Sheet filler {i}", FontSize = 11 });
        }

        var bottomEntry = new Entry { AutomationId = "KeyboardTallSheetBottomEntry", Placeholder = "bottom entry", FontSize = 14 };

        var content = new ScrollView
        {
            AutomationId = "KeyboardTallSheetScroll",
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 0, 16, 16),
                Children = { MakeOverlayBody("TallSheet", filler, bottomEntry) }
            }
        };

        _overlay = await this.GetScaffold().ShowBottomSheetAsync(
            content,
            new ScaffoldBottomSheetOptions
            {
                Detents = [ScaffoldSheetDetent.Fraction(0.85)],
                InitialDetent = 0
            }
        );

        _state.Text = _overlay.IsOpen ? "overlay:open" : "overlay:failed";
        ObserveClose(_overlay);
    }

    /// <summary>
    /// A Pan-mode sheet (<see cref="Scaffold.KeyboardModeProperty"/> on the content): the sheet
    /// keeps its size and slides up by the least that keeps the FOCUSED entry above the keyboard —
    /// a bottom entry pans it (almost) a keyboard's worth, the top one much less.
    /// </summary>
    private async Task ShowPanSheetAsync()
    {
        var filler = new VerticalStackLayout { Spacing = 0 };

        for (var i = 0; i < 6; i++)
        {
            filler.Add(new Label { Text = $"Sheet filler {i}", FontSize = 11 });
        }

        var bottomEntry = new Entry { AutomationId = "KeyboardPanSheetBottomEntry", Placeholder = "bottom entry", FontSize = 14 };

        var content = new VerticalStackLayout
        {
            Padding = new Thickness(16, 0, 16, 16),
            Children = { MakeOverlayBody("PanSheet", filler, bottomEntry) }
        };

        Scaffold.SetKeyboardMode(content, ScaffoldKeyboardMode.Pan);

        _overlay = await this.GetScaffold().ShowBottomSheetAsync(content);
        _state.Text = _overlay.IsOpen ? "overlay:open" : "overlay:failed";
        ObserveClose(_overlay);
    }

    /// <summary>A Pan-mode centered popup: same size, slides up just enough for the focused entry.</summary>
    private async Task ShowPanPopupAsync()
    {
        var bottomEntry = new Entry { AutomationId = "KeyboardPanPopupBottomEntry", Placeholder = "bottom entry", FontSize = 14 };

        var content = new Border
        {
            AutomationId = "KeyboardPanPopupContent",
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Background = new SolidColorBrush(Colors.White),
            Padding = 16,
            WidthRequest = 240,
            Content = MakeOverlayBody("PanPopup", new Label { Text = "Some more content", FontSize = 12, HeightRequest = 120 }, bottomEntry)
        };

        Scaffold.SetKeyboardMode(content, ScaffoldKeyboardMode.Pan);

        _overlay = await this.GetScaffold().ShowPopupAsync(content);
        _state.Text = _overlay.IsOpen ? "overlay:open" : "overlay:failed";
        ObserveClose(_overlay);
    }

    private async Task ShowEntryPopupAsync(View? anchor)
    {
        var marker = anchor is null ? "Popup" : "AnchoredPopup";

        var content = new Border
        {
            AutomationId = $"Keyboard{marker}Content",
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(12) },
            Background = new SolidColorBrush(Colors.White),
            Padding = 16,
            WidthRequest = 240,
            Content = MakeOverlayBody(marker)
        };

        _overlay = await this.GetScaffold().ShowPopupAsync(
            content,
            anchor is null
                ? null
                : new ScaffoldPopupOptions { Placement = ScaffoldPopupPlacement.AnchorBelow, Anchor = anchor }
        );

        _state.Text = _overlay.IsOpen ? "overlay:open" : "overlay:failed";
        ObserveClose(_overlay);
    }

    private void ObserveClose(IScaffoldPopup popup)
        => popup.Closed.ContinueWith(
            _ => Dispatcher.Dispatch(() => _state.Text = "overlay:closed"),
            TaskScheduler.Default
        );
}

/// <summary>Scaffold harness of keyboard-aware overlays (bottom sheets and popups hosting text input).</summary>
[UsedImplicitly]
[TestPage("Scaffold Keyboard Overlay Tests")]
public class KeyboardOverlayScaffold : Scaffold
{
    public KeyboardOverlayScaffold()
    {
        Areas.Add(new ScaffoldRoot { Title = "KeyboardOverlays", PageType = typeof(KeyboardOverlayHomePage) });
    }
}
