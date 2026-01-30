using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Nalu.Maui.Sample.PageModels;

public partial class TenCarouselPageModel : ObservableObject, ILeavingAware
{
    private readonly IMessenger _messenger;
    private static int _instanceCount;

    private int _idCounter;
    
    public string Message { get; } = "This is page ten carousel - horizontal carousel layout!";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
    
    public ReplaceableObservableCollection<TenItem> Items { get; }
    public IVirtualScrollReorderableSource Adapter { get; }

    [ObservableProperty]
    public partial int CurrentIndex { get; set; } = 5;

    public TenCarouselPageModel(IMessenger messenger)
    {
        _messenger = messenger;
        Items = new ReplaceableObservableCollection<TenItem>(Enumerable.Range(1, 30).Select(i => new TenItem($"Item {i}")));
        Adapter = VirtualScroll.CreateObservableCollectionAdapter(Items);
        _idCounter = Items.Count;
    }


    [RelayCommand]
    private void AddItem()
    {
        if (Items.Count > 0)
        {
            var randomIndex = Random.Shared.Next(Items.Count);
            Items.Insert(randomIndex, new TenItem($"Item {_idCounter++}"));
        }
        else
        {
            Items.Add(new TenItem($"Item {_idCounter++}"));
        }
    }

    [RelayCommand]
    private void RemoveItem()
    {
        if (Items.Count > 0)
        {
            var randomIndex = Random.Shared.Next(Items.Count);
            Items.RemoveAt(randomIndex);
        }
    }

    [RelayCommand]
    private void ClearItems()
    {
        Items.Clear();
    }

    [RelayCommand]
    private void ReplaceItems()
    {
        var newItems = Enumerable.Range(1, Random.Shared.Next(10, 40))
            .Select(i => new TenItem($"Replaced Item {i}"));
        Items.ReplaceAll(newItems);
        _idCounter = Items.Count;
    }

    [RelayCommand]
    private void MoveItem()
    {
        if (Items.Count > 1)
        {
            var fromIndex = Random.Shared.Next(Items.Count);
            int toIndex;
            while ((toIndex = Random.Shared.Next(Items.Count)) == fromIndex)
            {
                // Ensure toIndex is different from fromIndex
            }

            Items.Move(fromIndex, toIndex);
        }
    }
    
    [RelayCommand]
    private void ScrollToItem()
    {
        if (Items.Count > 0)
        {
            var randomIndex = Random.Shared.Next(Items.Count);
            _messenger.Send(new TenCarouselPageScrollToItemMessage(randomIndex));
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
            Items.Insert(0, new TenItem($"Refreshed Item {_idCounter++}"));
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
}

public record TenCarouselPageScrollToItemMessage(int ItemIndex);
