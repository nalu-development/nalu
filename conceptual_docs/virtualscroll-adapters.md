# Custom Adapters

For advanced scenarios requiring sectioned data or direct data source access, implement `IVirtualScrollAdapter`:

```csharp
public interface IVirtualScrollAdapter
{
    int GetSectionCount();
    int GetItemCount(int sectionIndex);
    object? GetSection(int sectionIndex);
    object? GetItem(int sectionIndex, int itemIndex);
    IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback);
}
```

The adapter pattern is optimal since it allows for easily creating adapters backed by direct-access data stores such as databases. Instead of trying to load all data from the datastore into an in-memory collection and dealing with cache invalidation, you can write your adapter directly against any type of storage.

## Example: Grouped Data Adapter

```csharp
public class GroupedDataAdapter : IVirtualScrollAdapter
{
    private readonly ObservableCollection<MyGroup> _groups;

    public GroupedDataAdapter(ObservableCollection<MyGroup> groups)
    {
        _groups = groups;
    }

    public int GetSectionCount() => _groups.Count;

    public int GetItemCount(int sectionIndex) => _groups[sectionIndex].Items.Count;

    public object? GetSection(int sectionIndex) => _groups[sectionIndex];

    public object? GetItem(int sectionIndex, int itemIndex) => _groups[sectionIndex].Items[itemIndex];

    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback)
    {
        // Implement change notifications for your data structure
        // Return a disposable that unsubscribes when disposed
        return new MySubscription(_groups, changeCallback);
    }
}
```

## Example: SQLite Database Adapter (Flat List)

For many scenarios, it is ideal to create adapters that query data directly from a database. Here's an example of a custom adapter for a flat list (no sections/grouping) backed by SQLite. Notice that we cache commonly used data such as `ItemCount` and reset the cache when data changes:

```csharp
public class SQLiteAdapter : IVirtualScrollAdapter
{
    private readonly Database _db;
    private int? _cachedItemCount;
    private Action<VirtualScrollChangeSet>? _changeCallback;

    public SQLiteAdapter(Database database)
    {
        _db = database;
    }

    // Single section for flat lists
    public int GetSectionCount() => 1;

    // Cache the count to avoid repeated queries
    public int GetItemCount(int sectionIndex)
        => _cachedItemCount ??= _db.ExecuteScalar<int>("SELECT COUNT(Id) FROM Items");

    public object? GetSection(int sectionIndex) => null;

    // Query single item on demand - only loads what's visible
    public object? GetItem(int sectionIndex, int itemIndex)
        => _db.FindWithQuery<ItemInfo>("SELECT * FROM Items ORDER BY Id LIMIT 1 OFFSET ?", itemIndex);

    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback)
    {
        _changeCallback = changeCallback;
        return new SubscriptionHandle(() => _changeCallback = null);
    }

    // Call this after insert/delete operations
    public void InvalidateData()
    {
        _cachedItemCount = null;
        _changeCallback?.Invoke(new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.Reset() }));
    }

    private sealed class SubscriptionHandle(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
```

**Usage:**

```csharp
public partial class ItemListPageModel : ObservableObject
{
    public SQLiteAdapter Adapter { get; }

    public ItemListPageModel(Database database)
    {
        Adapter = new SQLiteAdapter(database);
    }

    [RelayCommand]
    private async Task AddItemAsync()
    {
        await _database.InsertAsync(new ItemInfo { Name = "New Item" });
        Adapter.InvalidateData(); // Refresh the list
    }
}
```

## Example: SQLite Database Adapter (Sectioned/Grouped)

Here's an example of a more sophisticated adapter with grouping/sections that queries directly from SQLite:

```csharp
public class SQLiteSectionedAdapter : IVirtualScrollAdapter
{
    private readonly Database _db;
    private int? _cachedSectionCount;
    private readonly Dictionary<int, GroupInfo> _cachedSections = new();
    private readonly Dictionary<int, int> _cachedItemCounts = new();
    private Action<VirtualScrollChangeSet>? _changeCallback;

    public SQLiteSectionedAdapter(Database database)
    {
        _db = database;
    }

    public int GetSectionCount()
        => _cachedSectionCount ??= _db.ExecuteScalar<int>("SELECT COUNT(DISTINCT GroupId) FROM Items");

    public int GetItemCount(int sectionIndex)
    {
        if (_cachedItemCounts.TryGetValue(sectionIndex, out var count))
            return count;

        // Get the section first to know its GroupId
        var section = (GroupInfo)GetSection(sectionIndex)!;
        count = _db.ExecuteScalar<int>("SELECT COUNT(Id) FROM Items WHERE GroupId = ?", section.GroupId);
        _cachedItemCounts[sectionIndex] = count;
        return count;
    }

    public object? GetSection(int sectionIndex)
    {
        if (_cachedSections.TryGetValue(sectionIndex, out var section))
            return section;

        var sql = @"
            SELECT DISTINCT g.GroupId, g.GroupName, COUNT(i.Id) as ItemCount
            FROM Groups g
                INNER JOIN Items i ON i.GroupId = g.GroupId
            GROUP BY g.GroupId
            ORDER BY g.GroupName
            LIMIT 1 OFFSET ?
        ";

        var groupInfo = _db.FindWithQuery<GroupInfo>(sql, sectionIndex);
        if (groupInfo != null)
            _cachedSections[sectionIndex] = groupInfo;

        return groupInfo;
    }

    public object? GetItem(int sectionIndex, int itemIndex)
    {
        var section = (GroupInfo)GetSection(sectionIndex)!;
        return _db.FindWithQuery<ItemInfo>(
            "SELECT * FROM Items WHERE GroupId = ? ORDER BY Id LIMIT 1 OFFSET ?",
            section.GroupId,
            itemIndex);
    }

    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback)
    {
        _changeCallback = changeCallback;
        return new SubscriptionHandle(() => _changeCallback = null);
    }

    // Call this after insert/delete/update operations
    public void InvalidateData()
    {
        _cachedSectionCount = null;
        _cachedSections.Clear();
        _cachedItemCounts.Clear();
        _changeCallback?.Invoke(new VirtualScrollChangeSet(new[] { VirtualScrollChangeFactory.Reset() }));
    }

    private sealed class SubscriptionHandle(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
```

**XAML usage with section templates:**

```xml
<nalu:VirtualScroll Adapter="{Binding Adapter}">
    <nalu:VirtualScroll.SectionHeaderTemplate>
        <DataTemplate x:DataType="models:GroupInfo">
            <Label Text="{Binding GroupName}"
                   FontSize="18"
                   FontAttributes="Bold"
                   BackgroundColor="LightGray"
                   Padding="16,8" />
        </DataTemplate>
    </nalu:VirtualScroll.SectionHeaderTemplate>

    <nalu:VirtualScroll.ItemTemplate>
        <DataTemplate x:DataType="models:ItemInfo">
            <nalu:ViewBox>
                <Border Margin="8" Padding="16">
                    <Label Text="{Binding Name}" />
                </Border>
            </nalu:ViewBox>
        </DataTemplate>
    </nalu:VirtualScroll.ItemTemplate>
</nalu:VirtualScroll>
```

## Benefits of Database-Backed Adapters

1. **Memory Efficiency**: Only items currently visible are loaded into memory
2. **No Data Duplication**: Data lives in the database, not duplicated in an `ObservableCollection`
3. **Lazy Loading**: Items are queried on-demand as the user scrolls
4. **Direct Source Access**: Works with any data store (SQLite, Realm, web APIs, etc.)
5. **Simple Cache Invalidation**: Call your `InvalidateData()` after any data modification, or implement specific and performant change notifications

