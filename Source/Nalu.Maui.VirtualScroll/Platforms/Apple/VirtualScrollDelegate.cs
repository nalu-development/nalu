using UIKit;

namespace Nalu;

/// <summary>
/// Delegate for handling scroll events in VirtualScroll on iOS/Mac Catalyst.
/// </summary>
internal class VirtualScrollDelegate : UICollectionViewDelegate
{
    private IVirtualScrollController? _controller;
    private bool _scrollEventsEnabled;
    private readonly FadingEdgeController _fadingEdgeController = new();
    private ItemsLayoutOrientation _orientation;
    private double _fadingEdgeLength;

    public VirtualScrollDelegate(IVirtualScrollController controller, UICollectionViewScrollDirection scrollDirection, double fadingEdgeLength)
    {
        _controller = controller;
        
        // Cache initial orientation from scroll direction
        _orientation = scrollDirection == UICollectionViewScrollDirection.Vertical
            ? ItemsLayoutOrientation.Vertical
            : ItemsLayoutOrientation.Horizontal;
        
        // Cache initial fading edge length
        _fadingEdgeLength = fadingEdgeLength;
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
        }
        base.Dispose(disposing);
    }
}

