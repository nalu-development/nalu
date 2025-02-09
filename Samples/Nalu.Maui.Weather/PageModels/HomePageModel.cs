using CommunityToolkit.Mvvm.ComponentModel;
using Nalu.Maui.Weather.Services;
using Nalu.Maui.Weather.ViewModels;

namespace Nalu.Maui.Weather.PageModels;

public class HomePageModel(
    WeatherState weatherState,
    IWeatherService weatherService
)
    : ObservableObject
{
    public WeatherState WeatherState => weatherState;
}
