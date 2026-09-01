using System.Collections.ObjectModel;
using System.Diagnostics;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// The card cell inside a <see cref="VirtualScroll" /> (2000 items): Grid template vs Magnet template.
/// Every Magnet cell declares its own definition inline; structurally identical cells share the compiled tape.
/// Counters: inflated cells (recycling), card measure/arrange passes, and the settle time of an animated scroll to the end.
/// </summary>
[UsedImplicitly]
[TestPage("Magnet VirtualScroll Perf")]
public class MagnetVirtualScrollPerfTestsPage : ContentPage
{
    public sealed class Item(int index)
    {
        public int Index { get; } = index;
        public string Name { get; } = MagnetPerfCards.NameFor(index);
    }

    private readonly Label _status = new() { AutomationId = "VsPerfStatus", FontSize = 12, FontFamily = "Courier" };
    private readonly Grid _host = new();
    private readonly ObservableCollection<Item> _items = new(Enumerable.Range(0, 2000).Select(i => new Item(i)));
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private VirtualScroll? _scroll;
    private long _scrollStart;
    private long _lastScrolled;
    private int _scrollEvents;
    private IDispatcherTimer? _settleTimer;

    public MagnetVirtualScrollPerfTestsPage()
    {
        var gridButton = new Button { Text = "Grid cells", AutomationId = "VsPerfGridButton", FontSize = 12 };
        gridButton.Clicked += (_, _) => Show(false);
        var magnetButton = new Button { Text = "Magnet cells", AutomationId = "VsPerfMagnetButton", FontSize = 12 };
        magnetButton.Clicked += (_, _) => Show(true);
        var endButton = new Button { Text = "Scroll to end", AutomationId = "VsPerfEndButton", FontSize = 12 };
        endButton.Clicked += (_, _) => ScrollTo(_items.Count - 1);
        var topButton = new Button { Text = "Top", AutomationId = "VsPerfTopButton", FontSize = 12 };
        topButton.Clicked += (_, _) => ScrollTo(0);
        var resetButton = new Button { Text = "Reset counters", AutomationId = "VsPerfResetButton", FontSize = 12 };
        resetButton.Clicked += (_, _) =>
        {
            MagnetPerfCards.ResetCounters();
            UpdateStatus("counters reset");
        };

        Content = new Grid
        {
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
            Children =
            {
                new HorizontalStackLayout { Spacing = 6, Padding = new Thickness(8, 4), Children = { gridButton, magnetButton, endButton, topButton, resetButton } },
                WithRow(_status, 1),
                WithRow(_host, 2)
            }
        };
    }

    private static View WithRow(View view, int row)
    {
        Grid.SetRow(view, row);

        return view;
    }

    private void Show(bool magnet)
    {
        _host.Clear();
        MagnetPerfCards.ResetCounters();
        var sw = Stopwatch.StartNew();

        _scroll = new VirtualScroll
        {
            AutomationId = "VsPerfScroll",
            ItemsSource = VirtualScroll.CreateObservableCollectionAdapter(_items),
            BackgroundColor = Colors.LightGray,
            ItemTemplate = new DataTemplate(() =>
            {
                // The template does not know the item yet: build a neutral card and bind the texts.
                var card = magnet ? MagnetPerfCards.CreateMagnetCard(0, out var name) : MagnetPerfCards.CreateGridCard(0, out name);
                name.SetBinding(Label.TextProperty, nameof(Item.Name));

                return card;
            })
        };

        _scroll.OnScrolled += (_, _) =>
        {
            _scrollEvents++;
            _lastScrolled = _clock.ElapsedTicks;
        };

        _host.Add(_scroll);
        UpdateStatus($"{(magnet ? "Magnet" : "Grid")} cells: shown in {sw.Elapsed.TotalMilliseconds:F1} ms (managed)");
    }

    private void ScrollTo(int index)
    {
        if (_scroll is null)
        {
            return;
        }

        MagnetPerfCards.MeasureCount = 0;
        MagnetPerfCards.ArrangeCount = 0;
        var inflatedBefore = MagnetPerfCards.InflatedCount;
        _scrollStart = _clock.ElapsedTicks;
        _lastScrolled = 0;
        _scrollEvents = 0;
        _scroll.ScrollTo(0, index, ScrollToPosition.MakeVisible, animated: true);

        _settleTimer?.Stop();
        _settleTimer = Dispatcher.CreateTimer();
        _settleTimer.Interval = TimeSpan.FromMilliseconds(100);
        _settleTimer.Tick += (_, _) =>
        {
            var now = _clock.ElapsedTicks;
            var sinceLast = Ms(now - (_lastScrolled == 0 ? _scrollStart : _lastScrolled));

            if (sinceLast < 500 && !(_lastScrolled == 0 && Ms(now - _scrollStart) > 3000))
            {
                return;
            }

            _settleTimer?.Stop();
            _settleTimer = null;
            var duration = _lastScrolled == 0 ? double.NaN : Ms(_lastScrolled - _scrollStart);
            UpdateStatus($"scroll to {index}: {duration:F0} ms, {_scrollEvents} scroll events, {MagnetPerfCards.InflatedCount - inflatedBefore} cells inflated during the scroll");
        };
        _settleTimer.Start();
    }

    private void UpdateStatus(string prefix)
        => _status.Text = $"{prefix}\ncells inflated {MagnetPerfCards.InflatedCount} · card measures {MagnetPerfCards.MeasureCount} · arranges {MagnetPerfCards.ArrangeCount} · arranges w/o measure {MagnetPerfCards.ArrangesWithoutMeasure}";

    private double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
