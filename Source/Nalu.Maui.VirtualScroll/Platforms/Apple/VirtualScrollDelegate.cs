using UIKit;

namespace Nalu;

/// <summary>
/// Delegate for handling scroll events in VirtualScroll on iOS/Mac Catalyst.
/// </summary>
internal class VirtualScrollDelegate : UICollectionViewDelegate
{
    private IVirtualScrollController? _controller;
    private bool _scrollEventsEnabled;

    public VirtualScrollDelegate(IVirtualScrollController controller)
    {
        _controller = controller;
    }

    /// <summary>
    /// Enables or disables scroll event notifications.
    /// </summary>
    public void SetScrollEventsEnabled(bool enabled) => _scrollEventsEnabled = enabled;

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

