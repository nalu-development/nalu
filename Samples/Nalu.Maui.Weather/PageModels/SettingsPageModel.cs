using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nalu.Maui.Weather.Popups;
using Nalu.Maui.Weather.Services;
using Nalu.Maui.Weather.ViewModels;

namespace Nalu.Maui.Weather.PageModels;

public partial class SettingsPageModel(
    WeatherState weatherState,
    IWeatherService weatherService,
    IPopupService popupService,
    IPreferences preferences
)
    : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    private TimeSpan _duration = TimeSpan.FromTicks(preferences.Get(Constants.PrefForecastDays, TimeSpan.FromDays(7).Ticks));

    public string DurationText => $"{Math.Round(Duration.TotalDays, MidpointRounding.AwayFromZero)} days";

    [RelayCommand]
    public async Task EditDurationAsync()
    {
        var result = await popupService.ShowPopupAsync<DurationEdit>(durationEdit =>
            {
                durationEdit.Duration = Duration;
                durationEdit.MaxDuration = TimeSpan.FromDays(14);
                durationEdit.WholeDuration = TimeSpan.FromDays(7);
                durationEdit.RoundToTicks = TimeSpan.FromDays(1).Ticks;
                durationEdit.Format = "{0:d\\ }days";
            }
        );

        if (result is TimeSpan newDuration)
        {
            Duration = newDuration;
            preferences.Set(Constants.PrefForecastDays, Duration.Ticks);
        }
    }
}
