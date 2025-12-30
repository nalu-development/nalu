using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

namespace Nalu.Maui.Sample.PageModels;

public class ReplaceableObservableCollection<T> : ObservableCollection<T>
{
    public ReplaceableObservableCollection(IEnumerable<T> items) : base(items)
    {
    }

    private static readonly FieldInfo? _itemsField = typeof(ObservableCollection<T>).GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
    
    private List<T> GetItemsList()
    {
        if (_itemsField?.GetValue(this) is List<T> items)
        {
            return items;
        }
        
        // Fallback: use protected Items property via reflection
        var itemsProperty = typeof(ObservableCollection<T>).GetProperty("Items", BindingFlags.NonPublic | BindingFlags.Instance);
        if (itemsProperty?.GetValue(this) is List<T> protectedItems)
        {
            return protectedItems;
        }
        
        throw new InvalidOperationException("Unable to access internal Items list");
    }
    
    public void ReplaceAll(IEnumerable<T> items)
    {
        if (items == null)
        {
            throw new ArgumentNullException(nameof(items));
        }
        
        var itemsList = GetItemsList();
        
        // Replace the underlying list
        itemsList.Clear();
        foreach (var item in items)
        {
            itemsList.Add(item);
        }
        
        // Trigger a single Clear/Reset notification
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
    }
}

public partial class TenPageModel : ObservableObject, ILeavingAware
{
    private readonly IMessenger _messenger;
    private static int _instanceCount;

    private int _idCounter;
    
    public string Message { get; } = "This is page ten - custom tab bar works!";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
    
    public ReplaceableObservableCollection<TenItem> Items { get; }

    public TenPageModel(IMessenger messenger)
    {
        _messenger = messenger;
        Items = new ReplaceableObservableCollection<TenItem>(Enumerable.Range(1, 30).Select(i => new TenItem($"Item {i}")));
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
            _messenger.Send(new TenPageScrollToItemMessage(randomIndex));
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

public record TenPageScrollToItemMessage(int ItemIndex);

public partial class TenItem : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [RelayCommand]
    private void AddLine()
    {
        Name += "\nAnother line added to this item.";
    }

    public TenItem(string name)
    {
        Name = name;
    }
}

