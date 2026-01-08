using Foundation;
using UIKit;

namespace Nalu;

/// <summary>
/// Delegate for handling scroll events in VirtualScroll on iOS/Mac Catalyst.
/// </summary>
internal class VirtualScrollDelegate : UICollectionViewDelegate
{
    private IVirtualScroll? _virtualScroll;
    private IVirtualScrollController? _controller;
    private bool _scrollEventsEnabled;
    private readonly FadingEdgeController _fadingEdgeController = new();
    private ItemsLayoutOrientation _orientation;
    private double _fadingEdgeLength;

    public VirtualScrollDelegate(IVirtualScroll virtualScroll, UICollectionViewScrollDirection scrollDirection, double fadingEdgeLength)
    {
        _virtualScroll = virtualScroll;
        _controller = virtualScroll as IVirtualScrollController ?? throw new InvalidOperationException("VirtualView must implement IVirtualScrollController.");
        
        // Cache initial orientation from scroll direction
        _orientation = scrollDirection == UICollectionViewScrollDirection.Vertical
            ? ItemsLayoutOrientation.Vertical
            : ItemsLayoutOrientation.Horizontal;
        
        // Cache initial fading edge length
        _fadingEdgeLength = fadingEdgeLength;
    }
    
    private object? _currentDragItem;
    private NSIndexPath? _currentDragIndexPath;
    private NSIndexPath? _checkedDestinationIndexPath;
    private NSIndexPath? _checkedFinalIndexPath;
    
    public void ItemDragStarted(NSIndexPath indexPath)
    {
        if (_virtualScroll?.DragHandler is { } dragHandler)
        {
            var sectionIndex = indexPath.Section;
            var itemIndex = indexPath.Item.ToInt32();
            _currentDragItem = _virtualScroll.Adapter?.GetItem(sectionIndex, itemIndex);
            _currentDragIndexPath = indexPath;
            dragHandler.OnDragStarted(new VirtualScrollDragInfo(_currentDragItem, sectionIndex, itemIndex));
        }
    }
    
#pragma warning disable IDE0060 // Remove unused parameter warning
    public void ItemDragMoved(NSIndexPath sourceIndexPath, NSIndexPath destinationIndexPath)
#pragma warning restore IDE0060
        => _currentDragIndexPath = destinationIndexPath;

    public void ItemDragCanceled()
    {
        if (_virtualScroll?.DragHandler is { } dragHandler && _currentDragItem is not null && _currentDragIndexPath is not null)
        {
            var sectionIndex = _currentDragIndexPath.Section;
            var itemIndex = _currentDragIndexPath.Item.ToInt32();
            dragHandler.OnDragCanceled(new VirtualScrollDragInfo(_currentDragItem, sectionIndex, itemIndex));
        }

        _currentDragItem = null;
        _currentDragIndexPath = null;
    }
    
    public void ItemDragEnded()
    {
        if (_virtualScroll?.DragHandler is { } dragHandler && _currentDragItem is not null && _currentDragIndexPath is not null)
        {
            var sectionIndex = _currentDragIndexPath.Section;
            var itemIndex = _currentDragIndexPath.Item.ToInt32();
            dragHandler.OnDragEnded(new VirtualScrollDragInfo(_currentDragItem, sectionIndex, itemIndex));
        }

        _currentDragItem = null;
        _currentDragIndexPath = null;
    }

    /// <inheritdoc/>
    public override NSIndexPath GetTargetIndexPathForMove(UICollectionView collectionView, NSIndexPath originalIndexPath, NSIndexPath proposedIndexPath)
    {
        if (proposedIndexPath == _checkedDestinationIndexPath && _checkedFinalIndexPath is not null)
        {
            return _checkedFinalIndexPath;
        }
        
        if (_virtualScroll?.DragHandler is { } dragHandler && _currentDragIndexPath is not null)
        {
            var originalSectionIndex = originalIndexPath.Section;
            var originalItemIndex = originalIndexPath.Item.ToInt32();
            var currentSectionIndex = _currentDragIndexPath.Section;
            var currentItemIndex = _currentDragIndexPath.Item.ToInt32();
            var destinationSectionIndex = proposedIndexPath.Section;
            var destinationItemIndex = proposedIndexPath.Item.ToInt32();
            var info = new VirtualScrollDragDropInfo(_currentDragItem, originalSectionIndex, originalItemIndex, currentSectionIndex, currentItemIndex, destinationSectionIndex, destinationItemIndex);
            var canDrop = dragHandler.CanDropItemAt(info);
            _checkedDestinationIndexPath = proposedIndexPath;
            _checkedFinalIndexPath = canDrop ? proposedIndexPath : _currentDragIndexPath;
            return _checkedFinalIndexPath;
        }

#pragma warning disable CA1422
        return base.GetTargetIndexPathForMove(collectionView, originalIndexPath, proposedIndexPath);
#pragma warning restore CA1422
    }

    public override NSIndexPath GetTargetIndexPathForMoveOfItemFromOriginalIndexPath(
        UICollectionView collectionView,
        NSIndexPath originalIndexPath,
        NSIndexPath currentIndexPath,
        NSIndexPath proposedIndexPath
    )
    {
        if (proposedIndexPath == _checkedDestinationIndexPath && _checkedFinalIndexPath is not null)
        {
            return _checkedFinalIndexPath;
        }

        if (_virtualScroll?.DragHandler is { } dragHandler)
        {
            var originalSectionIndex = originalIndexPath.Section;
            var originalItemIndex = originalIndexPath.Item.ToInt32();
            var currentSectionIndex = currentIndexPath.Section;
            var currentItemIndex = currentIndexPath.Item.ToInt32();
            var destinationSectionIndex = proposedIndexPath.Section;
            var destinationItemIndex = proposedIndexPath.Item.ToInt32();
            var info = new VirtualScrollDragDropInfo(_currentDragItem, originalSectionIndex, originalItemIndex, currentSectionIndex, currentItemIndex, destinationSectionIndex, destinationItemIndex);
            var canDrop = dragHandler.CanDropItemAt(info);
            _checkedDestinationIndexPath = proposedIndexPath;
            _checkedFinalIndexPath = canDrop ? proposedIndexPath : currentIndexPath;
            return _checkedFinalIndexPath;
        }
        
#pragma warning disable CA1416
        return base.GetTargetIndexPathForMoveOfItemFromOriginalIndexPath(collectionView, originalIndexPath, currentIndexPath, proposedIndexPath);
#pragma warning restore CA1416
    }

    /// <summary>
    /// Enables or disables scroll event notifications.
    /// </summary>
    public void SetScrollEventsEnabled(bool enabled) => _scrollEventsEnabled = enabled;

    /// <summary>
    /// Updates the cached orientation from the platform layout's scroll direction.
    /// </summary>
    public void UpdateOrientation(UICollectionViewScrollDirection scrollDirection) =>
        _orientation = scrollDirection == UICollectionViewScrollDirection.Vertical
            ? ItemsLayoutOrientation.Vertical
            : ItemsLayoutOrientation.Horizontal;

    /// <summary>
    /// Updates the cached fading edge length and updates the visual fading edge.
    /// </summary>
    public void UpdateFadingEdgeLength(UICollectionView collectionView, double fadingEdgeLength)
    {
        _fadingEdgeLength = fadingEdgeLength;
        // Always update to handle both enabling (length > 0) and disabling (length == 0) fading edge
        UpdateFadingEdgeInternal(collectionView);
    }

    /// <summary>
    /// Updates the fading edge based on the current scroll view state (called from observers).
    /// </summary>
    public void UpdateFadingEdge(UICollectionView collectionView)
    {
        if (_fadingEdgeLength > 0)
        {
            UpdateFadingEdgeInternal(collectionView);
        }
    }

    /// <summary>
    /// Updates the fading edge based on the current scroll view state.
    /// </summary>
    private void UpdateFadingEdgeInternal(UICollectionView collectionView) =>
        _fadingEdgeController.Update(_fadingEdgeLength, _orientation, collectionView);

    public override void Scrolled(UIScrollView scrollView)
    {
        if (_scrollEventsEnabled)
        {
            _controller?.Scrolled(
                scrollView.ContentOffset.X,
                scrollView.ContentOffset.Y,
                scrollView.ContentSize.Width,
                scrollView.ContentSize.Height);
        }
        
        // Update fading edge on scroll (only if fading edge is enabled)
        if (_fadingEdgeLength > 0 && scrollView is UICollectionView collectionView)
        {
            UpdateFadingEdgeInternal(collectionView);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _controller = null;
            _virtualScroll = null;
        }

        base.Dispose(disposing);
    }
}

