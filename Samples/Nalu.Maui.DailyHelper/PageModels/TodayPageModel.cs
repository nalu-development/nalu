using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;
using Nalu.Maui.DailyHelper.Models;
using Nalu.Maui.DailyHelper.Services;
using System.Collections.ObjectModel;
using System.Reactive.Disposables;

namespace Nalu.Maui.DailyHelper.PageModels;

public partial class TodayPageModel : ObservableObject, IEnteringAware, IIntentHydrator<TaskEditorIntent>, IDisposable
{
    private readonly INavigationService _navigation;
    private readonly WeatherStore _weather;
    private readonly TodoStore _todos;
    private readonly CompositeDisposable _subscriptions = [];
    private readonly ReadOnlyObservableCollection<TodoItem> _items;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWeather))]
    public partial CurrentConditions? Current { get; set; }

    public bool HasWeather => Current is not null;

    /// <summary>Today's open tasks, bound to the page's VirtualScroll.</summary>
    public IVirtualScrollAdapter TodosAdapter { get; }

    public string DateLabel => DateTime.Today.ToString("dddd, d MMMM");

    public TodayPageModel(INavigationService navigation, WeatherStore weather, TodoStore todos)
    {
        _navigation = navigation;
        _weather = weather;
        _todos = todos;

        // One store, one pipeline: today's open tasks, live-sorted. Completing a task
        // re-evaluates the filter and the row animates out of the list.
        _subscriptions.Add(
            _todos.Connect()
                  .Filter(t => t.Bucket == TodoBucket.Today)
                  .SortAndBind(out _items, SortExpressionComparer<TodoItem>.Ascending(t => t.SortOrder))
                  .Subscribe()
        );

        TodosAdapter = VirtualScroll.CreateObservableCollectionAdapter(_items);

        _subscriptions.Add(_weather.Current.Subscribe(current => Current = current));
    }

    public ValueTask OnEnteringAsync()
    {
        if (!_weather.HasData)
        {
            // Fire-and-forget: never make the user wait for the network on navigation —
            // the reactive pipelines light the page up whenever data lands.
            _ = SafeRefreshAsync();
        }

        return ValueTask.CompletedTask;
    }

    [RelayCommand]
    private async Task RefreshAsync(Action completed)
    {
        try
        {
            await SafeRefreshAsync();
        }
        finally
        {
            completed();
        }
    }

    [RelayCommand]
    private void Toggle(TodoItem item) => _todos.Toggle(item.Id);

    [RelayCommand]
    private Task OpenWeatherAsync() => _navigation.GoToAsync(Nav.Push<WeatherDetailPageModel>());

    [RelayCommand]
    private Task AddTaskAsync() => _navigation.GoToAsync(Nav.Push<TaskEditorPageModel>());

    [RelayCommand]
    private Task EditTaskAsync(TodoItem item)
        => _navigation.GoToAsync(Nav.Push<TaskEditorPageModel>(new TaskEditorIntent(item.Id) { Item = item }));

    private async Task SafeRefreshAsync()
    {
        try
        {
            await _weather.RefreshAsync();
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            // Offline: keep whatever data we have.
        }
    }


    /// <summary>
    /// Restore hydration: the snapshot persisted only the task id — reload the item from the
    /// store before the replay recreates the editor (this page is already alive, sitting
    /// below the editor in the restoring stack).
    /// </summary>
    public ValueTask HydrateAsync(TaskEditorIntent intent)
    {
        intent.Item = _todos.Get(intent.Id);

        return ValueTask.CompletedTask;
    }

    public void Dispose() => _subscriptions.Dispose();
}
