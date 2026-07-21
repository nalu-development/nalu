using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Virtual Scroll Carousel Tests")]
public class VirtualScrollCarouselTests : ContentPage
{
    private static readonly Color[] _pageColors =
    [
        Colors.LightCoral, Colors.LightSeaGreen, Colors.LightSkyBlue, Colors.Plum, Colors.Khaki
    ];

    private readonly ObservableCollection<VirtualScrollListItem> _items;
    private readonly VirtualScroll _virtualScroll;
    private readonly Label _rangeLabel;
    private int _currentIndex;

    public VirtualScrollCarouselTests()
    {
        _items = new ObservableCollection<VirtualScrollListItem>(
            Enumerable.Range(1, 5).Select(i => new VirtualScrollListItem($"Carousel{i}"))
        );

        _virtualScroll = new VirtualScroll
                         {
                             AutomationId = "CarouselScroll",
                             ItemsSource = _items,
                             ItemsLayout = new HorizontalCarouselVirtualScrollLayout(),

                             ItemTemplate = new DataTemplate(() =>
                                 {
                                     var label = new Label
                                                 {
                                                     FontSize = 32,
                                                     HorizontalOptions = LayoutOptions.Center,
                                                     VerticalOptions = LayoutOptions.Center
                                                 };
                                     label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                     label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                     var grid = new Grid();
                                     grid.SetBinding(BackgroundColorProperty, new Binding(nameof(VirtualScrollListItem.Name), converter: new NameToColorConverter()));
                                     grid.Add(label);

                                     return grid;
                                 }
                             )
                         };

        // Track the carousel's CurrentRange attached property (TwoWay: user swipes update it,
        // setting it scrolls the carousel).
        _rangeLabel = new Label { AutomationId = "CarouselRangeLabel", FontSize = 14, Text = "0" };
        _virtualScroll.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "CurrentRange")
            {
                var range = CarouselVirtualScrollLayout.GetCurrentRange(_virtualScroll);
                _currentIndex = range.StartItemIndex;
                _rangeLabel.Text = $"{range.StartItemIndex}";
            }
        };

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 MakeButton("Prev", "PrevPageButton", () => GoTo(_currentIndex - 1)),
                                 MakeButton("Next", "NextPageButton", () => GoTo(_currentIndex + 1)),
                                 MakeButton("Vertical", "SwitchToVerticalButton", () => _virtualScroll.ItemsLayout = new VerticalCarouselVirtualScrollLayout()),
                                 MakeButton("Horizontal", "SwitchToHorizontalButton", () => _virtualScroll.ItemsLayout = new HorizontalCarouselVirtualScrollLayout()),
                                 _rangeLabel
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(controlsLayout);
        grid.Add(_virtualScroll, 0, 1);

        Content = grid;
    }

    private sealed class NameToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        {
            var index = value is string name && int.TryParse(name["Carousel".Length..], out var i) ? i - 1 : 0;

            return _pageColors[index % _pageColors.Length];
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 12 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private void GoTo(int index)
    {
        index = Math.Clamp(index, 0, _items.Count - 1);
        CarouselVirtualScrollLayout.SetCurrentRange(_virtualScroll, new VirtualScrollRange(0, index, 0, index));
    }
}
