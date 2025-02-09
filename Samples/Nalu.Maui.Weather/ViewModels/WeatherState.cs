using CommunityToolkit.Mvvm.ComponentModel;
using Nalu.Maui.Weather.Models;

namespace Nalu.Maui.Weather.ViewModels;

public partial class WeatherState(TimeProvider timeProvider) : ObservableObject
{
    [ObservableProperty]
    private Location _location = null!;

    [ObservableProperty]
    private DailyWeatherModel _todayWeather;

    [ObservableProperty]
    private HourlyWeatherModel _currentWeather;

    [ObservableProperty]
    private HourlyAirQualityModel _currentAirQuality;

    public ObservableRangeCollection<HourlyAirQualityModel> TodayHourlyAirQualityData { get; } = new();
    public ObservableRangeCollection<HourlyWeatherModel> TodayHourlyWeatherData { get; } = new();
    public ObservableRangeCollection<DailyWeatherModel> DailyWeatherData { get; } = new();

    public void UpdateCurrent()
    {
        var nowHour = timeProvider.GetLocalNow().DateTime.Hour;
        var hourlyWeather = TodayHourlyWeatherData.First(x => x.Time.Hour == nowHour);
        CurrentWeather = hourlyWeather;

        var hourlyAirQuality = TodayHourlyAirQualityData.First(x => x.Time.Hour == nowHour);
        CurrentAirQuality = hourlyAirQuality;
    }
}
