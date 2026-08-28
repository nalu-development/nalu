using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Scroll Box Tests")]
public class ScrollBoxTests : ContentPage
{
    private readonly Label _startedLabel;
    private readonly Label _scrolledLabel;
    private readonly Label _endedLabel;
    private readonly Label _positionLabel;
    private readonly Label _resultLabel;
    private readonly ScrollBox _scrollBox;
    private int _startedCount;
    private int _scrolledCount;
    private int _endedCount;

    public ScrollBoxTests()
    {
        _startedLabel = new Label { AutomationId = "ScrollStartedCountLabel", FontSize = 12 };
        _scrolledLabel = new Label { AutomationId = "ScrolledCountLabel", FontSize = 12 };
        _endedLabel = new Label { AutomationId = "ScrollEndedCountLabel", FontSize = 12 };
        _positionLabel = new Label { AutomationId = "ScrollPositionLabel", FontSize = 12, Text = "-" };
        _resultLabel = new Label { AutomationId = "ScrollResultLabel", FontSize = 12, Text = "-" };

        var stack = new VerticalStackLayout();

        for (var i = 1; i <= 40; i++)
        {
            stack.Add(new Label
                {
                    Text = $"Item{i}",
                    AutomationId = $"Item{i}",
                    FontSize = 16,
                    HeightRequest = 44,
                    Margin = new Thickness(16, 0)
                }
            );
        }

        _scrollBox = new ScrollBox
        {
            AutomationId = "TestScrollBox",
            Content = stack
        };

        _scrollBox.ScrollStartedCommand = new Command<ScrollBoxScrolledEventArgs>(_ =>
            {
                _startedCount++;
                UpdateLabels();
            }
        );

        _scrollBox.ScrolledCommand = new Command<ScrollBoxScrolledEventArgs>(args =>
            {
                _scrolledCount++;
                _positionLabel.Text = $"Y:{args.ScrollY:0} X:{args.ScrollX:0} T:{args.TotalScrollableHeight:0} P:{args.ScrollPercentageY:0.00}";
                UpdateLabels();
            }
        );

        _scrollBox.ScrollEndedCommand = new Command<ScrollBoxScrolledEventArgs>(_ =>
            {
                _endedCount++;
                UpdateLabels();
            }
        );

        UpdateLabels();

        var controls = new HorizontalWrapLayout
                       {
                           CreateActionButton("JumpTo400Button", "Jump 400", () => _scrollBox.ScrollToAsync(0, 400, animated: false)),
                           CreateActionButton("AnimateTo600Button", "Anim 600", () => _scrollBox.ScrollToAsync(0, 600)),
                           CreateActionButton("Item30CenterButton", "Item30 center", () => ScrollToItemAsync(30, ScrollToPosition.Center)),
                           CreateActionButton("Item5StartButton", "Item5 start", () => ScrollToItemAsync(5, ScrollToPosition.Start)),
                           CreateActionButton("Item40EndButton", "Item40 end", () => ScrollToItemAsync(40, ScrollToPosition.End)),
                           CreateActionButton("BackToStartButton", "Start", () => _scrollBox.ScrollToAsync(0, 0, animated: false)),
                           _startedLabel,
                           _scrolledLabel,
                           _endedLabel,
                           _positionLabel,
                           _resultLabel
                       };
        controls.HorizontalSpacing = 8;
        controls.VerticalSpacing = 4;
        controls.Padding = new Thickness(16, 8);

        // The controls row is FIXED height: label text changes must never reflow the header and
        // move the ScrollBox viewport between an interaction and a bounds assertion.
        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(170), new RowDefinition(GridLength.Star)]
                   };
        grid.Add(controls);
        grid.Add(_scrollBox, 0, 1);

        Content = grid;
    }

    private Button CreateActionButton(string automationId, string text, Func<Task> action)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11, Padding = new Thickness(6, 2) };

        button.Clicked += async (_, _) =>
        {
            _resultLabel.Text = "…";
            await action();
            _resultLabel.Text = $"done Y:{_scrollBox.ScrollY:0} X:{_scrollBox.ScrollX:0}";
        };

        return button;
    }

    private Task ScrollToItemAsync(int index, ScrollToPosition position)
    {
        var stack = (VerticalStackLayout) _scrollBox.Content!;
        var item = stack.Children[index - 1];

        return _scrollBox.ScrollToAsync(item, position, animated: false);
    }

    private void UpdateLabels()
    {
        _startedLabel.Text = $"Started: {_startedCount}";
        _scrolledLabel.Text = $"Scrolled: {_scrolledCount}";
        _endedLabel.Text = $"Ended: {_endedCount}";
    }
}
