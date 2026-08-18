using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Edge-to-edge ScrollBox page hosted by a Scaffold with a SEMI-TRANSPARENT nav bar and the
/// default tab bar: the page declares <c>SafeAreaEdges=None</c> and the ScrollBox alone must
/// apply every inset exactly once — content rests below the nav bar and above the tab bar,
/// scrolls UNDER both (visible through the translucent chrome), programmatic scrolls clamp
/// against the insets, and the IME inset lets the bottom entry scroll above the keyboard.
/// </summary>
[UsedImplicitly]
public class ScrollBoxSafeAreaPage : ContentPage
{
    public ScrollBoxSafeAreaPage()
    {
        Title = "ScrollBox SafeArea";
        HideSoftInputOnTapped = true;

        // The ScrollBox owns the safe area: the page must not consume any inset.
        SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None);

        Scaffold.SetNavBarAppearance(
            this,
            new ScaffoldNavBarAppearance
            {
                Background = new SolidColorBrush(Color.FromArgb("#66FF8800"))
            }
        );

        var resultLabel = new Label { AutomationId = "SafeAreaResultLabel", FontSize = 12, Text = "-" };

        var stack = new VerticalStackLayout();

        for (var i = 1; i <= 30; i++)
        {
            stack.Add(new Label
                {
                    Text = $"SafeItem{i}",
                    AutomationId = $"SafeItem{i}",
                    FontSize = 16,
                    HeightRequest = 44,
                    Margin = new Thickness(16, 0),
                    BackgroundColor = i % 2 == 0 ? Colors.LightYellow : Colors.LightCyan
                }
            );
        }

        stack.Add(new Entry
            {
                AutomationId = "SafeAreaEntry",
                Placeholder = "type here",
                FontSize = 14,
                Margin = new Thickness(16, 0)
            }
        );

        var scrollBox = new ScrollBox
        {
            AutomationId = "SafeAreaScrollBox",
            Content = stack
        };

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitScrollBoxSafeArea", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();

        var startButton = new Button { Text = "Start", AutomationId = "SafeStartButton", FontSize = 11 };

        startButton.Clicked += async (_, _) =>
        {
            resultLabel.Text = "…";
            await scrollBox.ScrollToAsync(0, 0, animated: false);
            resultLabel.Text = $"done Y:{scrollBox.ScrollY:0}";
        };

        var endButton = new Button { Text = "Entry end", AutomationId = "SafeEndButton", FontSize = 11 };

        endButton.Clicked += async (_, _) =>
        {
            resultLabel.Text = "…";
            var entry = stack.Children[^1];
            await scrollBox.ScrollToAsync(entry, ScrollToPosition.End, animated: false);
            resultLabel.Text = $"done Y:{scrollBox.ScrollY:0}";
        };

        // Floating controls so the ScrollBox itself stays edge-to-edge behind the chrome.
        var controls = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(16, 0),
            VerticalOptions = LayoutOptions.Center,
            Children = { startButton, endButton, exitButton, resultLabel }
        };

        // The page-level SafeAreaEdges=None is not enough: on .NET 10 every LAYOUT applies the
        // safe area (including the Scaffold's bar footprints) as its own padding. The grid must
        // opt out too, so the ScrollBox runs edge-to-edge and owns every inset itself.
        var grid = new Grid
        {
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None)
        };
        grid.Add(scrollBox);
        grid.Add(new ViewBox
            {
                Content = controls,
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.Start,
                BackgroundColor = Color.FromArgb("#AAFFFFFF")
            }
        );

        Content = grid;
    }
}

/// <summary>
/// Scaffold harness for ScrollBox safe-area behavior: translucent nav bar, default tab bar
/// (two roots so the bar is meaningful), edge-to-edge ScrollBox page.
/// </summary>
[UsedImplicitly]
[TestPage("Scroll Box SafeArea Tests")]
public class ScrollBoxSafeAreaScaffold : Scaffold
{
    public ScrollBoxSafeAreaScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "SafeArea", PageType = typeof(ScrollBoxSafeAreaPage) },
                    new ScaffoldRoot { Title = "Other", PageType = typeof(ScrollBoxSafeAreaPage) }
                }
            }
        );
    }
}
