using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.DailyHelper.Overlays;

/// <summary>Opens the duration sheet on the task's current expected duration.</summary>
public sealed record DurationSheetIntent(TimeSpan? Duration);

/// <summary>
/// The sheet's answer. A wrapper on purpose: the overlay task completes with <c>null</c> on
/// DISMISSAL (scrim tap, pull-down, back) — the caller must be able to tell "keep what you
/// had" apart from an explicit "no duration" picked with the Clear button.
/// </summary>
public sealed record DurationSheetResult(TimeSpan? Duration);

/// <summary>
/// Model-first bottom sheet (§7.2): receives the intent through the navigation-style
/// entering hook and closes itself through the injected <see cref="IOverlayRef"/>.
/// </summary>
public partial class DurationSheetModel(IOverlayRef overlay) : ObservableObject, IEnteringAware<DurationSheetIntent>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationLabel))]
    public partial TimeSpan? Duration { get; set; }

    public string DurationLabel
        => Duration is not { } duration || duration == TimeSpan.Zero
            ? "No duration"
            : duration.Hours > 0
                ? duration.Minutes > 0 ? $"{duration.Hours} h {duration.Minutes} min" : $"{duration.Hours} h"
                : $"{duration.Minutes} min";

    public ValueTask OnEnteringAsync(DurationSheetIntent intent)
    {
        Duration = intent.Duration;

        return ValueTask.CompletedTask;
    }

    [RelayCommand]
    private Task Done() => overlay.CloseAsync(new DurationSheetResult(Duration is { } d && d > TimeSpan.Zero ? d : null));

    [RelayCommand]
    private Task Clear() => overlay.CloseAsync(new DurationSheetResult(null));
}
