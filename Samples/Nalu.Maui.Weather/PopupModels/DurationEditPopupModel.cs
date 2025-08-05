using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Weather.PopupModels;

public class DurationEditIntent : AwaitableIntent<TimeSpan?>
{
    public Action<DurationEditPopupModel> Builder { get; }

    public DurationEditIntent(Action<DurationEditPopupModel> builder)
    {
        Builder = builder;
    }
}

public partial class DurationEditPopupModel(INavigationService navigationService) : PopupModelBase<DurationEditIntent, TimeSpan?>(navigationService)
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

    private DurationEditIntent _intent = null!;

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

            return string.Format((string)Format, (object?)duration);
        }
    }

    public bool CanSave => Duration is not null;

    public override ValueTask OnEnteringAsync(DurationEditIntent intent)
    {
        intent.Builder(this);
        return base.OnEnteringAsync(intent);
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        if (Duration is null)
        {
            return;
        }

        await CloseAsync(Duration);
    }

    [RelayCommand]
    public Task CancelAsync() => CloseAsync();
}
