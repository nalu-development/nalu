---
name: nalu-maui-virtualscroll
description: Nalu.Maui.VirtualScroll: high-performance virtualized list (RecyclerView/UICollectionView). Use when building lists, carousels, sectioned data, pull-to-refresh, drag-and-drop reorder, or replacing MAUI CollectionView for performance.
---

# Nalu.Maui.VirtualScroll

High-performance virtualized scrolling (Android RecyclerView, iOS UICollectionView). Replaces MAUI CollectionView with dynamic item sizing, headers/footers, sections, carousel, pull-to-refresh, and drag-and-drop.

## When to use

- Long or complex lists where CollectionView performance is insufficient.
- Sectioned data, carousel, or reorderable lists.
- AOT builds: use `IVirtualScrollAdapter` (factory methods), not raw `ObservableCollection` binding.

## Setup

```csharp
builder.UseNaluVirtualScroll();
```

## ItemsSource and adapters

- **IVirtualScrollAdapter** (recommended, AOT-compatible): `VirtualScroll.CreateObservableCollectionAdapter(items)` or `CreateObservableCollectionAdapter(sections, s => s.Items)`; for static data: `CreateStaticCollectionAdapter(items)` or with sections.
- **ObservableCollection&lt;T&gt;** (not AOT): Auto-wrapped; full change notifications.
- **IEnumerable**: Static list; no change notifications.

Wrap item content in `nalu:ViewBox` for best performance.

```xml
<nalu:VirtualScroll ItemsSource="{Binding Adapter}">
    <nalu:VirtualScroll.ItemTemplate>
        <DataTemplate x:DataType="models:MyItem">
            <nalu:ViewBox>
                <Label Text="{Binding Name}" Padding="16" />
            </nalu:ViewBox>
        </DataTemplate>
    </nalu:VirtualScroll.ItemTemplate>
</nalu:VirtualScroll>
```

## Templates

- **ItemTemplate** (required for items), **HeaderTemplate**, **FooterTemplate** (binding context = page/VM).
- **SectionHeaderTemplate**, **SectionFooterTemplate** for sectioned adapters.
- **DataTemplateSelector**: All template properties accept a `DataTemplateSelector` for heterogeneous item types (e.g. different templates by item type). Set the selector as the template value; use sparingly—many templates can reduce recycling efficiency.

```xml
<nalu:VirtualScroll.ItemTemplate>
    <local:MyItemTemplateSelector
        TextTemplate="{StaticResource TextItemTemplate}"
        ImageTemplate="{StaticResource ImageItemTemplate}" />
</nalu:VirtualScroll.ItemTemplate>
```

## Layouts

- `{nalu:VerticalVirtualScrollLayout}` (default), `{nalu:HorizontalVirtualScrollLayout}`.
- Carousel: `HorizontalCarouselVirtualScrollLayout`, `VerticalCarouselVirtualScrollLayout`; bind `CarouselVirtualScrollLayout.CurrentRange` to `int` or `VirtualScrollRange` for current index.
- Optional: `EstimatedItemSize`, `EstimatedHeaderSize`, `EstimatedSectionHeaderSize`, etc. on layout for iOS.

## Scrolling

- **ScrollTo(sectionIndex, itemIndex)** or **ScrollTo(item)**; overloads with `ScrollToPosition` (MakeVisible, Start, Center, End) and `animated`.
- **ScrolledCommand** / **OnScrolled**: `VirtualScrollScrolledEventArgs` (ScrollX/Y, ScrollPercentageY, RemainingScrollY, etc.). Use for progress/infinite scroll; do **not** call `GetVisibleItemsRange()` inside scroll handlers (expensive).
- **ScrollStartedCommand** / **ScrollEndedCommand** for gesture start/end.

See [Scrolling](https://nalu-development.github.io/nalu/virtualscroll-scrolling.html) for visible range and infinite-scroll patterns.

## Pull-to-refresh

`IsRefreshEnabled="True"`, `RefreshCommand`, `IsRefreshing`, `RefreshAccentColor`. **RefreshCommand** receives a completion callback—invoke it when done: `completionCallback();` in a `RelayCommand` or `args.Complete()` with `OnRefresh` event.

## Drag and drop

Bind **DragHandler** to the same adapter as **ItemsSource** (adapters implement `IReorderableVirtualScrollAdapter`). Only data items are reorderable; headers/footers are not. See [Drag & Drop](https://nalu-development.github.io/nalu/virtualscroll-dragdrop.html) for custom behavior.

```xml
<nalu:VirtualScroll ItemsSource="{Binding Adapter}" DragHandler="{Binding Adapter}">
```

## Performance tips

1. Use `nalu:ViewBox` in item templates.
2. Keep item templates simple.
3. For AOT: use adapter factory methods; do not bind `ObservableCollection` directly.
4. Prefer `ObservableRangeCollection<T>` for bulk updates.
5. Do not call `GetVisibleItemsRange()` in scroll handlers; use `ScrollPercentageY` or `RemainingScrollY` for infinite scroll.

## Caveats

- **License**: Non-commercial use (MIT); commercial requires [GitHub Sponsors](https://github.com/sponsors/albyrock87). See package LICENSE.
- **Platform**: Android and iOS/Mac Catalyst only; no Windows.
- **AOT**: Must use `IVirtualScrollAdapter` (e.g. from factory); binding `ObservableCollection` directly throws in AOT.

## Additional context

- [VirtualScroll](https://nalu-development.github.io/nalu/virtualscroll.html) · [Scrolling](https://nalu-development.github.io/nalu/virtualscroll-scrolling.html) · [Adapters](https://nalu-development.github.io/nalu/virtualscroll-adapters.html) · [Drag & Drop](https://nalu-development.github.io/nalu/virtualscroll-dragdrop.html) · [Performance](https://nalu-development.github.io/nalu/virtualscroll-performance.html)
- Code: [Source/Nalu.Maui.VirtualScroll/](Source/Nalu.Maui.VirtualScroll/)
