namespace Nalu.Maui.Weather.PageModels;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Resources;
using Services;
using ViewModels;

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
    INavigationService navigationService)
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
        var start = time.Date;
        var end = start.AddDays(1);

        Message = Texts.LoadingIQ;
        var iq = await weatherService.GetAirQualityAsync((float)location.Latitude, (float)location.Longitude, start, end);

        Message = Texts.LoadingWeatherForecast;
        var forecast = await weatherService.GetWeatherAsync((float)location.Latitude, (float)location.Longitude, start, end);

        weatherState.WeatherData.AddRange(forecast);
        weatherState.AirQualityData.AddRange(iq);

        Message = string.Empty;

        // Instead of showing a button, we could just navigate to the main page automatically via dispatcher:
        // _ = dispatcher.DispatchAsync(NavigateToHomePage);

        // However, we're trying to measure the navigation speed here, so we'll just show the button and wait for user command.
        IsReady = true;
    }

    [RelayCommand]
    private Task NavigateToHomePage() => navigationService.GoToAsync(Navigation.Absolute().ShellContent<HomePageModel>());

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
