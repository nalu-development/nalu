using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nalu.Maui.Weather.Resources;
using Nalu.Maui.Weather.Services;
using Nalu.Maui.Weather.ViewModels;

namespace Nalu.Maui.Weather.PageModels;

public class StartupIntent;

/// <summary>
/// A page acting like a splash screen, where we can do some initialization work.
/// </summary>
public partial class InitializationPageModel(
    IDispatcher dispatcher,
    IGeolocation geolocation,
    TimeProvider timeProvider,
    WeatherState weatherState,
    IWeatherService weatherService,
    INavigationService navigationService
)
    : ObservableObject, IAppearingAware<StartupIntent>
{
    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private bool _isReady;

    public async ValueTask OnAppearingAsync(StartupIntent intent)
    {
        // Here we can do some groundwork to log the user in, or check for updates, etc.
        // For now, we'll just retrieve the weather data, and navigate to the main page.
        Message = Texts.GettingLocation;

        var location = await GetGeoLocationAsync();
        weatherState.Location = location;

        var time = timeProvider.GetLocalNow();
        var today = time.Date;
        var forecastEnd = today.AddDays(14);

        Message = Texts.LoadingIQ;

        var hourlyAirQualityModels = await weatherService.GetHourlyAirQualityAsync(
            (float) location.Latitude,
            (float) location.Longitude,
            today,
            today
        );

        Message = Texts.LoadingWeatherForecast;

        var hourlyWeatherModels = await weatherService.GetHourlyWeatherAsync(
            (float) location.Latitude,
            (float) location.Longitude,
            today,
            today
        );

        var dailyWeatherModels = await weatherService.GetDailyWeatherAsync(
            (float) location.Latitude,
            (float) location.Longitude,
            today,
            forecastEnd
        );

        weatherState.TodayWeather = dailyWeatherModels.First();
        weatherState.TodayHourlyWeatherData.AddRange(hourlyWeatherModels);
        weatherState.TodayHourlyAirQualityData.AddRange(hourlyAirQualityModels.Take(24));
        weatherState.DailyWeatherData.AddRange(dailyWeatherModels.Skip(1));
        weatherState.UpdateCurrent();

        Message = string.Empty;

        // We can navigate to the main page now by either:
        // 1. Showing a button for the user to click: useful for navigation performance benchmarking
        // IsReady = true;
        // 2. Automatically navigating to the main page: useful for real-world user experience
        _ = dispatcher.DispatchAsync(NavigateToHomePage);
    }

    [RelayCommand]
    private Task NavigateToHomePage()
    {
        var navigation = Navigation
                         .Absolute(NavigationBehavior.Immediate | NavigationBehavior.PopAllPagesOnItemChange)
                         .ShellContent<HomePageModel>();

        return navigationService.GoToAsync(navigation);
    }

    private async Task<Location> GetGeoLocationAsync()
    {
        try
        {
            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
            var location = await geolocation.GetLocationAsync(request) ?? throw new FeatureNotSupportedException();

            return location;
        }
        // TODO: Catch specific exceptions:
        //   FeatureNotSupportedException
        //   FeatureNotEnabledException
        //   PermissionException
        catch (Exception ex)
        {
            // Unable to get location, fallback to default location
            return new Location(47.6720673, -122.1409981);
        }
    }
}
