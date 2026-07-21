using System.Collections.ObjectModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
[TestPage("Virtual Scroll Events Tests")]
public class VirtualScrollEventsTests : ContentPage
{
    private readonly Label _startedLabel;
    private readonly Label _scrolledLabel;
    private readonly Label _endedLabel;
    private readonly Label _eventScrolledLabel;
    private readonly Label _lastScrollLabel;
    private int _startedCount;
    private int _scrolledCount;
    private int _endedCount;
    private int _eventScrolledCount;

    public VirtualScrollEventsTests()
    {
        var items = new ObservableCollection<VirtualScrollListItem>(
            Enumerable.Range(1, 50).Select(i => new VirtualScrollListItem($"E{i}"))
        );

        _startedLabel = new Label { AutomationId = "ScrollStartedCountLabel", FontSize = 14 };
        _scrolledLabel = new Label { AutomationId = "ScrolledCountLabel", FontSize = 14 };
        _endedLabel = new Label { AutomationId = "ScrollEndedCountLabel", FontSize = 14 };
        _eventScrolledLabel = new Label { AutomationId = "EventScrolledCountLabel", FontSize = 14 };
        _lastScrollLabel = new Label { AutomationId = "LastScrollLabel", FontSize = 14, Text = "-" };

        var virtualScroll = new VirtualScroll
                            {
                                AutomationId = "EventsScroll",
                                ItemsSource = items,

                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var label = new Label { FontSize = 16, Margin = new Thickness(16, 10) };
                                        label.SetBinding(Label.TextProperty, nameof(VirtualScrollListItem.Name));
                                        label.SetBinding(AutomationIdProperty, nameof(VirtualScrollListItem.Name));

                                        return label;
                                    }
                                )
                            };

        virtualScroll.ScrollStartedCommand = new Command<VirtualScrollScrolledEventArgs>(_ =>
            {
                _startedCount++;
                UpdateLabels();
            }
        );

        virtualScroll.ScrolledCommand = new Command<VirtualScrollScrolledEventArgs>(args =>
            {
                _scrolledCount++;
                _lastScrollLabel.Text = $"Y:{args.ScrollY:0} T:{args.TotalScrollableHeight:0} V:{args.ViewportHeight:0} P:{args.ScrollPercentageY:0.00}";
                UpdateLabels();
            }
        );

        virtualScroll.ScrollEndedCommand = new Command<VirtualScrollScrolledEventArgs>(_ =>
            {
                _endedCount++;
                UpdateLabels();
            }
        );

        virtualScroll.OnScrolled += (_, _) =>
        {
            _eventScrolledCount++;
            UpdateLabels();
        };

        UpdateLabels();

        var resetButton = new Button { Text = "Reset counters", AutomationId = "ResetCountersButton", FontSize = 12 };
        resetButton.Clicked += (_, _) =>
        {
            _startedCount = 0;
            _scrolledCount = 0;
            _endedCount = 0;
            _eventScrolledCount = 0;
            _lastScrollLabel.Text = "-";
            UpdateLabels();
        };

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 resetButton,
                                 _startedLabel,
                                 _scrolledLabel,
                                 _endedLabel,
                                 _eventScrolledLabel,
                                 _lastScrollLabel
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(controlsLayout);
        grid.Add(virtualScroll, 0, 1);

        Content = grid;
    }

    private void UpdateLabels()
    {
        _startedLabel.Text = $"Started: {_startedCount}";
        _scrolledLabel.Text = $"Scrolled: {_scrolledCount}";
        _endedLabel.Text = $"Ended: {_endedCount}";
        _eventScrolledLabel.Text = $"EventScrolled: {_eventScrolledCount}";
    }
}
