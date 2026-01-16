## VirtualScroll [![Nalu.Maui.VirtualScroll NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.VirtualScroll.svg)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.VirtualScroll)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/)

A high-performance virtualized scrolling view designed to replace the traditional `CollectionView` in .NET MAUI applications.

`VirtualScroll` provides a more efficient implementation tailored specifically for Android and iOS platforms, offering smooth scrolling, dynamic item sizing, and proper support for observable collections.

> VirtualScroll is the result of over a year of platform-level engineering and performance optimizations derived from my experience contributing core improvements to **.NET MAUI**.
>
> **Licensing:** This package is available under a **Dual License**. It is free for non-commercial use (MIT), while commercial use requires an active **[GitHub Sponsors subscription](https://github.com/sponsors/albyrock87)**. This model allows me to continue maintaining and improving high-performance components for the MAUI ecosystem. 
> 
> Please refer to the [LICENSE.md](https://github.com/albyrock87/nalu/blob/main/Source/Nalu.Maui.VirtualScroll/LICENSE.md) for full terms.

If this library is valuable to your work, consider supporting its continued development and maintenance ❤️

[![Sponsor](https://img.shields.io/badge/Sponsor-%E2%9D%A4-pink?logo=github&style=for-the-badge)](https://github.com/sponsors/albyrock87)

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
<nalu:VirtualScroll ItemsSource="{Binding Items}">
    <nalu:VirtualScroll.ItemTemplate>
        <DataTemplate x:DataType="models:MyItem">
            <nalu:ViewBox>
                <Label Text="{Binding Name}" Padding="16" />
            </nalu:ViewBox>
        </DataTemplate>
    </nalu:VirtualScroll.ItemTemplate>
</nalu:VirtualScroll>
```

The `ItemsSource` property accepts:
- **`IVirtualScrollAdapter`**: The standard adapter interface returned by factory methods (e.g., `CreateObservableCollectionAdapter`, `CreateStaticCollectionAdapter`) or implemented for custom scenarios ✅ **AOT-compatible** (recommended for AOT)
- **`ObservableCollection<T>`**, **`ReadOnlyObservableCollection<T>`**: Automatically wrapped with full change notification support (add, remove, move, replace, reset) ⚠️ **Not AOT-compatible** - use `IVirtualScrollAdapter` instead when using AOT
- **`IEnumerable`**: Static lists are supported but won't react to changes ✅ **AOT-compatible**

### Factory Methods

For programmatic adapter creation, `VirtualScroll` provides factory methods that offer type-safe ways to create adapters:

#### Observable Collection Adapters

Create adapters for observable collections with full change notification support:

```csharp
// Single collection
var adapter = VirtualScroll.CreateObservableCollectionAdapter(items);

// Read-only observable collection
var adapter = VirtualScroll.CreateObservableCollectionAdapter(readOnlyItems);

// Grouped collections
var adapter = VirtualScroll.CreateObservableCollectionAdapter(
    sections, 
    section => section.Items);
```

The factory methods support various combinations of `ObservableCollection<T>` and `ReadOnlyObservableCollection<T>` for both sections and items.

> **⚠️ AOT Compatibility:** When using AOT (Ahead-of-Time compilation), you must use these factory methods or create adapters explicitly. Automatic adapter creation from `INotifyCollectionChanged` collections is not supported in AOT mode and will throw a `NotSupportedException`. Always provide an `IVirtualScrollAdapter` when using AOT.

#### Static Collection Adapters

Create adapters for static collections (no change notifications):

```csharp
// Single collection
var adapter = VirtualScroll.CreateStaticCollectionAdapter(items);

// Grouped collections
var adapter = VirtualScroll.CreateStaticCollectionAdapter(
    sections, 
    section => section.Items);
```

Static collection adapters are useful when:
- Your data doesn't change after initial load
- You want to use `IEnumerable<T>` collections like arrays or LINQ results
- You're working with grouped data that doesn't need change notifications

**Example usage:**

```csharp
public partial class MyPageModel : ObservableObject
{
    public IVirtualScrollAdapter Adapter { get; }

    public MyPageModel()
    {
        // Create grouped adapter from static data
        var categories = new[]
        {
            new Category { Name = "A", Items = new[] { new Item("A1"), new Item("A2") } },
            new Category { Name = "B", Items = new[] { new Item("B1"), new Item("B2") } }
        };
        
        Adapter = VirtualScroll.CreateStaticCollectionAdapter(
            categories,
            category => category.Items);
    }
}
```

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
<nalu:VirtualScroll ItemsSource="{Binding Items}">
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

The `ItemsLayout` property controls how items are arranged. `VirtualScroll` supports linear and carousel layouts:

```xml
<!-- Vertical scrolling (default) -->
<nalu:VirtualScroll ItemsLayout="{nalu:VerticalVirtualScrollLayout}" ... />

<!-- Horizontal scrolling -->
<nalu:VirtualScroll ItemsLayout="{nalu:HorizontalVirtualScrollLayout}" ... />

<!-- Carousel layouts (paging + full-size items) -->
<nalu:VirtualScroll ItemsLayout="{nalu:HorizontalCarouselVirtualScrollLayout}" ... />
<nalu:VirtualScroll ItemsLayout="{nalu:VerticalCarouselVirtualScrollLayout}" ... />
```

Carousel layouts:
- Snap to pages (one item per viewport)
- Measure items to fill the available space
- Expose a two-way `CurrentRange` attached property representing the current item as `VirtualScrollRange`
  - This property can also be bound to `int` for the majority of use cases
- Loop-mode is not supported and it's not planned

**Carousel usage example:**

```xml
<vs:VirtualScroll Grid.Row="1"
                  x:Name="VirtualScroll"
                  DragHandler="{Binding Adapter}"
                  ItemsSource="{Binding Adapter}"
                  vs:CarouselVirtualScrollLayout.CurrentRange="{Binding CurrentIndex}"
                  ItemsLayout="{vs:HorizontalCarouselVirtualScrollLayout}">
    <vs:VirtualScroll.ItemTemplate>
        <DataTemplate x:DataType="pageModels:TenItem">
            <nalu:ViewBox>
                <Border Margin="8"
                        Padding="16,8"
                        BackgroundColor="LightCoral">
                    <Label Text="{Binding Name}"/>
                </Border>
            </nalu:ViewBox>
        </DataTemplate>
    </vs:VirtualScroll.ItemTemplate>
</vs:VirtualScroll>
```

```csharp
[ObservableProperty]
public partial int CurrentIndex { get; set; } = 5;
```

You can also configure estimated sizes for better performance and UX on iOS:

```xml
<!-- Vertical layout with custom estimated sizes -->
<nalu:VirtualScroll ItemsLayout="{nalu:VerticalVirtualScrollLayout EstimatedItemSize=72, EstimatedSectionHeaderSize=57}" ... />
```

The estimated size properties help reduce layout calculations on iOS, especially while `UICollectionView` estimates the total content size:
- `EstimatedItemSize`: Estimated size of each item (default: 64)
- `EstimatedHeaderSize`: Estimated size of the global header (default: 64)
- `EstimatedFooterSize`: Estimated size of the global footer (default: 64)
- `EstimatedSectionHeaderSize`: Estimated size of section headers (default: 64)
- `EstimatedSectionFooterSize`: Estimated size of section footers (default: 64)

### Scroll To Item

`VirtualScroll` provides methods to programmatically scroll to specific items. See [Scrolling](virtualscroll-scrolling.md) for details.

### Scroll Events

`VirtualScroll` provides scroll position changes plus scroll start/end events. See [Scrolling](virtualscroll-scrolling.md) for details.

### Visible Items Range

Get the range of currently visible items (including headers and footers). See [Scrolling](virtualscroll-scrolling.md) for details.

### Pull-to-Refresh

Enable pull-to-refresh functionality with the following properties:

```xml
<nalu:VirtualScroll ItemsSource="{Binding Items}"
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

### Fading Edge

`VirtualScroll` supports a fading edge effect that creates a smooth gradient at the scrollable edges, providing visual feedback about scrollable content. The fading edge automatically adapts to the scroll direction based on the `ItemsLayout` orientation.

```xml
<!-- Vertical scrolling with fading edge -->
<nalu:VirtualScroll ItemsSource="{Binding Items}"
                    ItemsLayout="{nalu:VerticalVirtualScrollLayout}"
                    FadingEdgeLength="16">
    ...
</nalu:VirtualScroll>

<!-- Horizontal scrolling with fading edge -->
<nalu:VirtualScroll ItemsSource="{Binding Items}"
                    ItemsLayout="{nalu:HorizontalVirtualScrollLayout}"
                    FadingEdgeLength="24">
    ...
</nalu:VirtualScroll>
```

#### How It Works

- **Vertical layouts**: The fading edge appears at the top and/or bottom edges when content extends beyond the visible area
- **Horizontal layouts**: The fading edge appears at the left and/or right edges when content extends beyond the visible area
- The fading edge automatically appears/disappears based on scroll position:
  - When scrolled to the start, only the end edge shows fading
  - When scrolled to the end, only the start edge shows fading
  - When in the middle, both edges show fading
  - When all content fits in the view, no fading edge is shown

#### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `FadingEdgeLength` | `double` | `0.0` | The length of the fading edge effect in device-independent units. A value of `0` disables the fading edge. The orientation (horizontal or vertical) is automatically determined from the `ItemsLayout`. |

> **Note:** The fading edge feature is optimized for Android and iOS platforms. On Android, it uses native `RecyclerView` fading edge support. On iOS, it uses a custom gradient mask implementation.

### Drag & Drop

`VirtualScroll` supports drag and drop operations, allowing users to reorder items by dragging them to new positions. All built-in adapters support drag and drop out of the box.

To enable drag and drop, bind the `DragHandler` property to your adapter:

```xml
<vs:VirtualScroll ItemsSource="{Binding Adapter}"
                  DragHandler="{Binding Adapter}">
    <!-- Templates -->
</vs:VirtualScroll>
```

Drag and drop works with both single collections and grouped collections, supporting moves within sections and between sections. See [Drag & Drop](virtualscroll-dragdrop.md) for complete documentation including custom behavior, lifecycle hooks, and advanced scenarios.

### Custom Adapters

For advanced scenarios requiring sectioned data or direct data source access, implement `IVirtualScrollAdapter`. See [Custom Adapters](virtualscroll-adapters.md) for complete documentation.

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
                            ItemsSource="{Binding Items}"
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

### API Reference

For complete API documentation including all properties, methods, and events, see the [VirtualScroll API Reference](https://nalu-development.github.io/nalu/api/Nalu.VirtualScroll.html).

### Platform Support

`VirtualScroll` is optimized for:
- **Android** - Uses `RecyclerView` under the hood
- **iOS / Mac Catalyst** - Uses `UICollectionView` under the hood

Windows support is not currently available.

### Performance Comparison with MAUI CollectionView

`VirtualScroll` is designed to provide superior performance compared to MAUI's built-in `CollectionView`. See [Performance](virtualscroll-performance.md) for detailed benchmarks and optimization tips.

## Learn More

- 📘 [Scrolling](virtualscroll-scrolling.md) - Scroll to item, scroll events, and visible items range
- 📘 [Custom Adapters](virtualscroll-adapters.md) - Creating custom adapters for sectioned data and database-backed lists
- 📘 [Drag & Drop](virtualscroll-dragdrop.md) - Reordering items with drag and drop, custom behavior, and lifecycle hooks
- 📘 [Performance](virtualscroll-performance.md) - Performance benchmarks and optimization tips
