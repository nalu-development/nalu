namespace Nalu.Maui.Weather.PageModels;

using CommunityToolkit.Mvvm.ComponentModel;
using Models;
using OpenMeteo;
using Resources;
using Services;
using ViewModels;

public partial class HomePageModel(
    WeatherState weatherState,
    IWeatherService weatherService)
    : ObservableObject
{
    public WeatherState WeatherState => weatherState;
}
