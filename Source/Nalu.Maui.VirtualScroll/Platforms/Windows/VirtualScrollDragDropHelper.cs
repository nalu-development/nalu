using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DragEventArgs = Microsoft.UI.Xaml.DragEventArgs;
using DragStartingEventArgs = Microsoft.UI.Xaml.DragStartingEventArgs;

namespace Nalu;

/// <summary>
/// Helper class for handling drag and drop operations on Windows ItemsRepeater.
/// </summary>
internal class VirtualScrollDragDropHelper : IDisposable
{
    private readonly ItemsRepeater _itemsRepeater;
    private readonly IVirtualScroll _virtualScroll;
    private readonly IVirtualScrollFlattenedAdapter? _flattenedAdapter;
    private VirtualScrollElementContainer? _draggingContainer;
    private int _originalSectionIndex = -1;
    private int _originalItemIndex = -1;
    private WeakReference<object?>? _draggingItem;

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
        
        // Handle drag leave at the ItemsRepeater level (for cleanup when leaving the entire control)
        _itemsRepeater.DragLeave += OnDragLeave;
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

        // Attach drag event handlers
        container.DragStarting += OnContainerDragStarting;
        container.DragOver += OnContainerDragOver;
        container.Drop += OnContainerDrop;
    }

    private void OnElementClearing(ItemsRepeater sender, ItemsRepeaterElementClearingEventArgs args)
    {
        if (args.Element is not VirtualScrollElementContainer container)
        {
            return;
        }

        // Remove drag event handlers when element is being recycled
        container.DragStarting -= OnContainerDragStarting;
        container.DragOver -= OnContainerDragOver;
        container.Drop -= OnContainerDrop;

        // Reset drag state if this was the dragging container
        if (container == _draggingContainer)
        {
            _draggingContainer = null;
            _originalSectionIndex = -1;
            _originalItemIndex = -1;
            _draggingItem = null;
        }
    }

    private void OnContainerDragStarting(UIElement sender, DragStartingEventArgs e)
    {
        if (sender is not VirtualScrollElementContainer container || _flattenedAdapter is null || _virtualScroll.DragHandler is null || _virtualScroll.Adapter is null)
        {
            e.Cancel = true;
            return;
        }

        // Only allow dragging items, not headers or footers
        if (container.FlattenedItem?.Type != VirtualScrollFlattenedPositionType.Item)
        {
            e.Cancel = true;
            return;
        }

        // Get section and item index for the container
        if (!_flattenedAdapter.TryGetSectionAndItemIndex(container.FlattenedIndex, out var sectionIndex, out var itemIndex))
        {
            e.Cancel = true;
            return;
        }

        // Get the item from the adapter
        var item = _virtualScroll.Adapter.GetItem(sectionIndex, itemIndex);
        var dragInfo = new VirtualScrollDragInfo(item, sectionIndex, itemIndex);

        // Call OnDragInitiating
        _virtualScroll.DragHandler.OnDragInitiating(dragInfo);

        // Check if item can be dragged
        if (!_virtualScroll.DragHandler.CanDragItem(dragInfo))
        {
            e.Cancel = true;
            return;
        }

        // Store drag state
        _draggingContainer = container;
        _originalSectionIndex = sectionIndex;
        _originalItemIndex = itemIndex;
        _draggingItem = new WeakReference<object?>(item);

        // Call OnDragStarted
        _virtualScroll.DragHandler.OnDragStarted(dragInfo);

        // Set data package with item identifier
        e.Data.SetData("VirtualScrollItem", container.FlattenedIndex);
        e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void OnContainerDragOver(object sender, DragEventArgs e)
    {
        if (sender is not VirtualScrollElementContainer container || 
            _flattenedAdapter is null || 
            _virtualScroll.DragHandler is null || 
            _virtualScroll.Adapter is null)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            return;
        }

        // Only allow dragging over items, not headers or footers
        if (container.FlattenedItem?.Type != VirtualScrollFlattenedPositionType.Item ||
            !_flattenedAdapter.TryGetSectionAndItemIndex(container.FlattenedIndex, out var destinationSectionIndex, out var destinationItemIndex))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            return;
        }

        // Get current source position (may have changed during drag)
        if (_draggingContainer is null || !_flattenedAdapter.TryGetSectionAndItemIndex(_draggingContainer.FlattenedIndex, out var currentSectionIndex, out var currentItemIndex))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            return;
        }

        var dragMoveInfo = new VirtualScrollDragDropInfo(
            GetItemWithCache(_virtualScroll.Adapter, currentSectionIndex, currentItemIndex),
            _originalSectionIndex,
            _originalItemIndex,
            currentSectionIndex,
            currentItemIndex,
            destinationSectionIndex,
            destinationItemIndex
        );

        var canDrop = _virtualScroll.DragHandler.CanDropItemAt(dragMoveInfo);
        e.AcceptedOperation = canDrop
            ? Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move
            : Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
        
        // Mark as handled to prevent bubbling
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        // If drag leaves the ItemsRepeater entirely and we have an active drag, clean up
        // This handles the case where user cancels drag (e.g., presses Escape or drags outside)
        if (_draggingContainer is not null)
        {
            var point = e.GetPosition(_itemsRepeater);
            var rect = new Windows.Foundation.Rect(0, 0, _itemsRepeater.ActualWidth, _itemsRepeater.ActualHeight);
            
            // If the point is outside the ItemsRepeater bounds, clean up
            if (!rect.Contains(point))
            {
                CleanupDragState();
            }
        }
    }

    private void OnContainerDrop(object sender, DragEventArgs e)
    {
        if (sender is not VirtualScrollElementContainer container ||
            _flattenedAdapter is null || 
            _virtualScroll.DragHandler is null || 
            _virtualScroll.Adapter is null)
        {
            CleanupDragState();
            return;
        }

        // Only allow dropping on items, not headers or footers
        if (container.FlattenedItem?.Type != VirtualScrollFlattenedPositionType.Item ||
            !_flattenedAdapter.TryGetSectionAndItemIndex(container.FlattenedIndex, out var destinationSectionIndex, out var destinationItemIndex))
        {
            // Cancel drag if dropped outside valid target
            CleanupDragState();
            return;
        }

        // Get current source position
        if (_draggingContainer is null || !_flattenedAdapter.TryGetSectionAndItemIndex(_draggingContainer.FlattenedIndex, out var sourceSectionIndex, out var sourceItemIndex))
        {
            CleanupDragState();
            return;
        }

        // Don't do anything if dropped on the same position
        if (sourceSectionIndex == destinationSectionIndex && sourceItemIndex == destinationItemIndex)
        {
            CleanupDragState();
            return;
        }

        var dragMoveInfo = new VirtualScrollDragMoveInfo(
            GetItemWithCache(_virtualScroll.Adapter, sourceSectionIndex, sourceItemIndex),
            sourceSectionIndex,
            sourceItemIndex,
            destinationSectionIndex,
            destinationItemIndex
        );

        _virtualScroll.DragHandler.MoveItem(dragMoveInfo);

        // Notify the adapter of the move
        _flattenedAdapter.OnAdapterChanged(
            new VirtualScrollChangeSet(
                [
                    new VirtualScrollChange(
                        VirtualScrollChangeOperation.MoveItem,
                        sourceSectionIndex,
                        sourceItemIndex,
                        destinationSectionIndex,
                        destinationItemIndex
                    )
                ]
            )
        );

        // Clean up drag state
        CleanupDragState();
        
        // Mark as handled to prevent bubbling
        e.Handled = true;
    }

    private void CleanupDragState()
    {
        if (_virtualScroll.DragHandler is not null && 
            _draggingContainer is not null && 
            _flattenedAdapter is not null &&
            _virtualScroll.Adapter is not null &&
            _flattenedAdapter.TryGetSectionAndItemIndex(_draggingContainer.FlattenedIndex, out var finalSectionIndex, out var finalItemIndex))
        {
            var dragInfo = new VirtualScrollDragInfo(
                GetItemWithCache(_virtualScroll.Adapter, finalSectionIndex, finalItemIndex), 
                finalSectionIndex, 
                finalItemIndex);
            _virtualScroll.DragHandler.OnDragEnded(dragInfo);
        }

        _originalSectionIndex = -1;
        _originalItemIndex = -1;
        _draggingItem = null;
        _draggingContainer = null;
    }

    private object? GetItemWithCache(IVirtualScrollAdapter adapter, int sourceSectionIndex, int sourceItemIndex)
        => _draggingItem?.TryGetTarget(out var cachedItem) is true ? cachedItem : adapter.GetItem(sourceSectionIndex, sourceItemIndex);

    public void Dispose()
    {
        if (_itemsRepeater is not null)
        {
            _itemsRepeater.ElementPrepared -= OnElementPrepared;
            _itemsRepeater.ElementClearing -= OnElementClearing;
            _itemsRepeater.DragLeave -= OnDragLeave;
        }

        _draggingContainer = null;
        _draggingItem = null;
    }
}
