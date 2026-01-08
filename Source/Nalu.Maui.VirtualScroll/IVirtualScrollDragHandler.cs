namespace Nalu;

/// <summary>
/// A handler for drag and drop operations on items within a virtual scroll.
/// </summary>
/// <remarks>
/// Section header/footer and global header/footer views cannot be dragged.
/// </remarks>
public interface IVirtualScrollDragHandler
{
    /// <summary>
    /// Determines whether the specified item can be dragged.
    /// </summary>
    bool CanDragItem(VirtualScrollDragInfo dragInfo);

    /// <summary>
    /// Moves an item from its source position to a destination position.
    /// </summary>
    void MoveItem(VirtualScrollDragMoveInfo dragMoveInfo);

    /// <summary>
    /// Determines whether the specified item can be dropped at the destination position.
    /// </summary>
    bool CanDropItemAt(VirtualScrollDragDropInfo dragDropInfo);

    /// <summary>
    /// Called when a drag operation is started.
    /// </summary>
    void OnDragStarted(VirtualScrollDragInfo dragInfo);

    /// <summary>
    /// Called when a drag operation is canceled.
    /// </summary>
    void OnDragCanceled(VirtualScrollDragInfo virtualScrollDragInfo);
    
    /// <summary>
    /// Called when a drag operation ended.
    /// </summary>
    void OnDragEnded(VirtualScrollDragInfo virtualScrollDragInfo);
}

/// <summary>
/// Information about a drag operation in a VirtualScroll.
/// </summary>
/// <param name="Item">The data source item being dragged retrieved from the adapter in the corresponding drag position.</param>
/// <param name="SectionIndex">The section index for the item.</param>
/// <param name="ItemIndex">The item index for the item.</param>
public readonly record struct VirtualScrollDragInfo(object? Item, int SectionIndex, int ItemIndex);

/// <summary>
/// Information about a drop-over operation in a VirtualScroll.
/// </summary>
/// <param name="Item">The data source item being dragged retrieved from the adapter in the corresponding drag position.</param>
/// <param name="OriginalSectionIndex">The section index for the source item when the drag started.</param>
/// <param name="OriginalItemIndex">The item index for the source item when the drag started.</param>
/// <param name="CurrentSectionIndex">The section index for the source item.</param>
/// <param name="CurrentItemIndex">The item index for the source item.</param>
/// <param name="DestinationSectionIndex">The section index for the destination item.</param>
/// <param name="DestinationItemIndex">The item index for the destination item.</param>
public readonly record struct VirtualScrollDragDropInfo(object? Item, int OriginalSectionIndex, int OriginalItemIndex, int CurrentSectionIndex, int CurrentItemIndex, int DestinationSectionIndex, int DestinationItemIndex);

/// <summary>
/// Information about a drag move operation in a VirtualScroll.
/// </summary>
/// <param name="Item">The data source item being moved retrieved from the adapter in the corresponding source position.</param>
/// <param name="CurrentSectionIndex">The section index for the source item.</param>
/// <param name="CurrentItemIndex">The item index for the source item.</param>
/// <param name="DestinationSectionIndex">The section index for the destination item.</param>
/// <param name="DestinationItemIndex">The item index for the destination item.</param>
public readonly record struct VirtualScrollDragMoveInfo(object? Item, int CurrentSectionIndex, int CurrentItemIndex, int DestinationSectionIndex, int DestinationItemIndex);
