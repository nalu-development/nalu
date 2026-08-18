using DynamicData;
using Nalu.Maui.DailyHelper.Models;
using OpenMeteo;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Nalu.Maui.DailyHelper.Services;

/// <summary>
/// Centralized weather state: a <see cref="SourceCache{TObject,TKey}"/> of hourly forecasts plus
/// a "current conditions" stream. Pages never fetch data themselves — they subscribe, filter,
/// group and sort this single source of truth. Switching unit re-publishes the whole cache in
/// one <c>EditDiff</c>, so every subscribed view converts at once.
/// </summary>
public sealed class WeatherStore : IDisposable
{
    // Fixed demo location: no permission prompts, deterministic first run.
    private const string _placeName = "Rome";
    private const float _latitude = 45.5384f;
    private const float _longitude = 12.2916f;

    private readonly OpenMeteoClient _client = new();
    private readonly SourceCache<HourForecast, DateTime> _hours = new(h => h.Time);
    private readonly BehaviorSubject<CurrentConditions?> _current = new(null);
    private readonly BehaviorSubject<bool> _useFahrenheit = new(false);
    private IReadOnlyList<HourForecast> _celsiusHours = [];

    /// <summary>Hourly forecast changesets (values already in the selected unit).</summary>
    public IObservable<IChangeSet<HourForecast, DateTime>> Hours => _hours.Connect();

    /// <summary>The latest "right now" snapshot (null until the first refresh).</summary>
    public IObservable<CurrentConditions?> Current => _current.AsObservable();

    /// <summary>Whether temperatures are published in Fahrenheit.</summary>
    public bool UseFahrenheit
    {
        get => _useFahrenheit.Value;
        set
        {
            if (_useFahrenheit.Value != value)
            {
                _useFahrenheit.OnNext(value);
                Publish();
            }
        }
    }

    public bool HasData => _hours.Count > 0;

    /// <summary>Fetches a week of hourly data from Open-Meteo and republishes the cache.</summary>
    public async Task RefreshAsync()
    {
        var today = DateTime.Today;

        var forecast = await _client.QueryAsync(
            new WeatherForecastOptions(_latitude, _longitude)
            {
                Start_date = today.ToString("yyyy-MM-dd"),
                End_date = today.AddDays(7).ToString("yyyy-MM-dd"),
                Timezone = "auto",
                Hourly = new HourlyOptions(
                    [
                        HourlyOptionsParameter.temperature_2m,
                        HourlyOptionsParameter.apparent_temperature,
                        HourlyOptionsParameter.precipitation,
                        HourlyOptionsParameter.windspeed_10m,
                        HourlyOptionsParameter.relativehumidity_2m,
                        HourlyOptionsParameter.weathercode
                    ]
                )
            }
        );

        if (forecast?.Hourly?.Time is not { } times)
        {
            return;
        }

        _celsiusHours = times
                        .Select((t, i) => new HourForecast(
                                DateTime.SpecifyKind(DateTime.Parse(t), DateTimeKind.Local),
                                forecast.Hourly.Temperature_2m![i] ?? 0,
                                forecast.Hourly.Apparent_temperature![i] ?? 0,
                                forecast.Hourly.Precipitation![i] ?? 0,
                                forecast.Hourly.Windspeed_10m![i] ?? 0,
                                forecast.Hourly.Relativehumidity_2m![i] ?? 0,
                                (int) (forecast.Hourly.Weathercode![i] ?? 0)
                            )
                        )
                        .ToList();

        Publish();
    }

    /// <summary>Converts the raw data to the selected unit and pushes it as one atomic update.</summary>
    private void Publish()
    {
        var hours = _useFahrenheit.Value
            ? _celsiusHours.Select(h => h with
                {
                    Temperature = ToFahrenheit(h.Temperature),
                    FeelsLike = ToFahrenheit(h.FeelsLike)
                }
            ).ToList()
            : _celsiusHours;

        _hours.EditDiff(hours, (a, b) => a == b);

        var now = DateTime.Now;
        var current = hours.Where(h => h.Time <= now).OrderBy(h => h.Time).LastOrDefault() ?? hours.FirstOrDefault();

        if (current is not null)
        {
            var todayHours = hours.Where(h => h.Time.Date == now.Date).ToList();

            _current.OnNext(
                new CurrentConditions(
                    _placeName,
                    current.Time,
                    current.Temperature,
                    current.FeelsLike,
                    todayHours.Min(h => h.Temperature),
                    todayHours.Max(h => h.Temperature),
                    todayHours.Sum(h => h.Precipitation),
                    current.WindSpeed,
                    current.Humidity,
                    current.WeatherCode
                )
            );
        }
    }

    private static double ToFahrenheit(double celsius) => (celsius * 9 / 5) + 32;

    public void Dispose()
    {
        _hours.Dispose();
        _current.Dispose();
        _useFahrenheit.Dispose();
    }
}
