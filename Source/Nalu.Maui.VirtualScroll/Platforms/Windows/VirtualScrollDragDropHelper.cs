using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

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

        // Enable drag and drop on ItemsRepeater
        _itemsRepeater.CanDragItems = true;
        _itemsRepeater.CanReorderItems = true;
        _itemsRepeater.DragItemsStarting += OnDragItemsStarting;
        _itemsRepeater.DragOver += OnDragOver;
        _itemsRepeater.Drop += OnDrop;
    }

    private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count == 0 || _flattenedAdapter is null || _virtualScroll.DragHandler is null || _virtualScroll.Adapter is null)
        {
            e.Cancel = true;
            return;
        }

        // Get the first item being dragged
        var flattenedIndex = (int)e.Items[0];
        if (!_flattenedAdapter.TryGetSectionAndItemIndex(flattenedIndex, out var sectionIndex, out var itemIndex))
        {
            e.Cancel = true;
            return;
        }

        var item = _virtualScroll.Adapter.GetItem(sectionIndex, itemIndex);
        var dragInfo = new VirtualScrollDragInfo(item, sectionIndex, itemIndex);

        _virtualScroll.DragHandler.OnDragInitiating(dragInfo);
        if (!_virtualScroll.DragHandler.CanDragItem(dragInfo))
        {
            e.Cancel = true;
            return;
        }

        _originalSectionIndex = sectionIndex;
        _originalItemIndex = itemIndex;
        _draggingItem = new WeakReference<object?>(item);
        _virtualScroll.DragHandler.OnDragStarted(dragInfo);

        // Store the container for later use
        _draggingContainer = _itemsRepeater.TryGetElement(flattenedIndex) as VirtualScrollElementContainer;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (_flattenedAdapter is null || _virtualScroll.DragHandler is null || _virtualScroll.Adapter is null)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.None;
            return;
        }

        // Find the element under the drag position
        var point = e.GetPosition(_itemsRepeater);
        var element = VisualTreeHelper.FindElementsInHostCoordinates(point, _itemsRepeater)
            .OfType<VirtualScrollElementContainer>()
            .FirstOrDefault();

        if (element is null || !_flattenedAdapter.TryGetSectionAndItemIndex(element.FlattenedIndex, out var destinationSectionIndex, out var destinationItemIndex))
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
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (_flattenedAdapter is null || _virtualScroll.DragHandler is null || _virtualScroll.Adapter is null)
        {
            return;
        }

        // Find the element under the drop position
        var point = e.GetPosition(_itemsRepeater);
        var element = VisualTreeHelper.FindElementsInHostCoordinates(point, _itemsRepeater)
            .OfType<VirtualScrollElementContainer>()
            .FirstOrDefault();

        if (element is null || !_flattenedAdapter.TryGetSectionAndItemIndex(element.FlattenedIndex, out var destinationSectionIndex, out var destinationItemIndex))
        {
            return;
        }

        // Get current source position
        if (_draggingContainer is null || !_flattenedAdapter.TryGetSectionAndItemIndex(_draggingContainer.FlattenedIndex, out var sourceSectionIndex, out var sourceItemIndex))
        {
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

        // Clean up
        if (_virtualScroll.DragHandler is not null && _draggingContainer is not null && _flattenedAdapter.TryGetSectionAndItemIndex(_draggingContainer.FlattenedIndex, out var finalSectionIndex, out var finalItemIndex))
        {
            var dragInfo = new VirtualScrollDragInfo(GetItemWithCache(_virtualScroll.Adapter, finalSectionIndex, finalItemIndex), finalSectionIndex, finalItemIndex);
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
            _itemsRepeater.CanDragItems = false;
            _itemsRepeater.CanReorderItems = false;
            _itemsRepeater.DragItemsStarting -= OnDragItemsStarting;
            _itemsRepeater.DragOver -= OnDragOver;
            _itemsRepeater.Drop -= OnDrop;
        }

        _draggingContainer = null;
        _draggingItem = null;
    }
}
