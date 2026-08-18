using UIKit;

namespace Nalu;

/// <summary>
/// Scroll view delegate translating UIKit scroll callbacks into ScrollBox semantics: per-frame
/// scroll notifications (lazily enabled), user scroll sessions (started/ended), programmatic
/// scroll completion and fading-edge updates.
/// </summary>
internal sealed class ScrollBoxScrollViewDelegate(ScrollBoxHandler handler) : UIScrollViewDelegate
{
    private readonly ScrollBoxFadingEdgeController _fadingEdgeController = new();
    private bool _scrollEventsEnabled;
    private double _fadingEdgeLength;
    private ScrollBoxOrientation _orientation;

    /// <summary>
    /// Enables or disables per-frame scroll event notifications.
    /// </summary>
    public void SetScrollEventsEnabled(bool enabled) => _scrollEventsEnabled = enabled;

    /// <summary>
    /// Updates the cached orientation.
    /// </summary>
    public void UpdateOrientation(ScrollBoxOrientation orientation) => _orientation = orientation;

    /// <summary>
    /// Updates the cached fading edge length and refreshes the visual fading edge.
    /// </summary>
    public void UpdateFadingEdgeLength(UIScrollView scrollView, double fadingEdgeLength)
    {
        _fadingEdgeLength = fadingEdgeLength;

        // Always update to handle both enabling (length > 0) and disabling (length == 0).
        _fadingEdgeController.Update(_fadingEdgeLength, _orientation, scrollView);
    }

    /// <summary>
    /// Updates the fading edge based on the current scroll view state (called from observers).
    /// </summary>
    public void UpdateFadingEdge(UIScrollView scrollView)
    {
        if (_fadingEdgeLength > 0)
        {
            _fadingEdgeController.Update(_fadingEdgeLength, _orientation, scrollView);
        }
    }

    /// <inheritdoc />
    public override void Scrolled(UIScrollView scrollView)
    {
        if (_scrollEventsEnabled)
        {
            handler.OnPlatformScrolled();
        }

        if (_fadingEdgeLength > 0)
        {
            _fadingEdgeController.Update(_fadingEdgeLength, _orientation, scrollView);
        }
    }

    /// <inheritdoc />
    public override void DraggingStarted(UIScrollView scrollView) => handler.OnPlatformScrollStarted();

    // The "flick" stop.
    /// <inheritdoc />
    public override void DecelerationEnded(UIScrollView scrollView) => handler.OnPlatformScrollEnded();

    // The "manual drag" stop.
    /// <inheritdoc />
    public override void DraggingEnded(UIScrollView scrollView, bool willDecelerate)
    {
        if (!willDecelerate)
        {
            handler.OnPlatformScrollEnded();
        }
    }

    // SetContentOffset(animated: true) completion.
    /// <inheritdoc />
    public override void ScrollAnimationEnded(UIScrollView scrollView) => handler.OnPlatformScrollAnimationEnded();
}
