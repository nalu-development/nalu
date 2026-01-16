using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Platform;

namespace Nalu;

/// <summary>
/// Listener for RecyclerView scroll state changes (for detecting when scrolling starts and ends).
/// </summary>
internal class VirtualScrollRecyclerViewScrollStateListener : RecyclerView.OnScrollListener
{
    private readonly Action<RecyclerView, double, double, double, double> _scrollStartedHandler;
    private readonly Action<RecyclerView, double, double, double, double> _scrollEndedHandler;

    public VirtualScrollRecyclerViewScrollStateListener(
        Action<RecyclerView, double, double, double, double> scrollStartedHandler,
        Action<RecyclerView, double, double, double, double> scrollEndedHandler)
    {
        _scrollStartedHandler = scrollStartedHandler;
        _scrollEndedHandler = scrollEndedHandler;
    }

    public override void OnScrollStateChanged(RecyclerView recyclerView, int newState)
    {
        base.OnScrollStateChanged(recyclerView, newState);

        if (newState is RecyclerView.ScrollStateSettling || recyclerView.Context is not { } context)
        {
            return;
        }

        // Get absolute scroll positions in pixels
        var scrollX = recyclerView.ComputeHorizontalScrollOffset();
        var scrollY = recyclerView.ComputeVerticalScrollOffset();
        
        // Get total scrollable range in pixels
        var totalWidth = recyclerView.ComputeHorizontalScrollRange();
        var totalHeight = recyclerView.ComputeVerticalScrollRange();
        
        // Convert from pixels to device-independent units
        var scrollXDp = context.FromPixels(scrollX);
        var scrollYDp = context.FromPixels(scrollY);
        var totalWidthDp = context.FromPixels(totalWidth);
        var totalHeightDp = context.FromPixels(totalHeight);
        
        if (newState == RecyclerView.ScrollStateDragging)
        {
            _scrollStartedHandler.Invoke(recyclerView, scrollXDp, scrollYDp, totalWidthDp, totalHeightDp);
        }
        else if (newState == RecyclerView.ScrollStateIdle)
        {
            _scrollEndedHandler.Invoke(recyclerView, scrollXDp, scrollYDp, totalWidthDp, totalHeightDp);
        }
    }
}
