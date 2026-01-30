using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using DynamicData;

namespace Nalu.Maui.Sample.PageModels;

public partial class DynamicDataPageModel : ObservableObject, ILeavingAware, IDisposable
{
    private readonly IMessenger _messenger;
    private static int _instanceCount;

    private int _idCounter;
    private readonly IDisposable _subscription;
    
    public string Message { get; } = "This is DynamicData page - using SourceList!";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
    
    public SourceList<DynamicDataItem> SourceList { get; }
    private readonly ReadOnlyObservableCollection<DynamicDataItem> _readOnlyItems;
    public ReadOnlyObservableCollection<DynamicDataItem> Items => _readOnlyItems;

    public DynamicDataPageModel(IMessenger messenger)
    {
        _messenger = messenger;
        SourceList = new SourceList<DynamicDataItem>();
        
        // Bind SourceList to ObservableCollection for VirtualScroll
        _subscription = SourceList.Connect()
            .Bind(out _readOnlyItems)
            .Subscribe();
        
        // Initialize with items
        var initialItems = Enumerable.Range(1, 30).Select(i => new DynamicDataItem($"Item {i}"));
        SourceList.AddRange(initialItems);
        _idCounter = SourceList.Count;
    }

    [RelayCommand]
    private void AddItem()
    {
        if (SourceList.Count > 0)
        {
            var randomIndex = Random.Shared.Next(SourceList.Count);
            SourceList.Insert(randomIndex, new DynamicDataItem($"Item {_idCounter++}"));
        }
        else
        {
            SourceList.Add(new DynamicDataItem($"Item {_idCounter++}"));
        }
    }

    [RelayCommand]
    private void RemoveItem()
    {
        if (SourceList.Count > 0)
        {
            var randomIndex = Random.Shared.Next(SourceList.Count);
            SourceList.RemoveAt(randomIndex);
        }
    }
    
    [RelayCommand]
    private void ReloadItem()
    {
        if (SourceList.Count > 0)
        {
            var randomIndex = Random.Shared.Next(SourceList.Count);
            SourceList.Edit(innerList =>
            {
                var dynamicDataItem = innerList[randomIndex];
                dynamicDataItem.Expanded = !dynamicDataItem.Expanded;
                innerList[randomIndex] = dynamicDataItem;
            });
        }
    }

    [RelayCommand]
    private void ClearItems()
    {
        SourceList.Clear();
    }

    [RelayCommand]
    private void ReplaceItems()
    {
        var newItems = Enumerable.Range(1, Random.Shared.Next(10, 40))
            .Select(i => new DynamicDataItem($"Replaced Item {i}"));
        SourceList.Edit(innerList =>
        {
            innerList.Clear();
            innerList.AddRange(newItems);
        });
        _idCounter = SourceList.Count;
    }

    [RelayCommand]
    private void MoveItem()
    {
        if (SourceList.Count > 1)
        {
            var fromIndex = Random.Shared.Next(SourceList.Count);
            int toIndex;
            while ((toIndex = Random.Shared.Next(SourceList.Count)) == fromIndex)
            {
                // Ensure toIndex is different from fromIndex
            }

            SourceList.Move(fromIndex, toIndex);
        }
    }
    
    [RelayCommand]
    private void ScrollToItem()
    {
        if (SourceList.Count > 0)
        {
            var randomIndex = Random.Shared.Next(SourceList.Count);
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
            SourceList.Insert(0, new DynamicDataItem($"Refreshed Item {_idCounter++}"));
        }
        finally
        {
            // Always call completion callback when done
            completionCallback();
        }
    }

    private CancellationTokenSource? _autoChangesCts;

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
        SourceList?.Dispose();
    }
}

public record DynamicDataPageScrollToItemMessage(int ItemIndex);

public partial class DynamicDataItem : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; }
    
    [ObservableProperty]
    public partial bool Expanded { get; set; }

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

