using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Nalu.Maui.DailyHelper.Models;
using Nalu.Maui.DailyHelper.Services;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;

namespace Nalu.Maui.DailyHelper.PageModels;

public partial class WeatherDetailPageModel : ObservableObject, IDisposable
{
    private readonly CompositeDisposable _subscriptions = [];
    private readonly ReadOnlyObservableCollection<HourForecast> _nextHours;

    [ObservableProperty]
    public partial CurrentConditions? Current { get; set; }

    /// <summary>Per-day roll-ups of the remaining forecast week ("Coming days" rows).</summary>
    [ObservableProperty]
    public partial IReadOnlyList<DayRow> Days { get; set; } = [];

    /// <summary>The next 24 hours, rendered by a horizontal VirtualScroll strip.</summary>
    public IVirtualScrollAdapter NextHoursAdapter { get; }

    public WeatherDetailPageModel(WeatherStore weather)
    {
        var from = DateTime.Now.AddHours(-1);
        var to = DateTime.Now.AddHours(24);

        _subscriptions.Add(
            weather.Hours
                   .Filter(h => h.Time > from && h.Time <= to)
                   .SortAndBind(out _nextHours, SortExpressionComparer<HourForecast>.Ascending(h => h.Time))
                   .Subscribe()
        );

        NextHoursAdapter = VirtualScroll.CreateObservableCollectionAdapter(_nextHours);

        // Daily roll-up: midday-representative glyph, min/max range, tomorrow onward. Each row
        // also carries a 3-hourly slice powering the ExpanderViewBox inline preview.
        _subscriptions.Add(
            weather.Hours
                   .ToCollection()
                   .Subscribe(hours => Days = hours
                                             .GroupBy(h => h.Time.Date)
                                             .Where(g => g.Key > DateTime.Today)
                                             .OrderBy(g => g.Key)
                                             .Select(g => new DayRow(
                                                 new DaySummary(
                                                     g.Key,
                                                     g.OrderBy(h => Math.Abs(h.Time.Hour - 13)).First().WeatherCode,
                                                     g.Min(h => h.Temperature),
                                                     g.Max(h => h.Temperature)),
                                                 g.Where(h => h.Time.Hour is >= 6 and <= 21 && h.Time.Hour % 3 == 0)
                                                  .OrderBy(h => h.Time)
                                                  .ToList()))
                                             .ToList())
        );

        _subscriptions.Add(weather.Current.Subscribe(current => Current = current));
    }

    public void Dispose() => _subscriptions.Dispose();
}

/// <summary>
/// A "Coming days" row: the day roll-up plus a 3-hourly slice revealed inline by the
/// ExpanderViewBox when the row is tapped.
/// </summary>
public sealed partial class DayRow(DaySummary summary, IReadOnlyList<HourForecast> hours) : ObservableObject
{
    public DaySummary Summary { get; } = summary;

    public IReadOnlyList<HourForecast> Hours { get; } = hours;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ChevronGlyph))]
    public partial bool IsExpanded { get; set; }

    public string ChevronGlyph => IsExpanded ? "\ue5ce" /* expand_less */ : "\ue5cf" /* expand_more */;

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;
}
