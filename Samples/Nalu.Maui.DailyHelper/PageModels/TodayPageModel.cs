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

    /// <summary>The insights card slide (0 = progress, 1 = effort) — drives the SlideBox.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressChipOpacity), nameof(EffortChipOpacity))]
    public partial int InsightIndex { get; set; }

    public double ProgressChipOpacity => InsightIndex == 0 ? 1 : 0.45;

    public double EffortChipOpacity => InsightIndex == 1 ? 1 : 0.45;

    /// <summary>Open tasks due today (the list below).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllDone), nameof(Progress), nameof(ProgressLabel), nameof(OpenCountLabel))]
    public partial int OpenCount { get; set; }

    /// <summary>Completed tasks (any due date up to today).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AllDone), nameof(Progress), nameof(ProgressLabel))]
    public partial int DoneCount { get; set; }

    [ObservableProperty]
    public partial int OverdueCount { get; set; }

    /// <summary>Total expected effort of the remaining tasks, from the duration wheel.</summary>
    [ObservableProperty]
    public partial string PlannedDurationLabel { get; set; } = "—";

    public bool AllDone => OpenCount == 0 && DoneCount > 0;

    public double Progress => OpenCount + DoneCount == 0 ? 0 : (double) DoneCount / (OpenCount + DoneCount);

    public string ProgressLabel => $"{DoneCount} of {OpenCount + DoneCount} done";

    public string OpenCountLabel => OpenCount == 1 ? "1 task left" : $"{OpenCount} tasks left";

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

        // Insights card stats: one unfiltered pass over the store keeps the SlideBox slides
        // (progress ring + planned effort) live with every toggle/edit.
        _subscriptions.Add(
            _todos.Connect()
                  .ToCollection()
                  .Subscribe(items =>
                  {
                      var open = items.Where(t => t.Bucket == TodoBucket.Today).ToList();
                      OpenCount = open.Count;
                      DoneCount = items.Count(t => t.IsDone);
                      OverdueCount = open.Count(t => t.IsOverdue);

                      var planned = open.Aggregate(TimeSpan.Zero, (total, t) => total + (t.Duration ?? TimeSpan.Zero));
                      PlannedDurationLabel = planned == TimeSpan.Zero
                          ? "No effort estimated"
                          : planned.Hours > 0
                              ? planned.Minutes > 0 ? $"{planned.Hours} h {planned.Minutes} min planned" : $"{planned.Hours} h planned"
                              : $"{planned.Minutes} min planned";
                  })
        );

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
    private void SelectInsight(string index) => InsightIndex = int.Parse(index);

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
