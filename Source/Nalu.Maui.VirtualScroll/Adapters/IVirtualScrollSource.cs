namespace Nalu;

/// <summary>
/// A <see cref="VirtualScroll.ItemsSource"/> adapter that provides support for batching updates notifications and drag-and-drop reordering of items.
/// </summary>
public interface IVirtualScrollSource : IVirtualScrollBatchableSource, IVirtualScrollReorderableSource;
