using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Nalu.Maui.DailyHelper.Models;
using Nalu.Maui.DailyHelper.Services;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Nalu.Maui.DailyHelper.PageModels;

/// <summary>A forecast day: the group key plus its own sorted inner pipeline.</summary>
public sealed class DaySection : IDisposable
{
    private readonly ReadOnlyObservableCollection<HourForecast> _hours;
    private readonly IDisposable _subscription;

    public DateOnly Date { get; }

    public string Title
        => Date == DateOnly.FromDateTime(DateTime.Today) ? "Today"
            : Date == DateOnly.FromDateTime(DateTime.Today).AddDays(1) ? "Tomorrow"
            : Date.ToString("dddd d MMMM");

    public ReadOnlyObservableCollection<HourForecast> Hours => _hours;

    public DaySection(IGroup<HourForecast, DateTime, DateOnly> group)
    {
        Date = group.Key;

        _subscription = group.Cache.Connect()
                             .SortAndBind(out _hours, SortExpressionComparer<HourForecast>.Ascending(h => h.Time))
                             .Subscribe();
    }

    public void Dispose() => _subscription.Dispose();
}

public partial class ForecastPageModel : ObservableObject, IEnteringAware, IDisposable
{
    private readonly WeatherStore _weather;
    private readonly CompositeDisposable _subscriptions = [];
    private readonly BehaviorSubject<int> _dayRange;
    private readonly ReadOnlyObservableCollection<DaySection> _days;

    // Flyout toggles: which metrics each hourly row shows. Pure view state, the flyout
    // binds to them directly because per-page flyout content inherits the page's BindingContext.
    [ObservableProperty]
    private bool _showPrecipitation = true;

    [ObservableProperty]
    private bool _showWind;

    [ObservableProperty]
    private bool _showHumidity;

    public bool ShowFullWeek
    {
        get => _dayRange.Value == 7;
        set
        {
            if (ShowFullWeek != value)
            {
                _dayRange.OnNext(value ? 7 : 3);
                OnPropertyChanged();
            }
        }
    }

    public bool UseFahrenheit
    {
        get => _weather.UseFahrenheit;
        set
        {
            if (_weather.UseFahrenheit != value)
            {
                _weather.UseFahrenheit = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Grouped adapter: day sections wrapping their hourly rows.</summary>
    public IVirtualScrollAdapter DaysAdapter { get; }

    /// <summary>The forecast row for the running hour — the "Now" button's scroll target.</summary>
    public HourForecast? CurrentHour
    {
        get
        {
            var now = DateTime.Now;

            return _days.FirstOrDefault(d => d.Date == DateOnly.FromDateTime(now))
                        ?.Hours.LastOrDefault(h => h.Time <= now);
        }
    }

    public ForecastPageModel(WeatherStore weather)
    {
        _weather = weather;
        _dayRange = new BehaviorSubject<int>(7);

        // The whole page in one pipeline: dynamic day-range filter, group by day,
        // wrap each group in a section, sort sections chronologically.
        var rangeFilter = _dayRange.Select(days =>
            {
                var end = DateTime.Today.AddDays(days);

                return (Func<HourForecast, bool>) (h => h.Time < end);
            }
        );

        _subscriptions.Add(
            _weather.Hours
                    .Filter(rangeFilter)
                    .Group(h => DateOnly.FromDateTime(h.Time))
                    .Transform(g => new DaySection(g))
                    .DisposeMany()
                    .SortAndBind(out _days, SortExpressionComparer<DaySection>.Ascending(d => d.Date))
                    .Subscribe()
        );

        DaysAdapter = VirtualScroll.CreateObservableCollectionAdapter(_days, s => s.Hours);
    }

    public ValueTask OnEnteringAsync()
    {
        if (!_weather.HasData)
        {
            // Fire-and-forget: the grouped pipeline fills the page whenever data lands.
            _ = RefreshCoreAsync();
        }

        return ValueTask.CompletedTask;
    }

    [RelayCommand]
    private async Task RefreshAsync(Action completed)
    {
        try
        {
            await RefreshCoreAsync();
        }
        finally
        {
            completed();
        }
    }

    private async Task RefreshCoreAsync()
    {
        try
        {
            await _weather.RefreshAsync();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // Offline: keep whatever data we have.
        }
    }

    public void Dispose()
    {
        _subscriptions.Dispose();
        _dayRange.Dispose();
    }
}
