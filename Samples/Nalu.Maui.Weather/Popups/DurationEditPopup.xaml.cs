using Nalu.Maui.Weather.PopupModels;

namespace Nalu.Maui.Weather.Popups;

public partial class DurationEditPopup
{
    private readonly DurationEditPopupModel _model;
    private const string _normalizeDurationAnimationKey = "NormalizeDuration";

    public DurationEditPopup(DurationEditPopupModel model)
    {
        InitializeComponent();
        BindingContext = _model = model;
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
