using CommunityToolkit.Mvvm.ComponentModel;
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

        _subscriptions.Add(weather.Current.Subscribe(current => Current = current));
    }

    public void Dispose() => _subscriptions.Dispose();
}
