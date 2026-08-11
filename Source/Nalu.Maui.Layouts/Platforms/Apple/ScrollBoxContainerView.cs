using CoreGraphics;
using UIKit;

namespace Nalu;

/// <summary>
/// Container view for the ScrollBox scroll view: hosts the fading-edge gradient mask (a
/// <c>CAGradientLayer</c> mask must sit on the scroll view's superview) and detects bounds
/// changes to keep it updated.
/// </summary>
internal sealed class ScrollBoxContainerView : UIView
{
    private CGRect _lastBounds = CGRect.Empty;

    /// <summary>
    /// Event raised when the bounds change.
    /// </summary>
    internal event EventHandler<EventArgs>? BoundsChanged;

    /// <inheritdoc />
    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        if (!Bounds.Equals(_lastBounds))
        {
            _lastBounds = Bounds;
            BoundsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
