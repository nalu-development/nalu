using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Scroll Box Sizing Tests")]
public class ScrollBoxSizingTests : ContentPage
{
    private readonly Label _heightLabel;
    private readonly Label _pendingResultLabel;
    private readonly ScrollBox _hugScrollBox;
    private readonly VerticalStackLayout _hugStack;

    public ScrollBoxSizingTests()
    {
        _heightLabel = new Label { AutomationId = "HugHeightLabel", FontSize = 12, Text = "-" };
        _pendingResultLabel = new Label { AutomationId = "PendingScrollResultLabel", FontSize = 12, Text = "-" };

        // Hugging box: with SizingStrategy=Max(200) the box must shrink AND grow with its content.
        _hugStack = new VerticalStackLayout();
        AddHugItems(2);

        _hugScrollBox = new ScrollBox
        {
            AutomationId = "HugScrollBox",
            SizingStrategy = ScrollBoxSizingStrategy.Max(200),
            BackgroundColor = Colors.LightBlue,
            Content = _hugStack
        };

        _hugScrollBox.SizeChanged += (_, _) => _heightLabel.Text = $"H:{_hugScrollBox.Height:0}";

        var addButton = new Button { Text = "Add 5", AutomationId = "AddHugItemsButton", FontSize = 11 };
        addButton.Clicked += (_, _) => AddHugItems(5);

        var removeButton = new Button { Text = "Remove all but 2", AutomationId = "RemoveHugItemsButton", FontSize = 11 };

        removeButton.Clicked += (_, _) =>
        {
            while (_hugStack.Children.Count > 2)
            {
                _hugStack.Children.RemoveAt(_hugStack.Children.Count - 1);
            }
        };

        // A second scroll box exercising the pre-layout ScrollTo queue: the request is issued
        // from the constructor, long before the first layout pass, and must still complete.
        var pendingStack = new VerticalStackLayout();

        for (var i = 1; i <= 30; i++)
        {
            pendingStack.Add(new Label
                {
                    Text = $"P{i}",
                    AutomationId = $"P{i}",
                    FontSize = 16,
                    HeightRequest = 44,
                    Margin = new Thickness(16, 0)
                }
            );
        }

        var pendingScrollBox = new ScrollBox
        {
            AutomationId = "PendingScrollBox",
            Content = pendingStack
        };

        _ = ReportPendingScrollAsync(pendingScrollBox);

        var controls = new HorizontalWrapLayout
                       {
                           addButton,
                           removeButton,
                           _heightLabel,
                           _pendingResultLabel
                       };
        controls.HorizontalSpacing = 8;
        controls.VerticalSpacing = 4;
        controls.Padding = new Thickness(16, 8);

        // The controls row is FIXED height so label changes never move the boxes below.
        var grid = new Grid
                   {
                       RowDefinitions =
                       [
                           new RowDefinition(100),
                           new RowDefinition(GridLength.Auto),
                           new RowDefinition(GridLength.Star)
                       ]
                   };
        grid.Add(controls);
        grid.Add(_hugScrollBox, 0, 1);
        grid.Add(pendingScrollBox, 0, 2);

        Content = grid;
    }

    private async Task ReportPendingScrollAsync(ScrollBox scrollBox)
    {
        // Issued pre-layout: MAUI ScrollView's ScrollToAsync would never complete here (#15387).
        await scrollBox.ScrollToAsync(0, 220, animated: false);
        _pendingResultLabel.Text = $"pending done Y:{scrollBox.ScrollY:0}";
    }

    private void AddHugItems(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _hugStack.Add(new Label
                {
                    Text = $"Hug{_hugStack.Children.Count + 1}",
                    FontSize = 16,
                    HeightRequest = 40,
                    Margin = new Thickness(16, 0)
                }
            );
        }
    }
}
