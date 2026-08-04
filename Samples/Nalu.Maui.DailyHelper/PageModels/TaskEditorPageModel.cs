using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nalu.Maui.DailyHelper.Models;
using Nalu.Maui.DailyHelper.Overlays;
using Nalu.Maui.DailyHelper.Services;

namespace Nalu.Maui.DailyHelper.PageModels;

/// <summary>Navigation intent opening the editor on an existing task.</summary>
public sealed record TaskEditorIntent(Guid Id);

public partial class TaskEditorPageModel(INavigationService navigation, TodoStore todos, IOverlayService overlays)
    : ObservableObject, IEnteringAware, IEnteringAware<TaskEditorIntent>
{
    private TodoItem? _original;

    [ObservableProperty]
    public partial string PageTitle { get; set; } = "New task";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Notes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasDueDate { get; set; }

    [ObservableProperty]
    public partial DateTime DueDate { get; set; } = DateTime.Today;

    [ObservableProperty]
    public partial bool CanDelete { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DurationLabel))]
    public partial TimeSpan? Duration { get; set; }

    public string DurationLabel
        => Duration is not { } duration ? "None"
            : duration.Hours > 0
                ? duration.Minutes > 0 ? $"{duration.Hours} h {duration.Minutes} min" : $"{duration.Hours} h"
                : $"{duration.Minutes} min";

    /// <summary>
    /// Opens the duration-wheel bottom sheet (a model-first §7.2 overlay). A null RESULT means
    /// the sheet was dismissed (scrim/pull-down/back) — keep the current value; the wrapper's
    /// null Duration is the explicit Clear.
    /// </summary>
    [RelayCommand]
    private async Task EditDurationAsync()
    {
        var result = await overlays.ShowBottomSheetAsync<DurationSheetModel, DurationSheetResult>(new DurationSheetIntent(Duration));

        if (result is not null)
        {
            Duration = result.Duration;
        }
    }

    public ValueTask OnEnteringAsync()
    {
        _original = todos.CreateDraft();
        HasDueDate = true;

        return ValueTask.CompletedTask;
    }

    public ValueTask OnEnteringAsync(TaskEditorIntent intent)
    {
        _original = todos.Get(intent.Id);

        if (_original is not null)
        {
            PageTitle = "Edit task";
            Title = _original.Title;
            Notes = _original.Notes ?? string.Empty;
            HasDueDate = _original.DueDate is not null;
            DueDate = _original.DueDate?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
            Duration = _original.Duration;
            CanDelete = true;
        }

        return ValueTask.CompletedTask;
    }

    private bool CanSave => !string.IsNullOrWhiteSpace(Title);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync()
    {
        if (_original is { } original)
        {
            todos.Save(
                original with
                {
                    Title = Title.Trim(),
                    Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
                    DueDate = HasDueDate ? DateOnly.FromDateTime(DueDate) : null,
                    Duration = Duration
                }
            );
        }

        return navigation.GoToAsync(Nalu.Navigation.Relative().Pop());
    }

    [RelayCommand]
    private Task DeleteAsync()
    {
        if (_original is { } original)
        {
            todos.Delete(original.Id);
        }

        return navigation.GoToAsync(Nalu.Navigation.Relative().Pop());
    }
}
