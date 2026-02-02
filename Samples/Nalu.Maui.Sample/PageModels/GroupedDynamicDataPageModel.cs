using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DynamicData;
using DynamicData.Binding;

namespace Nalu.Maui.Sample.PageModels;

/// <summary>
/// Category used for grouping items. First value is shown first in the list.
/// </summary>
public enum ItemCategory
{
    Primary = 0,
    Secondary = 1
}

/// <summary>
/// A group of items sharing the same category, with sorted items bound to a ReadOnlyObservableCollection.
/// </summary>
public sealed class SampleItemGroup : IDisposable
{
    private readonly IObservableCache<SampleItem, int> _groupCache;
    private readonly ReadOnlyObservableCollection<SampleItem> _items;
    private readonly IDisposable _subscription;

    public ItemCategory Category { get; }
    public ReadOnlyObservableCollection<SampleItem> Items => _items;

    public SampleItemGroup(ItemCategory category, IObservableCache<SampleItem, int> groupCache)
    {
        Category = category;
        _groupCache = groupCache;
        _subscription = groupCache.Connect()
            .SortAndBind(
                out _items,
                SortExpressionComparer<SampleItem>.Ascending(item => item.Name)
            )
            .Subscribe();
    }

    public void Dispose()
    {
        _subscription.Dispose();
        _groupCache.Dispose();
    }
}

public partial class GroupedDynamicDataPageModel : ObservableObject, IDisposable
{
    private static int _instanceCount;
    private int _itemIdCounter;
    private readonly IDisposable _groupedSubscription;

    public string Message { get; } = "Grouped DynamicData Demo";

    public int InstanceCount { get; } = Interlocked.Increment(ref _instanceCount);

    public SourceCache<SampleItem, int> SourceCache { get; }

    public IVirtualScrollAdapter Adapter { get; }

    public GroupedDynamicDataPageModel()
    {
        SourceCache = new SourceCache<SampleItem, int>(item => item.Id);

        _groupedSubscription = SourceCache.Connect()
            .GroupOnProperty(item => item.Category)
            .Transform(group => new SampleItemGroup(group.Key, group.Cache))
            .SortAndBind(
                out var groupedItems,
                SortExpressionComparer<SampleItemGroup>.Ascending(g => g.Category)
            )
            .DisposeMany()
            .Subscribe();

        Adapter = VirtualScroll.CreateObservableCollectionAdapter(groupedItems, group => group.Items);

        // Seed with a few items in each category
        var categories = Enum.GetValues<ItemCategory>();
        for (var i = 0; i < 3; i++)
        {
            foreach (var category in categories)
            {
                SourceCache.AddOrUpdate(CreateItem(category));
            }
        }
    }

    private SampleItem CreateItem(ItemCategory category)
    {
        var id = ++_itemIdCounter;
        return new SampleItem(id, $"Item {id}", category);
    }

    [RelayCommand]
    private void AddItem()
    {
        var category = Random.Shared.Next(2) == 0 ? ItemCategory.Primary : ItemCategory.Secondary;
        SourceCache.AddOrUpdate(CreateItem(category));
    }

    [RelayCommand]
    private void RemoveItem()
    {
        if (SourceCache.Count > 0)
        {
            var key = SourceCache.Keys.ElementAt(Random.Shared.Next(SourceCache.Count));
            SourceCache.Remove(key);
        }
    }

    [RelayCommand]
    private void MoveItem()
    {
        if (SourceCache.Count < 2)
            return;
        var keys = SourceCache.Keys.ToList();
        var fromKey = keys[Random.Shared.Next(keys.Count)];
        var toKey = keys[Random.Shared.Next(keys.Count)];
        if (fromKey == toKey)
            return;
        var item = SourceCache.Lookup(fromKey).Value;
        SourceCache.Remove(fromKey);
        SourceCache.AddOrUpdate(item);
    }

    [RelayCommand]
    private void ClearGroups()
    {
        SourceCache.Clear();
    }

    [RelayCommand]
    private void SwitchCategory()
    {
        if (SourceCache.Count > 0)
        {
            var key = SourceCache.Keys.ElementAt(Random.Shared.Next(SourceCache.Count));
            var item = SourceCache.Lookup(key).Value;
            item.Category = item.Category == ItemCategory.Primary ? ItemCategory.Secondary : ItemCategory.Primary;
        }
    }

    public void Dispose()
    {
        _groupedSubscription.Dispose();
        SourceCache.Dispose();
    }
}

public partial class SampleItem : ObservableObject
{
    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private ItemCategory _category;

    public int Id { get; }

    [RelayCommand]
    private void Tap()
    {
        Name += " (tapped)";
    }

    public SampleItem(int id, string name, ItemCategory category)
    {
        Id = id;
        _name = name;
        _category = category;
    }
}
