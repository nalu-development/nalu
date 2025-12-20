namespace Nalu;

/// <summary>
/// Controller interface for VirtualScroll refresh and scroll functionality.
/// </summary>
public interface IVirtualScrollController
{
    /// <summary>
    /// Refreshes the content. Called by the platform when the user triggers a refresh.
    /// </summary>
    /// <param name="completionCallback">Callback to invoke when the refresh is complete.</param>
    void Refresh(Action completionCallback);
    
    /// <summary>
    /// Called when the scroll position changes. Invoked by the handler when scrolling occurs.
    /// </summary>
    /// <param name="scrollX">The current horizontal scroll position in device-independent units.</param>
    /// <param name="scrollY">The current vertical scroll position in device-independent units.</param>
    /// <param name="totalScrollableWidth">The total scrollable width in device-independent units.</param>
    /// <param name="totalScrollableHeight">The total scrollable height in device-independent units.</param>
    void Scrolled(double scrollX, double scrollY, double totalScrollableWidth, double totalScrollableHeight);
}

