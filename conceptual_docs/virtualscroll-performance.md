# Performance

`VirtualScroll` is designed to provide superior performance compared to MAUI's built-in `CollectionView`. The following benchmarks demonstrate the performance improvements:

## Android Performance

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

## iOS Performance

On iOS using `UICollectionView`, `VirtualScroll` demonstrates substantial performance gains:

| Platform | MAUI CollectionView | Nalu VirtualScroll | Improvement |
|----------|-------------------|-------------------|-------------|
| **iOS** | 684.4ms | 375.7ms | **45% faster** |

This **45% performance improvement** on iOS results in noticeably smoother scrolling, especially with large datasets or complex item templates.

## Why VirtualScroll is Faster

1. **Optimized View Recycling**: `VirtualScroll` implements a more efficient cell recycling strategy, minimizing the overhead of binding operations during scrolling.

2. **Reduced Layout Overhead**: By using `ViewBox` instead of the legacy Xamarin Compatibility layout system, `VirtualScroll` reduces layout calculation overhead.

3. **Platform-Native Implementation**: Direct integration with platform-native virtualization (`RecyclerView` on Android, `UICollectionView` on iOS) eliminates abstraction layers that can introduce performance penalties.

4. **Efficient Change Notifications**: `VirtualScroll` handles `ObservableCollection` changes more efficiently, minimizing unnecessary view updates.

5. **Platform-Specific Guidance Adherence**: `VirtualScroll` implementation follows platform-specific best practices and guidelines, making it less prone to glitches and rendering issues compared to MAUI CollectionView abstractions that may not fully align with native platform guidance.

## Sizing strategy

[`SizingStrategy`](virtualscroll.md#sizing-strategy) is the one property that can make
`VirtualScroll` measure its content, so its cost is worth stating plainly:

| Mode | Measure cost | Re-measure churn |
|------|--------------|------------------|
| `Fill` (default) | **None.** The content size is never consulted — identical to the code path that existed before the property | None |
| `Max(n)` | Bounded by the items that fit within `n`, no matter how many the collection holds | **None once clamped**: at or past the cap the measured size cannot change, so pushes, item resizes and scrolling never reach the layout system |
| `Unbounded` | The whole collection is laid out — O(items) | Every content change re-measures the container |

The default costs nothing, so an app that never sets the property pays nothing.

The capped mode is the one to reach for: it is bounded on both axes of cost, and the clamp makes a
long list *cheaper* than a short one — once the content passes the cap the container stops being
invalidated entirely. On Android the cap is handed to `RecyclerView` as an `AT_MOST` measure spec,
so its auto-measure lays out children only until the cap is satisfied; on iOS the content size is
read from the collection view, which UIKit maintains during layout anyway.

Use `Unbounded` only for small, bounded collections — a dozen rows, not a feed. It defeats the point
of virtualization for measurement purposes (though rendering stays virtualized), and every insert
re-measures the whole container chain.

> [!NOTE]
> On iOS, item sizes are estimates until a cell is realized (`EstimatedItemSize` on the layout), so
> content shorter than the cap settles once as cells materialize. Content *longer* than the cap is
> unaffected — it clamps immediately. Keep `EstimatedItemSize` close to reality either way.

## Performance Tips

1. **Use `ViewBox`**: Wrap your item content in `nalu:ViewBox` instead of `ContentView` for better performance
2. **Avoid complex layouts in items**: Keep item templates as simple as possible
3. **Use `DataTemplateSelector` wisely**: While supported, having many different templates can impact recycling efficiency
4. **Prefer [`ObservableRangeCollection<T>`](https://github.com/jamesmontemagno/mvvm-helpers/blob/master/MvvmHelpers/ObservableRangeCollection.cs)**: It provides the best change notification support with minimal overhead
5. **Avoid calling `GetVisibleItemsRange()` in scroll handlers**: Use `ScrollPercentageY` from scroll events instead for infinite scroll scenarios
6. **Enable scroll events only when needed**: Scroll events are automatically disabled when no listeners are present, ensuring optimal performance
7. **Leave `SizingStrategy` alone unless the list must hug its content**, and prefer the capped form (`SizingStrategy="300"`) over `Unbounded` — see [Sizing strategy](#sizing-strategy)

