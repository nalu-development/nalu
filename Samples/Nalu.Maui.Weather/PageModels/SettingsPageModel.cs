using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nalu.Maui.Weather.PopupModels;
using Nalu.Maui.Weather.Services;
using Nalu.Maui.Weather.ViewModels;

namespace Nalu.Maui.Weather.PageModels;

public partial class SettingsPageModel(
    WeatherState weatherState,
    IWeatherService weatherService,
    INavigationService navigationService,
    IPreferences preferences
)
    : ObservableObject
{
    private INavigation CurrentNavigation => 
        Application.Current?.Windows[0].Page?.Navigation ??
        throw new InvalidOperationException("Unable to locate INavigation");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    private TimeSpan _duration = TimeSpan.FromTicks(preferences.Get(Constants.PrefForecastDays, TimeSpan.FromDays(7).Ticks));

    public string DurationText => $"{Math.Round(Duration.TotalDays, MidpointRounding.AwayFromZero)} days";

    [RelayCommand]
    public async Task EditDurationAsync()
    {
        var result = await navigationService.ResolveIntentAsync<DurationEditPopupModel, TimeSpan?>(
            new DurationEditIntent(durationEdit =>
                {
                    durationEdit.Duration = Duration;
                    durationEdit.MaxDuration = TimeSpan.FromDays(14);
                    durationEdit.WholeDuration = TimeSpan.FromDays(7);
                    durationEdit.RoundToTicks = TimeSpan.FromDays(1).Ticks;
                    durationEdit.Format = "{0:d\\ }days";
                }
            )
        );

        if (result is { } newDuration)
        {
            Duration = newDuration;
            preferences.Set(Constants.PrefForecastDays, Duration.Ticks);
        }
    }
}
