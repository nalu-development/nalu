using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DataPackageOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation;
using DragEventArgs = Microsoft.UI.Xaml.DragEventArgs;
using DragStartingEventArgs = Microsoft.UI.Xaml.DragStartingEventArgs;
using DropCompletedEventArgs = Microsoft.UI.Xaml.DropCompletedEventArgs;

namespace Nalu;

/// <summary>
/// Helper class for handling drag and drop operations on Windows ItemsRepeater.
/// </summary>
internal class VirtualScrollDragDropHelper : IDisposable
{
    private static long _idCounter;
    private readonly ItemsRepeater _itemsRepeater;
    private readonly IVirtualScroll _virtualScroll;
    private readonly IVirtualScrollFlattenedAdapter? _flattenedAdapter;
    private readonly string _virtualScrollDragKey = "VirtualScroll" + Interlocked.Increment(ref _idCounter);
    private VirtualScrollDragInfo? _draggingInfo;
    private int _draggingFlattenedIndex;
    private int _draggingCurrentSectionIndex;
    private int _draggingCurrentItemIndex;

    public VirtualScrollDragDropHelper(
        ItemsRepeater itemsRepeater,
        IVirtualScroll virtualScroll,
        IVirtualScrollFlattenedAdapter? flattenedAdapter)
    {
        _itemsRepeater = itemsRepeater;
        _virtualScroll = virtualScroll;
        _flattenedAdapter = flattenedAdapter;

        // Hook into element preparation to attach drag events to item containers
        _itemsRepeater.ElementPrepared += OnElementPrepared;
        _itemsRepeater.ElementClearing += OnElementClearing;
    }

    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not VirtualScrollElementContainer container)
        {
            return;
        }

        // Only enable drag for items, not headers or footers
        if (container.FlattenedItem?.Type != VirtualScrollFlattenedPositionType.Item)
        {
            return;
        }
        
        // Enable drag and drop on the container
        container.CanDrag = true;
        container.AllowDrop = true;
        container.DragStarting += OnElementDragStarting;
        container.DragOver += OnElementDragOver;
    }

    private void OnElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is not VirtualScrollElementContainer container)
        {
            return;
        }

        // Remove drag event handlers when element is being recycled
        container.DragStarting -= OnElementDragStarting;
        container.DragOver -= OnElementDragOver;
        
        // Note: DropCompleted is only subscribed on the source container when drag starts,
        // so we don't need to unsubscribe it here as it's handled dynamically
    }
    
#pragma warning disable VSTHRD100
    private async void OnElementDragStarting(UIElement sender, DragStartingEventArgs args)
#pragma warning restore VSTHRD100
    {
        if (_flattenedAdapter is null || _virtualScroll.DragHandler is null || sender is not VirtualScrollElementContainer container)
        {
            args.Cancel = true;
            return;
        }

        // Get section and item index for the container
        var flattenedIndex = container.FlattenedIndex;
        if (!_flattenedAdapter.TryGetSectionAndItemIndex(flattenedIndex, out var sectionIndex, out var itemIndex))
        {
            args.Cancel = true;
            return;
        }
        
        // Get the item from the adapter
        var dragInfo = new VirtualScrollDragInfo(container.FlattenedItem?.Value, sectionIndex, itemIndex);

        // Call OnDragInitiating
        _virtualScroll.DragHandler.OnDragInitiating(dragInfo);
        
        // Check if item can be dragged
        if (!_virtualScroll.DragHandler.CanDragItem(dragInfo))
        {
            args.Cancel = true;
            return;
        }
        
        _draggingInfo = dragInfo;
        _draggingFlattenedIndex = flattenedIndex;
        _draggingCurrentSectionIndex = sectionIndex;
        _draggingCurrentItemIndex = itemIndex;

        // Subscribe to DropCompleted on the source container
        // This ensures we capture the event even if drag ends outside any container
        container.DropCompleted += OnElementDropCompleted;

        args.Data.SetData(_virtualScrollDragKey, true);
        args.Data.RequestedOperation = DataPackageOperation.Move;
        
        // Call OnDragStarted after drag is initiated
        _virtualScroll.DragHandler.OnDragStarted(dragInfo);
        
        // TODO: if the list changed after initiating,
        // we should update the cached flattened index
        // actually, the handler should do that on collection changed event
        // this is to support scenario on NinePage (drag section hides all items)
    }

    private void OnElementDragOver(object sender, DragEventArgs args)
    {
        var currentFlattenedIndex = _draggingFlattenedIndex;

        if (_flattenedAdapter is null || 
            _virtualScroll.DragHandler is null || 
            sender is not VirtualScrollElementContainer container ||
            !args.DataView.Contains(_virtualScrollDragKey) || _draggingInfo is not { } draggingInfo)
        {
            args.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        var flattenedIndex = container.FlattenedIndex;
        
        // Get section and item index for the container
        if (currentFlattenedIndex == flattenedIndex ||
            !_flattenedAdapter.TryGetSectionAndItemIndex(flattenedIndex, out var sectionIndex, out var itemIndex))
        {
            args.AcceptedOperation = DataPackageOperation.None;
            return;
        }
        
        // Destination changed, check again and cache the result
        var currentSectionIndex = _draggingCurrentSectionIndex;
        var currentItemIndex = _draggingCurrentItemIndex;

        var dragMoveInfo = new VirtualScrollDragDropInfo(
            draggingInfo.Item,
            draggingInfo.SectionIndex,
            draggingInfo.ItemIndex,
            currentSectionIndex,
            currentItemIndex,
            sectionIndex,
            itemIndex
        );

        // Verify we can drop the item here
        var canDrop = _virtualScroll.DragHandler.CanDropItemAt(dragMoveInfo);

        if (!canDrop)
        {
            args.AcceptedOperation = DataPackageOperation.None;
            return;
        }

        var moveInfo = new VirtualScrollDragMoveInfo(
            draggingInfo.Item,
            currentSectionIndex,
            currentItemIndex,
            sectionIndex,
            itemIndex
        );
        
        _draggingCurrentSectionIndex = sectionIndex;
        _draggingCurrentItemIndex = itemIndex;
        _draggingFlattenedIndex = flattenedIndex;

        _virtualScroll.DragHandler.MoveItem(moveInfo);

        // Notify the adapter of the move
        _flattenedAdapter.OnAdapterChanged(
            new VirtualScrollChangeSet(
                [
                    new VirtualScrollChange(
                        VirtualScrollChangeOperation.MoveItem,
                        currentSectionIndex,
                        currentItemIndex,
                        sectionIndex,
                        itemIndex
                    )
                ]
            )
        );
    }

    private void OnElementDropCompleted(UIElement sender, DropCompletedEventArgs args)
    {
        if (_virtualScroll.DragHandler is not { } dragHandler || _draggingInfo is not { } draggingInfo)
        {
            return;
        }

        // Call OnDragEnded when drag operation completes
        // This fires regardless of where the drag ended (inside or outside any container)
        dragHandler.OnDragEnded(draggingInfo);

        // Unsubscribe from DropCompleted on the source container (sender is the source container)
        if (sender is VirtualScrollElementContainer sourceContainer)
        {
            sourceContainer.DropCompleted -= OnElementDropCompleted;
        }

        // Clear dragging state
        _draggingInfo = null;
        _draggingFlattenedIndex = -1;
        _draggingCurrentSectionIndex = -1;
        _draggingCurrentItemIndex = -1;
    }

    public void Dispose()
    {
        _itemsRepeater.ElementPrepared -= OnElementPrepared;
        _itemsRepeater.ElementClearing -= OnElementClearing;
    }
}
