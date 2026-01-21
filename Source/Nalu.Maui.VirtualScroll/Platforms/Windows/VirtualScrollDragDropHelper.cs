using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DragEventArgs = Microsoft.UI.Xaml.DragEventArgs;
using DragStartingEventArgs = Microsoft.UI.Xaml.DragStartingEventArgs;
using DropCompletedEventArgs = Microsoft.UI.Xaml.DropCompletedEventArgs;

namespace Nalu;

/// <summary>
/// Helper class for handling drag and drop operations on Windows ItemsRepeater.
/// </summary>
internal class VirtualScrollDragDropHelper : IDisposable
{
    private readonly ItemsRepeater _itemsRepeater;
    private readonly IVirtualScroll _virtualScroll;
    private readonly IVirtualScrollFlattenedAdapter? _flattenedAdapter;
    private bool _isDragging;
    private int _originalSectionIndex = -1;
    private int _originalItemIndex = -1;
    private int _currentFlattenedIndex = -1;
    private WeakReference<object?>? _draggingItem;
    private VirtualScrollElementContainer? _hiddenContainer;
    private int _checkedDestinationSectionIndex = -1;
    private int _checkedDestinationItemIndex = -1;
    private bool _checkedCanDrop;

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
        
        // Handle Drop and DragLeave at the ItemsRepeater level
        _itemsRepeater.AllowDrop = true;
        _itemsRepeater.Drop += OnItemsRepeaterDrop;
        _itemsRepeater.DragLeave += OnDragLeave;
    }

    private void OnElementPrepared(ItemsRepeater sender, ItemsRepeaterElementPreparedEventArgs args)
    {
        if (args.Element is not VirtualScrollElementContainer container)
        {
            return;
        }

        // IMPORTANT: Update container's FlattenedIndex from args.Index
        // After a move, ItemsRepeater may physically swap containers without calling UpdateItem,
        // so container.FlattenedIndex can become stale. args.Index is always correct.
        if (container.FlattenedIndex != args.Index)
        {
            System.Diagnostics.Debug.WriteLine($"[ElementPrepared] FIXING container.FlattenedIndex: was {container.FlattenedIndex}, should be {args.Index}");
            container.FlattenedIndex = args.Index;
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ElementPrepared] args.Index={args.Index}, container.FlattenedIndex={container.FlattenedIndex}");
        }

        // If we're dragging and this container is at the dragged item's position, update _hiddenContainer
        // This is crucial after scrolling when containers get recycled - _hiddenContainer might point to a stale container
        if (_isDragging && args.Index == _currentFlattenedIndex)
        {
            System.Diagnostics.Debug.WriteLine($"[ElementPrepared] Re-syncing _hiddenContainer: old={_hiddenContainer?.GetHashCode()}, new={container.GetHashCode()}, FlattenedIndex={args.Index}");
            SetHiddenContainer(container);
            // Invalidate cache since container reference changed (though position should be the same)
            _checkedDestinationSectionIndex = -1;
            _checkedDestinationItemIndex = -1;
        }

        // Only enable drag for items, not headers or footers
        if (container.FlattenedItem?.Type != VirtualScrollFlattenedPositionType.Item)
        {
            return;
        }

        // Enable drag and drop on the container
        container.CanDrag = true;
        container.AllowDrop = true;

        // Attach drag event handlers to containers
        container.DragStarting += OnContainerDragStarting;
        container.DragOver += OnContainerDragOver;
        container.DropCompleted += OnContainerDropCompleted;
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
        container.DropCompleted -= OnContainerDropCompleted;
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
        _isDragging = true;
        _originalSectionIndex = sectionIndex;
        _originalItemIndex = itemIndex;
        _currentFlattenedIndex = container.FlattenedIndex;
        _draggingItem = new WeakReference<object?>(item);
        
        // Reset CanDropItemAt cache for new drag operation
        _checkedDestinationSectionIndex = -1;
        _checkedDestinationItemIndex = -1;
        _checkedCanDrop = false;

        // Hide the dragged item to show a "drop space"
        SetHiddenContainer(container);

        // Call OnDragStarted
        _virtualScroll.DragHandler.OnDragStarted(dragInfo);

        // Set data package with item identifier
        e.Data.SetData("VirtualScrollItem", container.FlattenedIndex);
        e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private void OnContainerDragOver(object sender, DragEventArgs e)
    {
        if (sender is not VirtualScrollElementContainer container)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            return;
        }

        if (_flattenedAdapter is null || 
            _virtualScroll.DragHandler is null || 
            _virtualScroll.Adapter is null ||
            !_isDragging)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            return;
        }

        // Skip if this is the hidden container (the one being dragged)
        if (ReferenceEquals(container, _hiddenContainer))
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

        // Get current source position from tracked flattened index
        if (!_flattenedAdapter.TryGetSectionAndItemIndex(_currentFlattenedIndex, out var currentSectionIndex, out var currentItemIndex))
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            return;
        }

        // DEBUG LOGGING
        System.Diagnostics.Debug.WriteLine($"[DragOver] target.FlattenedIndex={container.FlattenedIndex}, currentFlattenedIndex={_currentFlattenedIndex}");
        System.Diagnostics.Debug.WriteLine($"[DragOver] current=({currentSectionIndex},{currentItemIndex}), destination=({destinationSectionIndex},{destinationItemIndex})");

        // Early return if position hasn't changed - no need to check CanDropItemAt
        if (currentSectionIndex == destinationSectionIndex && currentItemIndex == destinationItemIndex)
        {
            System.Diagnostics.Debug.WriteLine($"[DragOver] NO MOVE - same position");
            e.DragUIOverride.IsGlyphVisible = false;
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
            e.Handled = true;
            return;
        }

        // Cache CanDropItemAt result to avoid calling it on every pixel move
        // Only re-check if the destination has changed
        bool canDrop;
        if (_checkedDestinationSectionIndex == destinationSectionIndex && 
            _checkedDestinationItemIndex == destinationItemIndex)
        {
            // Use cached result
            canDrop = _checkedCanDrop;
        }
        else
        {
            // Destination changed, check again and cache the result
            var dragMoveInfo = new VirtualScrollDragDropInfo(
                GetItemWithCache(_virtualScroll.Adapter, currentSectionIndex, currentItemIndex),
                _originalSectionIndex,
                _originalItemIndex,
                currentSectionIndex,
                currentItemIndex,
                destinationSectionIndex,
                destinationItemIndex
            );

            canDrop = _virtualScroll.DragHandler.CanDropItemAt(dragMoveInfo);
            _checkedDestinationSectionIndex = destinationSectionIndex;
            _checkedDestinationItemIndex = destinationItemIndex;
            _checkedCanDrop = canDrop;
        }

        if (!canDrop)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            e.Handled = true;
            return;
        }

        e.DragUIOverride.IsGlyphVisible = false;

        e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

        // Perform the move immediately (like Android's OnMove) for real-time reordering UX
        {
            System.Diagnostics.Debug.WriteLine($"[DragOver] MOVING from ({currentSectionIndex},{currentItemIndex}) to ({destinationSectionIndex},{destinationItemIndex})");

            var moveInfo = new VirtualScrollDragMoveInfo(
                GetItemWithCache(_virtualScroll.Adapter, currentSectionIndex, currentItemIndex),
                currentSectionIndex,
                currentItemIndex,
                destinationSectionIndex,
                destinationItemIndex
            );

            _virtualScroll.DragHandler.MoveItem(moveInfo);

            // Notify the adapter of the move
            _flattenedAdapter.OnAdapterChanged(
                new VirtualScrollChangeSet(
                    [
                        new VirtualScrollChange(
                            VirtualScrollChangeOperation.MoveItem,
                            currentSectionIndex,
                            currentItemIndex,
                            destinationSectionIndex,
                            destinationItemIndex
                        )
                    ]
                )
            );

            // Update tracked current flattened index
            var destinationFlattenedIndex = _flattenedAdapter.GetFlattenedIndexForItem(destinationSectionIndex, destinationItemIndex);
            _currentFlattenedIndex = destinationFlattenedIndex;

            // Update both containers' FlattenedIndex to reflect their new positions after the move
            // The dragged item (_hiddenContainer) is now at the destination position
            // The target container (container) is now at the source position
            if (_hiddenContainer is not null)
            {
                var sourceFlattenedIndex = _flattenedAdapter.GetFlattenedIndexForItem(currentSectionIndex, currentItemIndex);
                
                _hiddenContainer.FlattenedIndex = destinationFlattenedIndex;
                container.FlattenedIndex = sourceFlattenedIndex;
                
                System.Diagnostics.Debug.WriteLine($"[DragOver] Updated _hiddenContainer.FlattenedIndex to {destinationFlattenedIndex} (destination: {destinationSectionIndex},{destinationItemIndex})");
                System.Diagnostics.Debug.WriteLine($"[DragOver] Updated target container.FlattenedIndex to {sourceFlattenedIndex} (source: {currentSectionIndex},{currentItemIndex})");
            }

            // Invalidate cache after move since current position changed
            _checkedDestinationSectionIndex = -1;
            _checkedDestinationItemIndex = -1;

            System.Diagnostics.Debug.WriteLine($"[DragOver] After move: _currentFlattenedIndex={_currentFlattenedIndex}");
        }
        
        // Mark as handled to prevent bubbling
        e.Handled = true;
    }

    private void OnDragLeave(object sender, DragEventArgs e)
    {
        // If drag leaves the ItemsRepeater entirely and we have an active drag, clean up
        // This handles the case where user cancels drag (e.g., presses Escape or drags outside)
        if (_isDragging)
        {
            var point = e.GetPosition(_itemsRepeater);
            var rect = new Windows.Foundation.Rect(0, 0, _itemsRepeater.ActualWidth, _itemsRepeater.ActualHeight);
            
            System.Diagnostics.Debug.WriteLine($"[DragLeave] point={point}, rect={rect}, contains={rect.Contains(point)}");
            
            // If the point is outside the ItemsRepeater bounds, clean up
            if (!rect.Contains(point))
            {
                System.Diagnostics.Debug.WriteLine($"[DragLeave] Outside bounds - calling CleanupDragState");
                CleanupDragState();
            }
        }
    }

    private void OnItemsRepeaterDrop(object sender, DragEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[Drop] _isDragging={_isDragging}, _hiddenContainer={_hiddenContainer?.GetHashCode()}");
        
        // The actual move already happened during DragOver (like Android's OnMove)
        // Drop just finalizes and cleans up the drag operation
        CleanupDragState();
        
        // Mark as handled to prevent bubbling
        e.Handled = true;
    }

    private void OnContainerDropCompleted(UIElement sender, DropCompletedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[DropCompleted] DropResult={e.DropResult}, _isDragging={_isDragging}, _hiddenContainer={_hiddenContainer?.GetHashCode()}");
        
        // This fires on the SOURCE element when drag ends (drop or cancel)
        // This is more reliable than waiting for Drop event on target
        CleanupDragState();
    }

    private void CleanupDragState()
    {
        System.Diagnostics.Debug.WriteLine($"[CleanupDragState] _isDragging={_isDragging}, _hiddenContainer={_hiddenContainer?.GetHashCode()}");
        
        // Early return if already cleaned (can be called from multiple events)
        if (!_isDragging)
        {
            // Still ensure hidden container is restored even if not dragging
            SetHiddenContainer(null);
            return;
        }
        
        if (_virtualScroll.DragHandler is not null && 
            _flattenedAdapter is not null &&
            _virtualScroll.Adapter is not null &&
            _currentFlattenedIndex >= 0 &&
            _flattenedAdapter.TryGetSectionAndItemIndex(_currentFlattenedIndex, out var finalSectionIndex, out var finalItemIndex))
        {
            var dragInfo = new VirtualScrollDragInfo(
                GetItemWithCache(_virtualScroll.Adapter, finalSectionIndex, finalItemIndex), 
                finalSectionIndex, 
                finalItemIndex);
            _virtualScroll.DragHandler.OnDragEnded(dragInfo);
        }

        // Restore visibility of the hidden container
        SetHiddenContainer(null);

        _isDragging = false;
        _originalSectionIndex = -1;
        _originalItemIndex = -1;
        _currentFlattenedIndex = -1;
        _draggingItem = null;
        _checkedDestinationSectionIndex = -1;
        _checkedDestinationItemIndex = -1;
        _checkedCanDrop = false;
    }

    private void SetHiddenContainer(VirtualScrollElementContainer? container)
    {
        System.Diagnostics.Debug.WriteLine($"[SetHiddenContainer] old={_hiddenContainer?.GetHashCode()}, new={container?.GetHashCode()}");
        
        // Restore the previously hidden container
        if (_hiddenContainer is not null)
        {
            System.Diagnostics.Debug.WriteLine($"[SetHiddenContainer] Restoring opacity to 1 on {_hiddenContainer.GetHashCode()}");
            _hiddenContainer.Opacity = 1;
            _hiddenContainer.IsHitTestVisible = true;
        }

        // Hide the new container (if any)
        _hiddenContainer = container;
        if (_hiddenContainer is not null)
        {
            System.Diagnostics.Debug.WriteLine($"[SetHiddenContainer] Setting opacity to 0 on {_hiddenContainer.GetHashCode()}");
            _hiddenContainer.Opacity = 0;
            // Disable hit testing so drag events can pass through to containers behind
            _hiddenContainer.IsHitTestVisible = false;
        }
    }

    private object? GetItemWithCache(IVirtualScrollAdapter adapter, int sourceSectionIndex, int sourceItemIndex)
        => _draggingItem?.TryGetTarget(out var cachedItem) is true ? cachedItem : adapter.GetItem(sourceSectionIndex, sourceItemIndex);

    public void Dispose()
    {
        if (_itemsRepeater is not null)
        {
            _itemsRepeater.ElementPrepared -= OnElementPrepared;
            _itemsRepeater.ElementClearing -= OnElementClearing;
            _itemsRepeater.Drop -= OnItemsRepeaterDrop;
            _itemsRepeater.DragLeave -= OnDragLeave;
        }

        SetHiddenContainer(null);
        _isDragging = false;
        _draggingItem = null;
    }
}
