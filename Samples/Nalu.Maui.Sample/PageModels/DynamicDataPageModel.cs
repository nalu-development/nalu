using System.Reactive.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;

namespace Nalu.Maui.Sample.PageModels;

public partial class DynamicDataPageModel : ObservableObject, ILeavingAware, IAppearingAware, IDisposable
{
    private readonly IMessenger _messenger;
    private static int _instanceCount;

    private int _idCounter;
    private IDisposable? _subscription;
    
    public string Message { get; } = "This is DynamicData page - using SourceList!";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    private readonly SourceList<DynamicDataItem> _sourceList;
    private readonly IObservable<IChangeSet<DynamicDataItem>> _sourceListObservable;
    private CancellationTokenSource? _autoChangesCts;

    public IVirtualScrollAdapter Adapter { get; }

    public DynamicDataPageModel(IMessenger messenger, IDispatcher dispatcher)
    {
        _messenger = messenger;

        // Initialize with items
        _sourceList = new SourceList<DynamicDataItem>();
        var initialItems = Enumerable.Range(1, 200).Select(i => new DynamicDataItem($"Item {i}"));
        _sourceList.AddRange(initialItems);
        
        // Bind SourceList to ObservableCollection for VirtualScroll
        _sourceListObservable = _sourceList.Connect()
                                           .ObserveOn(new DispatcherScheduler(dispatcher))
                                           .Bind(out var items);

        Adapter = VirtualScroll.CreateObservableCollectionAdapter(items);
        
        _idCounter = _sourceList.Count;
    }

    [RelayCommand]
    private void AddItem()
    {
        if (_sourceList.Count > 0)
        {
            var randomIndex = Random.Shared.Next(_sourceList.Count);
            _sourceList.Insert(randomIndex, new DynamicDataItem($"Item {_idCounter++}"));
        }
        else
        {
            _sourceList.Add(new DynamicDataItem($"Item {_idCounter++}"));
        }
    }

    [RelayCommand]
    private void RemoveItem()
    {
        if (_sourceList.Count > 0)
        {
            var randomIndex = Random.Shared.Next(_sourceList.Count);
            _sourceList.RemoveAt(randomIndex);
        }
    }

    [RelayCommand]
    private void ClearItems()
    {
        _sourceList.Clear();
    }

    [RelayCommand]
    private void ReplaceItems()
    {
        var newItems = Enumerable.Range(1, Random.Shared.Next(10, 40))
            .Select(i => new DynamicDataItem($"Replaced Item {i}"));
        _sourceList.Edit(innerList =>
        {
            innerList.Clear();
            innerList.AddRange(newItems);
        });
        _idCounter = _sourceList.Count;
    }

    [RelayCommand]
    private void MoveItem()
    {
        if (_sourceList.Count > 1)
        {
            var fromIndex = Random.Shared.Next(_sourceList.Count);
            int toIndex;
            while ((toIndex = Random.Shared.Next(_sourceList.Count)) == fromIndex)
            {
                // Ensure toIndex is different from fromIndex
            }

            _sourceList.Move(fromIndex, toIndex);
        }
    }
    
    [RelayCommand]
    private void ScrollToItem()
    {
        if (_sourceList.Count > 0)
        {
            var randomIndex = Random.Shared.Next(_sourceList.Count);
            _messenger.Send(new DynamicDataPageScrollToItemMessage(randomIndex));
        }
    }

    [RelayCommand]
    private async Task RefreshAsync(Action completionCallback)
    {
        try
        {
            // Simulate loading for 2 seconds
            await Task.Delay(2000);
            
            // Simulate refreshing data - add a new item at the beginning
            _sourceList.Insert(0, new DynamicDataItem($"Refreshed Item {_idCounter++}"));
        }
        finally
        {
            // Always call completion callback when done
            completionCallback();
        }
    }

    [RelayCommand]
    private async void ToggleAutoChanges()
    {
        if (_autoChangesCts != null)
        {
            await _autoChangesCts.CancelAsync();
            _autoChangesCts = null;
            return;
        }
        
        _autoChangesCts = new CancellationTokenSource();
        var token = _autoChangesCts.Token;

        while (!token.IsCancellationRequested)
        {
            // ReSharper disable once MethodSupportsCancellation
            await Task.Delay(500);
            AddItem();
            RemoveItem();
            MoveItem();
            ScrollToItem();
        }
    }

    public ValueTask OnLeavingAsync()
    {
        _autoChangesCts?.Cancel();
        _autoChangesCts = null;
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        _subscription?.Dispose();
        _sourceList?.Dispose();
    }

    public ValueTask OnAppearingAsync()
    {
        _subscription ??= _sourceListObservable.Subscribe();
        return ValueTask.CompletedTask;
    }
}

public record DynamicDataPageScrollToItemMessage(int ItemIndex);

public partial class DynamicDataItem : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [RelayCommand]
    private void AddLine()
    {
        Name += "\nAnother line added to this item.";
    }

    public DynamicDataItem(string name)
    {
        Name = name;
    }
}
