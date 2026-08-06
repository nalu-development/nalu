using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Harness for <see cref="VirtualScroll.SizingStrategy" />: a vertical list inside an AUTO row —
/// so the row height is whatever the VirtualScroll asks for — with buttons to switch strategy and
/// to grow/shrink the collection. A fixed 40dp item height makes the expected extent arithmetic
/// exact for the tests (n items ⇒ n*40 content).
/// </summary>
[UsedImplicitly]
[TestPage("Virtual Scroll SizingStrategy Tests")]
public class VirtualScrollSizingStrategyTests : ContentPage
{
    private const double _itemExtent = 40;

    private readonly ObservableCollection<string> _items = [];
    private readonly VirtualScroll _virtualScroll;
    private readonly Label _stateLabel;

    public VirtualScrollSizingStrategyTests()
    {
        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "SizingScroll",
                             BackgroundColor = Colors.LightSteelBlue,
                             ItemsLayout = new VerticalVirtualScrollLayout { EstimatedItemSize = _itemExtent },
                             ItemsSource = _items,
                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     HeightRequest = _itemExtent,
                                                     FontSize = 13,
                                                     VerticalTextAlignment = TextAlignment.Center
                                                 };

                                     label.SetBinding(Label.TextProperty, ".");

                                     return label;
                                 }
                             )
                         };

        _stateLabel = new Label { AutomationId = "SizingStateLabel", FontSize = 13 };

        SetItemCount(2);

        Button MakeButton(string text, string automationId, Action onClicked)
        {
            var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
            button.Clicked += (_, _) => onClicked();

            return button;
        }

        var controls = new HorizontalWrapLayout
                       {
                           HorizontalSpacing = 8,
                           VerticalSpacing = 8,
                           Padding = new Thickness(16, 8),
                           Children =
                           {
                               MakeButton("Fill", "SizingFillButton", () => SetStrategy(VirtualScrollSizingStrategy.Fill)),
                               MakeButton("Max 300", "SizingMaxButton", () => SetStrategy(VirtualScrollSizingStrategy.Max(300))),
                               MakeButton("Unbounded", "SizingUnboundedButton", () => SetStrategy(VirtualScrollSizingStrategy.Unbounded)),
                               MakeButton("2 items", "SizingFewItemsButton", () => SetItemCount(2)),
                               MakeButton("5 items", "SizingSomeItemsButton", () => SetItemCount(5)),
                               MakeButton("50 items", "SizingManyItemsButton", () => SetItemCount(50)),
                               MakeButton("+1 item", "SizingAddItemButton", () => _items.Add($"Item {_items.Count}")),
                               _stateLabel
                           }
                       };

        // AUTO row: the VirtualScroll's own desired size decides the row height, so its bounds
        // are the observable proof of the strategy. The filler below takes the remaining space.
        var grid = new Grid
                   {
                       RowDefinitions =
                       [
                           new RowDefinition(GridLength.Auto),
                           new RowDefinition(GridLength.Auto),
                           new RowDefinition(GridLength.Star)
                       ],
                       Children = { controls }
                   };

        grid.Add(_virtualScroll, 0, 1);

        grid.Add(
            new Label
            {
                AutomationId = "SizingFillerLabel",
                Text = "below the list",
                BackgroundColor = Colors.Khaki,
                FontSize = 13
            },
            0,
            2
        );

        Content = grid;
    }

    // SizingStrategy is marked experimental on Windows (it is not implemented there); this harness
    // drives iOS and Android, so the diagnostic is acknowledged rather than avoided.
#pragma warning disable NALUVS001
    private void SetStrategy(VirtualScrollSizingStrategy strategy)
    {
        _virtualScroll.SizingStrategy = strategy;
        UpdateStateLabel();
    }

    private void SetItemCount(int count)
    {
        _items.Clear();

        for (var i = 0; i < count; i++)
        {
            _items.Add($"Item {i}");
        }

        UpdateStateLabel();
    }

    private void UpdateStateLabel() => _stateLabel.Text = $"{_virtualScroll.SizingStrategy}/{_items.Count}";
#pragma warning restore NALUVS001
}
