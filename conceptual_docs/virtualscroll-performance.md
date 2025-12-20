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

## Performance Tips

1. **Use `ViewBox`**: Wrap your item content in `nalu:ViewBox` instead of `ContentView` for better performance
2. **Avoid complex layouts in items**: Keep item templates as simple as possible
3. **Use `DataTemplateSelector` wisely**: While supported, having many different templates can impact recycling efficiency
4. **Prefer [`ObservableRangeCollection<T>`](https://github.com/jamesmontemagno/mvvm-helpers/blob/master/MvvmHelpers/ObservableRangeCollection.cs)**: It provides the best change notification support with minimal overhead
5. **Avoid calling `GetVisibleItemsRange()` in scroll handlers**: Use `ScrollPercentageY` from scroll events instead for infinite scroll scenarios
6. **Enable scroll events only when needed**: Scroll events are automatically disabled when no listeners are present, ensuring optimal performance

