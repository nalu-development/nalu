using CoreGraphics;
using UIKit;

namespace Nalu;

/// <summary>
/// Container view for VirtualScrollCollectionView that detects bounds changes for fading edge updates.
/// </summary>
internal class VirtualScrollContainerView : UIView
{
    private CGRect _lastBounds = CGRect.Empty;

    /// <summary>
    /// Event raised when the bounds change.
    /// </summary>
    internal event EventHandler<EventArgs>? BoundsChanged;

    /// <inheritdoc/>
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        
        // Detect bounds changes to update fading edge
        if (!Bounds.Equals(_lastBounds))
        {
            var newBounds = Bounds;
            _lastBounds = newBounds;
            BoundsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}

