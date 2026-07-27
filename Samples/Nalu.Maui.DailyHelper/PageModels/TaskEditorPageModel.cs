using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nalu.Maui.DailyHelper.Models;
using Nalu.Maui.DailyHelper.Services;

namespace Nalu.Maui.DailyHelper.PageModels;

/// <summary>Navigation intent opening the editor on an existing task.</summary>
public sealed record TaskEditorIntent(Guid Id);

public partial class TaskEditorPageModel(INavigationService navigation, TodoStore todos)
    : ObservableObject, IEnteringAware, IEnteringAware<TaskEditorIntent>
{
    private TodoItem? _original;

    [ObservableProperty]
    private string _pageTitle = "New task";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    private bool _hasDueDate;

    [ObservableProperty]
    private DateTime _dueDate = DateTime.Today;

    [ObservableProperty]
    private bool _canDelete;

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
                    DueDate = HasDueDate ? DateOnly.FromDateTime(DueDate) : null
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
