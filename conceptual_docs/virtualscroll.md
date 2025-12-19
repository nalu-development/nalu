## VirtualScroll [![Nalu.Maui.VirtualScroll NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.VirtualScroll.svg)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.VirtualScroll)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/)

A high-performance virtualized scrolling view designed to replace the traditional `CollectionView` in .NET MAUI applications.

`VirtualScroll` provides a more efficient implementation tailored specifically for Android and iOS platforms, offering smooth scrolling, dynamic item sizing, and proper support for observable collections.

> VirtualScroll is the result of significant platform-level work and performance learnings—including **over a year of hands-on experience** gained by contributing improvements to **.NET MAUI core** as a community contributor.
>
> **Note:** This package uses a **Non-Commercial License**. Please refer to the [LICENSE-VirtualScroll.md](https://github.com/nalu-development/nalu/blob/main/Source/Nalu.Maui.VirtualScroll/LICENSE-VirtualScroll.md) for details.
>
> I’m evaluating whether to relicense VirtualScroll under **MIT (including commercial use)** in the future, based on the level of **community support and donations**.

If this library is valuable to your work, consider supporting its continued development and maintenance through a donation:

<a target="_blank" href="https://buymeacoffee.com/albyrock87">
    <img src="assets/images/donate.png" style="height:44px">
</a>

### Getting Started

Add `VirtualScroll` to your application in `MauiProgram.cs`:

```csharp
var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseNaluVirtualScroll(); // Add this line
```

### Basic Usage

The simplest way to use `VirtualScroll` is to bind it to an `ObservableCollection<T>`:

```xml
<nalu:VirtualScroll Adapter="{Binding Items}">
    <nalu:VirtualScroll.ItemTemplate>
        <DataTemplate x:DataType="models:MyItem">
            <nalu:ViewBox>
                <Label Text="{Binding Name}" Padding="16" />
            </nalu:ViewBox>
        </DataTemplate>
    </nalu:VirtualScroll.ItemTemplate>
</nalu:VirtualScroll>
```

The `Adapter` property accepts:
- **`ObservableCollection<T>`**: Automatically wrapped with full change notification support (add, remove, move, replace, reset)
- **`IEnumerable`**: Static lists are supported but won't react to changes
- **`IVirtualScrollAdapter`**: Custom adapters for advanced scenarios like sectioned data

### Templates

`VirtualScroll` supports multiple template types to create rich scrolling experiences:

#### ItemTemplate

The template used to display each item in the collection:

```xml
<nalu:VirtualScroll.ItemTemplate>
    <DataTemplate x:DataType="models:Person">
        <nalu:ViewBox>
            <Border StrokeShape="RoundRectangle 8" Margin="8" Padding="16">
                <Label Text="{Binding FullName}" />
            </Border>
        </nalu:ViewBox>
    </DataTemplate>
</nalu:VirtualScroll.ItemTemplate>
```

> **Tip:** Wrap your item content in a `nalu:ViewBox` for optimal performance. `ViewBox` is a lightweight alternative to `ContentView` that doesn't rely on the legacy Xamarin Compatibility layout system.

#### HeaderTemplate & FooterTemplate

Display content at the very beginning and end of the scroll view:

```xml
<nalu:VirtualScroll Adapter="{Binding Items}">
    <nalu:VirtualScroll.HeaderTemplate>
        <DataTemplate x:DataType="pageModels:MyPageModel">
            <VerticalStackLayout>
                <Image Source="banner.png" HeightRequest="128" Aspect="AspectFit" />
                <Label Text="Welcome" FontSize="32" FontAttributes="Bold" HorizontalOptions="Center" />
            </VerticalStackLayout>
        </DataTemplate>
    </nalu:VirtualScroll.HeaderTemplate>

    <nalu:VirtualScroll.FooterTemplate>
        <DataTemplate x:DataType="pageModels:MyPageModel">
            <Label Text="{Binding FooterMessage}" Padding="16" HorizontalOptions="Center" />
        </DataTemplate>
    </nalu:VirtualScroll.FooterTemplate>

    <nalu:VirtualScroll.ItemTemplate>
        <!-- Item template here -->
    </nalu:VirtualScroll.ItemTemplate>
</nalu:VirtualScroll>
```

#### SectionHeaderTemplate & SectionFooterTemplate

For sectioned data (when using a custom `IVirtualScrollAdapter`), you can define templates for section headers and footers:

```xml
<nalu:VirtualScroll.SectionHeaderTemplate>
    <DataTemplate x:DataType="models:Section">
        <Label Text="{Binding Title}" FontSize="18" FontAttributes="Bold" BackgroundColor="LightGray" Padding="16,8" />
    </DataTemplate>
</nalu:VirtualScroll.SectionHeaderTemplate>

<nalu:VirtualScroll.SectionFooterTemplate>
    <DataTemplate x:DataType="models:Section">
        <BoxView HeightRequest="1" BackgroundColor="Gray" />
    </DataTemplate>
</nalu:VirtualScroll.SectionFooterTemplate>
```

### DataTemplateSelector Support

All templates support `DataTemplateSelector` for heterogeneous item types:

```xml
<nalu:VirtualScroll.ItemTemplate>
    <local:MyItemTemplateSelector
        TextTemplate="{StaticResource TextItemTemplate}"
        ImageTemplate="{StaticResource ImageItemTemplate}" />
</nalu:VirtualScroll.ItemTemplate>
```

### Layouts

The `ItemsLayout` property controls how items are arranged. Currently, `VirtualScroll` supports linear layouts:

```xml
<!-- Vertical scrolling (default) -->
<nalu:VirtualScroll ItemsLayout="{x:Static nalu:LinearVirtualScrollLayout.Vertical}" ... />

<!-- Horizontal scrolling -->
<nalu:VirtualScroll ItemsLayout="{x:Static nalu:LinearVirtualScrollLayout.Horizontal}" ... />
```

### Scroll To Item

`VirtualScroll` provides methods to programmatically scroll to specific items:

#### Scroll by Index

```csharp
// Scroll to item at index 5 in section 0
virtualScroll.ScrollTo(sectionIndex: 0, itemIndex: 5);

// Scroll to section header (use itemIndex: -1)
virtualScroll.ScrollTo(sectionIndex: 1, itemIndex: -1);

// With position and animation control
virtualScroll.ScrollTo(0, 10, ScrollToPosition.Center, animated: true);
```

#### Scroll by Object

```csharp
// Scroll to a specific item or section object
virtualScroll.ScrollTo(myItem);
virtualScroll.ScrollTo(myItem, ScrollToPosition.Start, animated: false);
```

The `ScrollToPosition` options are:
- `MakeVisible` (default): Scrolls just enough to make the item visible
- `Start`: Positions the item at the start of the viewport
- `Center`: Centers the item in the viewport
- `End`: Positions the item at the end of the viewport

### Pull-to-Refresh

Enable pull-to-refresh functionality with the following properties:

```xml
<nalu:VirtualScroll Adapter="{Binding Items}"
                    IsRefreshEnabled="True"
                    RefreshCommand="{Binding RefreshCommand}"
                    RefreshAccentColor="CornflowerBlue"
                    IsRefreshing="{Binding IsLoading}">
    ...
</nalu:VirtualScroll>
```

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `IsRefreshEnabled` | `bool` | Enables or disables pull-to-refresh. Default: `false` |
| `RefreshCommand` | `ICommand` | Command executed when the user triggers a refresh. The command receives a completion callback as parameter. |
| `RefreshAccentColor` | `Color` | The color of the refresh indicator |
| `IsRefreshing` | `bool` | Two-way bindable property indicating whether the refresh is in progress |

#### RefreshCommand Implementation

The `RefreshCommand` receives a completion callback that **must be invoked** when the refresh operation completes:

```csharp
[RelayCommand]
private async Task RefreshAsync(Action completionCallback)
{
    try
    {
        await LoadDataAsync();
    }
    finally
    {
        completionCallback(); // Always call this when done!
    }
}
```

#### OnRefresh Event

Alternatively, you can handle the `OnRefresh` event:

```csharp
virtualScroll.OnRefresh += async (sender, args) =>
{
    await LoadDataAsync();
    args.Complete(); // Signal completion
};
```

### Custom Adapters

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

#### Example: Grouped Data Adapter

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

#### Example: SQLite Database Adapter (Flat List)

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

#### Example: SQLite Database Adapter (Sectioned/Grouped)

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

#### Benefits of Database-Backed Adapters

1. **Memory Efficiency**: Only items currently visible are loaded into memory
2. **No Data Duplication**: Data lives in the database, not duplicated in an `ObservableCollection`
3. **Lazy Loading**: Items are queried on-demand as the user scrolls
4. **Direct Source Access**: Works with any data store (SQLite, Realm, web APIs, etc.)
5. **Simple Cache Invalidation**: Call your `InvalidateData()` after any data modification, or implement specific and performant change notifications

### Dynamic Item Sizing

`VirtualScroll` fully supports dynamic item sizes. Items can change their height/width at runtime, and the scroll view will automatically adjust:

```xml
<DataTemplate x:DataType="models:ExpandableItem">
    <nalu:ViewBox>
        <Border Margin="8" Padding="16">
            <VerticalStackLayout>
                <Label Text="{Binding Title}" />
                <!-- Content that may change size -->
                <Label Text="{Binding Description}" IsVisible="{Binding IsExpanded}" />
            </VerticalStackLayout>
            <Border.GestureRecognizers>
                <TapGestureRecognizer Command="{Binding ToggleExpandCommand}" />
            </Border.GestureRecognizers>
        </Border>
    </nalu:ViewBox>
</DataTemplate>
```

### Complete Example

Here's a complete example demonstrating `VirtualScroll` with header, footer, dynamic items, and scroll functionality:

**XAML:**

```xml
<?xml version="1.0" encoding="utf-8"?>
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:nalu="https://nalu-development.github.com/nalu/layouts"
             xmlns:pageModels="clr-namespace:MyApp.PageModels"
             x:Class="MyApp.Pages.ItemListPage"
             x:DataType="pageModels:ItemListPageModel"
             Title="Items">

    <Grid RowDefinitions="Auto,*">
        <!-- Toolbar -->
        <HorizontalStackLayout Grid.Row="0" Spacing="8" Padding="16,8">
            <Button Text="Add" Command="{Binding AddItemCommand}" />
            <Button Text="Remove" Command="{Binding RemoveItemCommand}" />
            <Button Text="Scroll" Command="{Binding ScrollToRandomCommand}" />
        </HorizontalStackLayout>

        <!-- VirtualScroll -->
        <nalu:VirtualScroll Grid.Row="1"
                            x:Name="VirtualScroll"
                            Adapter="{Binding Items}"
                            IsRefreshEnabled="True"
                            RefreshCommand="{Binding RefreshCommand}">

            <nalu:VirtualScroll.HeaderTemplate>
                <DataTemplate x:DataType="pageModels:ItemListPageModel">
                    <VerticalStackLayout Padding="16">
                        <Image Source="logo.png" HeightRequest="100" Aspect="AspectFit" />
                        <Label Text="My Items" FontSize="28" FontAttributes="Bold" HorizontalOptions="Center" />
                    </VerticalStackLayout>
                </DataTemplate>
            </nalu:VirtualScroll.HeaderTemplate>

            <nalu:VirtualScroll.FooterTemplate>
                <DataTemplate x:DataType="pageModels:ItemListPageModel">
                    <Label Text="{Binding ItemCount, StringFormat='Total: {0} items'}"
                           Padding="16"
                           HorizontalOptions="Center"
                           TextColor="Gray" />
                </DataTemplate>
            </nalu:VirtualScroll.FooterTemplate>

            <nalu:VirtualScroll.ItemTemplate>
                <DataTemplate x:DataType="pageModels:ItemViewModel">
                    <nalu:ViewBox>
                        <Border StrokeShape="RoundRectangle 8"
                                Margin="8"
                                Padding="16"
                                BackgroundColor="LightCoral">
                            <Label Text="{Binding Name}" />
                            <Border.GestureRecognizers>
                                <TapGestureRecognizer Command="{Binding TapCommand}" />
                            </Border.GestureRecognizers>
                        </Border>
                    </nalu:ViewBox>
                </DataTemplate>
            </nalu:VirtualScroll.ItemTemplate>

        </nalu:VirtualScroll>
    </Grid>
</ContentPage>
```

**PageModel:**

```csharp
public partial class ItemListPageModel : ObservableObject
{
    public ObservableCollection<ItemViewModel> Items { get; } = new();

    public int ItemCount => Items.Count;

    [RelayCommand]
    private void AddItem()
    {
        var index = Items.Count > 0 ? Random.Shared.Next(Items.Count) : 0;
        Items.Insert(index, new ItemViewModel($"Item {Items.Count + 1}"));
        OnPropertyChanged(nameof(ItemCount));
    }

    [RelayCommand]
    private void RemoveItem()
    {
        if (Items.Count > 0)
        {
            Items.RemoveAt(Random.Shared.Next(Items.Count));
            OnPropertyChanged(nameof(ItemCount));
        }
    }

    [RelayCommand]
    private async Task RefreshAsync(Action completionCallback)
    {
        await Task.Delay(1000); // Simulate loading
        completionCallback();
    }
}
```

**Code-Behind (for ScrollTo):**

```csharp
public partial class ItemListPage : ContentPage
{
    public ItemListPage(ItemListPageModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }

    public void ScrollToItem(int index)
    {
        VirtualScroll.ScrollTo(0, index, ScrollToPosition.Center);
    }
}
```

### Properties Reference

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Adapter` | `object?` | `null` | The data source. Accepts `ObservableCollection<T>`, `IEnumerable`, or `IVirtualScrollAdapter` |
| `ItemTemplate` | `DataTemplate?` | `null` | Template for each item |
| `HeaderTemplate` | `DataTemplate?` | `null` | Template for the global header |
| `FooterTemplate` | `DataTemplate?` | `null` | Template for the global footer |
| `SectionHeaderTemplate` | `DataTemplate?` | `null` | Template for section headers |
| `SectionFooterTemplate` | `DataTemplate?` | `null` | Template for section footers |
| `ItemsLayout` | `IVirtualScrollLayout` | `LinearVirtualScrollLayout.Vertical` | Controls item arrangement |
| `IsRefreshEnabled` | `bool` | `false` | Enables pull-to-refresh |
| `RefreshCommand` | `ICommand?` | `null` | Command for refresh action |
| `RefreshAccentColor` | `Color?` | `null` | Refresh indicator color |
| `IsRefreshing` | `bool` | `false` | Indicates refresh state (two-way) |

### Methods Reference

| Method | Description |
|--------|-------------|
| `ScrollTo(int sectionIndex, int itemIndex, ScrollToPosition position, bool animated)` | Scrolls to item by indices |
| `ScrollTo(object itemOrSection, ScrollToPosition position, bool animated)` | Scrolls to item or section by object reference |

### Platform Support

`VirtualScroll` is optimized for:
- **Android** - Uses `RecyclerView` under the hood
- **iOS / Mac Catalyst** - Uses `UICollectionView` under the hood

Windows support is not currently available.

### Performance Comparison with MAUI CollectionView

`VirtualScroll` is designed to provide superior performance compared to MAUI's built-in `CollectionView`. The following benchmarks demonstrate the performance improvements:

#### Android Performance

When using Android's `RecyclerView` adapter pattern, `VirtualScroll` shows significant improvements in view binding operations:

| Operation | MAUI CollectionView | Nalu VirtualScroll | Improvement |
|-----------|-------------------|-------------------|-------------|
| **OnBindViewHolder** | 168ms | 25ms | **85% faster** |
| **OnCreateViewHolder** | 4ms | 48ms | Slower (one-time cost) |

**Understanding the Metrics:**

- **`OnBindViewHolder`**: This is the critical operation that occurs **every time you scroll**. When a cell scrolls out of view, it's recycled and `OnBindViewHolder` is called to bind it to a new data item. This happens frequently during scrolling, making it the primary performance bottleneck. `VirtualScroll`'s **85% improvement** in this operation translates directly to smoother scrolling.

- **`OnCreateViewHolder`**: This operation only occurs when creating new cells to fill the visible viewport. It's a **one-time cost** per cell type. While `VirtualScroll` is slower here, this cost is amortized over the lifetime of the cell since cells are reused many times via `OnBindViewHolder`. The trade-off is beneficial because:
  - Cells are created once and reused many times
  - The slower creation is offset by much faster binding during scrolling
  - The overall scrolling experience is significantly smoother

#### iOS Performance

On iOS using `UICollectionView`, `VirtualScroll` demonstrates substantial performance gains:

| Platform | MAUI CollectionView | Nalu VirtualScroll | Improvement |
|----------|-------------------|-------------------|-------------|
| **iOS** | 684.4ms | 375.7ms | **45% faster** |

This **45% performance improvement** on iOS results in noticeably smoother scrolling, especially with large datasets or complex item templates.

#### Why VirtualScroll is Faster

1. **Optimized View Recycling**: `VirtualScroll` implements a more efficient cell recycling strategy, minimizing the overhead of binding operations during scrolling.

2. **Reduced Layout Overhead**: By using `ViewBox` instead of the legacy Xamarin Compatibility layout system, `VirtualScroll` reduces layout calculation overhead.

3. **Platform-Native Implementation**: Direct integration with platform-native virtualization (`RecyclerView` on Android, `UICollectionView` on iOS) eliminates abstraction layers that can introduce performance penalties.

4. **Efficient Change Notifications**: `VirtualScroll` handles `ObservableCollection` changes more efficiently, minimizing unnecessary view updates.

5. **Platform-Specific Guidance Adherence**: `VirtualScroll` implementation follows platform-specific best practices and guidelines, making it less prone to glitches and rendering issues compared to MAUI CollectionView abstractions that may not fully align with native platform guidance.

### Performance Tips

1. **Use `ViewBox`**: Wrap your item content in `nalu:ViewBox` instead of `ContentView` for better performance
2. **Avoid complex layouts in items**: Keep item templates as simple as possible
3. **Use `DataTemplateSelector` wisely**: While supported, having many different templates can impact recycling efficiency
4. **Prefer [`ObservableRangeCollection<T>`](https://github.com/jamesmontemagno/mvvm-helpers/blob/master/MvvmHelpers/ObservableRangeCollection.cs)**: It provides the best change notification support with minimal overhead

