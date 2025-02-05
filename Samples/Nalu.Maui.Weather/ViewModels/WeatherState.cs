namespace Nalu.Maui.Weather.ViewModels;

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Models;

public partial class WeatherState : ObservableObject
{
    [ObservableProperty]
    private Location _location;

    public ObservableRangeCollection<AirQualityModel> AirQualityData { get; } = new();
    public ObservableRangeCollection<WeatherModel> WeatherData { get; } = new();
}
