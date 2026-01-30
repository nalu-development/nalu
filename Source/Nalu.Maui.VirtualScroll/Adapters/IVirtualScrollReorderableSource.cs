namespace Nalu;

/// <summary>
/// A <see cref="VirtualScroll.ItemsSource"/> adapter that supports automatic reordering of items via drag and drop.
/// </summary>
public interface IVirtualScrollReorderableSource : IVirtualScrollAdapter, IVirtualScrollDragHandler;
