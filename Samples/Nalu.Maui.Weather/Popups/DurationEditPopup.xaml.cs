using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Weather.Popups;

public partial class DurationEdit(IPopupService popupService) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText), nameof(CanSave))]
    private TimeSpan? _duration;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationText))]
    private string _format = "{0:hh\\:mm\\:ss}";

    [ObservableProperty]
    private TimeSpan? _maxDuration;

    [ObservableProperty]
    private TimeSpan _wholeDuration = TimeSpan.FromMinutes(1);

    public long? RoundToTicks { get; set; }

    public string DurationText
    {
        get
        {
            if (Duration is not { } duration)
            {
                return string.Empty;
            }

            if (RoundToTicks is not null)
            {
                duration = TimeSpan.FromTicks((long) Math.Round(duration.Ticks / (double) RoundToTicks.Value, MidpointRounding.AwayFromZero) * RoundToTicks.Value);
            }

            return string.Format(Format, duration);
        }
    }

    public bool CanSave => Duration is not null;

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (Duration is null)
        {
            return;
        }

        await popupService.ClosePopupAsync(Duration);
    }

    [RelayCommand]
    public Task CancelAsync() => popupService.ClosePopupAsync();
}

public partial class DurationEditPopup : Popup
{
    private readonly DurationEdit _model;
    private const string _normalizeDurationAnimationKey = "NormalizeDuration";

    public DurationEditPopup(DurationEdit model)
    {
        InitializeComponent();
        BindingContext = _model = model;
        Content.WidthRequest = Application.Current!.Windows[0].Width - 30;
    }

    private void DurationWheel_OnRotationEnded(object? sender, EventArgs e)
    {
        if (DurationWheel.Duration is not { } duration || _model.RoundToTicks is null)
        {
            return;
        }

        var normalizedTicks = (long) Math.Round(duration.Ticks / (double) _model.RoundToTicks.Value, MidpointRounding.AwayFromZero) * _model.RoundToTicks.Value;
        DurationWheel.Animate(_normalizeDurationAnimationKey, v => DurationWheel.Duration = TimeSpan.FromTicks((long) v), duration.Ticks, normalizedTicks);
    }

    private void DurationWheel_OnRotationStarted(object? sender, EventArgs e)
    {
        DurationWheel.AbortAnimation(_normalizeDurationAnimationKey);
    }
}
