using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nalu.Maui.Sample.PageModels;

public partial class ElevenPageModel : ObservableObject
{
    private static int _instanceCount;
    private int _groupIdCounter;
    private int _itemIdCounter;

    public string Message { get; } = "Grouped VirtualScroll Demo";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    public ObservableCollection<ElevenGroup> Groups { get; }

    public IVirtualScrollAdapter Adapter { get; }

    public ElevenPageModel()
    {
        Groups = new ObservableCollection<ElevenGroup>(
            Enumerable.Range(1, 5).Select(i => CreateGroup())
        );

        Adapter = VirtualScroll.CreateObservableCollectionAdapter(Groups, g => g.Items);
    }

    private ElevenGroup CreateGroup()
    {
        var groupId = ++_groupIdCounter;
        var items = Enumerable.Range(1, Random.Shared.Next(3, 8))
            .Select(_ => new ElevenItem($"Item {++_itemIdCounter}"))
            .ToList();
        return new ElevenGroup($"Group {groupId}", items);
    }

    [RelayCommand]
    private void AddGroup()
    {
        var randomIndex = Groups.Count > 0 ? Random.Shared.Next(Groups.Count + 1) : 0;
        Groups.Insert(randomIndex, CreateGroup());
    }

    [RelayCommand]
    private void RemoveGroup()
    {
        if (Groups.Count > 0)
        {
            var randomIndex = Random.Shared.Next(Groups.Count);
            Groups.RemoveAt(randomIndex);
        }
    }

    [RelayCommand]
    private void AddItem()
    {
        if (Groups.Count > 0)
        {
            var groupIndex = Random.Shared.Next(Groups.Count);
            var group = Groups[groupIndex];
            var itemIndex = group.Items.Count > 0 ? Random.Shared.Next(group.Items.Count + 1) : 0;
            group.Items.Insert(itemIndex, new ElevenItem($"Item {++_itemIdCounter}"));
        }
    }

    [RelayCommand]
    private void RemoveItem()
    {
        var nonEmptyGroups = Groups.Where(g => g.Items.Count > 0).ToList();
        if (nonEmptyGroups.Count > 0)
        {
            var group = nonEmptyGroups[Random.Shared.Next(nonEmptyGroups.Count)];
            var itemIndex = Random.Shared.Next(group.Items.Count);
            group.Items.RemoveAt(itemIndex);
        }
    }

    [RelayCommand]
    private void MoveItem()
    {
        var nonEmptyGroups = Groups.Where(g => g.Items.Count > 1).ToList();
        if (nonEmptyGroups.Count > 0)
        {
            var group = nonEmptyGroups[Random.Shared.Next(nonEmptyGroups.Count)];
            var fromIndex = Random.Shared.Next(group.Items.Count);
            int toIndex;
            do
            {
                toIndex = Random.Shared.Next(group.Items.Count);
            } while (toIndex == fromIndex);
            
            group.Items.Move(fromIndex, toIndex);
        }
    }
}

/// <summary>
/// Represents a group/section in the VirtualScroll.
/// </summary>
public partial class ElevenGroup : ObservableObject
{
    public string Title { get; }
    
    public ObservableCollection<ElevenItem> Items { get; }

    public ElevenGroup(string title, IEnumerable<ElevenItem> items)
    {
        Title = title;
        Items = new ObservableCollection<ElevenItem>(items);
    }
}

/// <summary>
/// Represents an item within a group.
/// </summary>
public partial class ElevenItem : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; }

    [RelayCommand]
    private void Tap()
    {
        Name += " (tapped)";
    }

    public ElevenItem(string name)
    {
        Name = name;
    }
}
