using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Nalu.Maui.DailyHelper.Models;
using Nalu.Maui.DailyHelper.Services;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;

namespace Nalu.Maui.DailyHelper.PageModels;

/// <summary>
/// A fixed bucket section: its own filtered + sorted pipeline over the shared store.
/// Sections never appear or disappear, which keeps section indexes stable while dragging.
/// </summary>
public sealed class TaskSection : IDisposable
{
    private readonly ReadOnlyObservableCollection<TodoItem> _items;
    private readonly IDisposable _subscription;

    public TodoBucket Bucket { get; }
    public string Name { get; }
    public ReadOnlyObservableCollection<TodoItem> Items => _items;

    public TaskSection(TodoBucket bucket, string name, TodoStore store)
    {
        Bucket = bucket;
        Name = name;

        _subscription = store.Connect()
                             .Filter(t => t.Bucket == bucket)
                             .SortAndBind(out _items, SortExpressionComparer<TodoItem>.Ascending(t => t.SortOrder))
                             .Subscribe();
    }

    public void Dispose() => _subscription.Dispose();
}

/// <summary>
/// The Tasks list adapter: the DynamicData projections stay read-only, so instead of mutating
/// the bound collections, drag-and-drop is translated into a <see cref="TodoStore"/> update —
/// the pipelines then re-emit the exact move the finger performed. Collection notifications are
/// suppressed while the move is applied because the platform view already shows the dragged
/// cell in place.
/// </summary>
public sealed class TaskSectionsAdapter
    : VirtualScrollGroupedNotifyCollectionChangedAdapter<ObservableCollection<TaskSection>, ReadOnlyObservableCollection<TodoItem>>,
      IReorderableVirtualScrollAdapter
{
    private readonly TodoStore _store;
    private bool _applyingDragMove;

    public TaskSectionsAdapter(ObservableCollection<TaskSection> sections, TodoStore store)
        : base(sections, section => ((TaskSection) section).Items)
    {
        _store = store;
    }

    public bool CanDragItem(VirtualScrollDragInfo dragInfo) => true;

    public bool CanDropItemAt(VirtualScrollDragDropInfo dragDropInfo) => true;

    public void MoveItem(VirtualScrollDragMoveInfo dragMoveInfo)
    {
        _applyingDragMove = true;

        try
        {
            _store.Move((TodoItem) dragMoveInfo.Item!, Sections[dragMoveInfo.DestinationSectionIndex].Bucket, dragMoveInfo.DestinationItemIndex);
        }
        finally
        {
            _applyingDragMove = false;
        }
    }

    public void OnDragInitiating(VirtualScrollDragInfo dragInfo) { }

    public void OnDragStarted(VirtualScrollDragInfo dragInfo) { }

    public void OnDragEnded(VirtualScrollDragInfo dragInfo) { }

    protected override bool ShouldIgnoreCollectionChanges() => _applyingDragMove;
}

public partial class TasksPageModel : ObservableObject, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly TodoStore _todos;
    private readonly ObservableCollection<TaskSection> _sections;

    /// <summary>Serves the VirtualScroll as both ItemsSource and DragHandler.</summary>
    public TaskSectionsAdapter SectionsAdapter { get; }

    public TasksPageModel(INavigationService navigation, TodoStore todos)
    {
        _navigation = navigation;
        _todos = todos;

        _sections =
        [
            new TaskSection(TodoBucket.Today, "Today", todos),
            new TaskSection(TodoBucket.Upcoming, "Upcoming", todos),
            new TaskSection(TodoBucket.Done, "Done", todos)
        ];

        SectionsAdapter = new TaskSectionsAdapter(_sections, todos);
    }

    [RelayCommand]
    private void Toggle(TodoItem item) => _todos.Toggle(item.Id);

    [RelayCommand]
    private Task AddTaskAsync() => _navigation.GoToAsync(Nalu.Navigation.Relative().Push<TaskEditorPageModel>());

    [RelayCommand]
    private Task EditTaskAsync(TodoItem item)
        => _navigation.GoToAsync(Nalu.Navigation.Relative().Push<TaskEditorPageModel>().WithIntent(new TaskEditorIntent(item.Id)));

    public void Dispose()
    {
        foreach (var section in _sections)
        {
            section.Dispose();
        }
    }
}
