using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Scroll Box Horizontal Tests")]
public class ScrollBoxHorizontalTests : ContentPage
{
    private readonly Label _resultLabel;
    private readonly ScrollBox _scrollBox;

    public ScrollBoxHorizontalTests()
    {
        _resultLabel = new Label { AutomationId = "HResultLabel", FontSize = 12, Text = "-" };
        var orientationLabel = new Label { AutomationId = "HOrientationLabel", FontSize = 12, Text = nameof(ScrollBoxOrientation.Horizontal) };

        _scrollBox = new ScrollBox
        {
            AutomationId = "HScrollBox",
            Orientation = ScrollBoxOrientation.Horizontal,
            FadingEdgeLength = 24,
            Content = BuildContent(ScrollBoxOrientation.Horizontal)
        };

        var toggleButton = new Button { Text = "Toggle orient", AutomationId = "HToggleOrientationButton", FontSize = 11 };

        toggleButton.Clicked += (_, _) =>
        {
            // Real-world orientation change: swap the content layout together with the axis.
            var newOrientation = _scrollBox.Orientation == ScrollBoxOrientation.Horizontal
                ? ScrollBoxOrientation.Vertical
                : ScrollBoxOrientation.Horizontal;

            _scrollBox.Content = BuildContent(newOrientation);
            _scrollBox.Orientation = newOrientation;
            orientationLabel.Text = newOrientation.ToString();
        };

        var controls = new HorizontalWrapLayout
                       {
                           CreateActionButton("HJumpTo300Button", "Jump X300", () => _scrollBox.ScrollToAsync(300, 0, animated: false)),
                           CreateActionButton("HJumpTo200YButton", "Jump Y200", () => _scrollBox.ScrollToAsync(0, 200, animated: false)),
                           CreateActionButton("HItem30CenterButton", "Item30 center", () => ScrollToItemAsync(30, ScrollToPosition.Center)),
                           CreateActionButton("HBackToStartButton", "Start", () => _scrollBox.ScrollToAsync(0, 0, animated: false)),
                           toggleButton,
                           orientationLabel,
                           _resultLabel
                       };
        controls.HorizontalSpacing = 8;
        controls.VerticalSpacing = 4;
        controls.Padding = new Thickness(16, 8);

        // Fixed-height controls row: label changes must never move the viewport (see ScrollBoxTests).
        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(170), new RowDefinition(GridLength.Star)]
                   };
        grid.Add(controls);
        grid.Add(_scrollBox, 0, 1);

        Content = grid;
    }

    private static Layout BuildContent(ScrollBoxOrientation orientation)
    {
        // 40 items x 60 units along the scrolling axis.
        Layout stack = orientation == ScrollBoxOrientation.Horizontal
            ? new HorizontalStackLayout()
            : new VerticalStackLayout();

        for (var i = 1; i <= 40; i++)
        {
            stack.Add(new Label
                {
                    Text = $"H{i}",
                    AutomationId = $"HItem{i}",
                    FontSize = 14,
                    WidthRequest = orientation == ScrollBoxOrientation.Horizontal ? 60 : 200,
                    HeightRequest = orientation == ScrollBoxOrientation.Horizontal ? 200 : 60,
                    BackgroundColor = i % 2 == 0 ? Colors.LightYellow : Colors.LightCyan
                }
            );
        }

        return stack;
    }

    private Button CreateActionButton(string automationId, string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11, Padding = new Thickness(6, 2) };

        button.Clicked += async (_, _) =>
        {
            _resultLabel.Text = "…";
            await action();
            _resultLabel.Text = $"done X:{_scrollBox.ScrollX:0} Y:{_scrollBox.ScrollY:0}";
        };

        return button;
    }

    private Task ScrollToItemAsync(int index, ScrollToPosition position)
    {
        var stack = (Layout) _scrollBox.Content!;
        var item = stack.Children[index - 1];

        return _scrollBox.ScrollToAsync(item, position, animated: false);
    }
}
