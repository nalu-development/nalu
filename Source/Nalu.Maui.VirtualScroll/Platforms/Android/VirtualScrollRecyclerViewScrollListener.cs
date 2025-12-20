using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Platform;

namespace Nalu;

/// <summary>
/// Listener for RecyclerView scroll events in VirtualScroll on Android.
/// </summary>
internal class VirtualScrollRecyclerViewScrollListener : RecyclerView.OnScrollListener
{
    private readonly Action<RecyclerView, double, double, double, double> _scrollHandler;

    public VirtualScrollRecyclerViewScrollListener(Action<RecyclerView, double, double, double, double> scrollHandler)
    {
        _scrollHandler = scrollHandler;
    }

    public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
    {
        base.OnScrolled(recyclerView, dx, dy);
        
        // Get absolute scroll positions in pixels
        var scrollX = recyclerView.ComputeHorizontalScrollOffset();
        var scrollY = recyclerView.ComputeVerticalScrollOffset();
        
        // Get total scrollable range in pixels
        var totalWidth = recyclerView.ComputeHorizontalScrollRange();
        var totalHeight = recyclerView.ComputeVerticalScrollRange();
        
        // Convert from pixels to device-independent units
        var context = recyclerView.Context;
        if (context is not null)
        {
            var scrollXDp = context.FromPixels(scrollX);
            var scrollYDp = context.FromPixels(scrollY);
            var totalWidthDp = context.FromPixels(totalWidth);
            var totalHeightDp = context.FromPixels(totalHeight);
            
            _scrollHandler?.Invoke(recyclerView, scrollXDp, scrollYDp, totalWidthDp, totalHeightDp);
        }
    }
}
