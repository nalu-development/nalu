## VirtualScroll [![Nalu.Maui.VirtualScroll NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.VirtualScroll.svg)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.VirtualScroll)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/)

A high-performance virtualized scrolling view designed to replace the traditional `CollectionView` in .NET MAUI applications.

`VirtualScroll` provides a more efficient implementation tailored specifically for Android and iOS platforms, offering smooth scrolling, dynamic item sizing, and proper support for observable collections.

> **Note:** This package uses a **Non-Commercial License**. Please refer to the [LICENSE-VirtualScroll.md](https://github.com/nalu-development/nalu/blob/main/Source/Nalu.Maui.VirtualScroll/LICENSE-VirtualScroll.md) for details.

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

For advanced scenarios requiring sectioned data, implement `IVirtualScrollAdapter`:

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

### Performance Tips

1. **Use `ViewBox`**: Wrap your item content in `nalu:ViewBox` instead of `ContentView` for better performance
2. **Avoid complex layouts in items**: Keep item templates as simple as possible
3. **Use `DataTemplateSelector` wisely**: While supported, having many different templates can impact recycling efficiency
4. **Prefer `ObservableCollection<T>`**: It provides the best change notification support with minimal overhead

