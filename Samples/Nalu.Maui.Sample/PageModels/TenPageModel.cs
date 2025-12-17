using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

public partial class TenPageModel : ObservableObject
{
    private static int _instanceCount;

    public string Message { get; } = "This is page ten - custom tab bar works!";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);
    
    public ObservableCollection<TenItem> Items { get; } = new(Enumerable.Range(1, 100).Select(i => new TenItem($"Item {i}")));

    [RelayCommand]
    private void AddItem()
    {
        if (Items.Count > 0)
        {
            var randomIndex = Random.Shared.Next(Items.Count);
            Items.Insert(randomIndex, new TenItem($"Item {Items.Count}"));
        }
        else
        {
            Items.Add(new TenItem($"Item {Items.Count}"));
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
            var toIndex = Random.Shared.Next(Items.Count);
            if (fromIndex != toIndex)
            {
                Items.Move(fromIndex, toIndex);
            }
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

