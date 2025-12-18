using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

public partial class TenPageModel : ObservableObject
{
    private static int _instanceCount;

    private int _idCounter;
    
    public string Message { get; } = "This is page ten - custom tab bar works!";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
    
    public ObservableCollection<TenItem> Items { get; }

    public TenPageModel()
    {
        Items = new(Enumerable.Range(1, 30).Select(i => new TenItem($"Item {i}")));
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
            await Task.Delay(500, token);
            AddItem();
            RemoveItem();
            MoveItem();
        }
    }
}

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

