namespace Nalu;

/// <summary>
/// Listener for RecyclerView scroll events in VirtualScroll on Android. The per-frame
/// computation (offsets, ranges, density conversion) happens in the Java base class —
/// this managed callback is the single boundary crossing per frame, receiving four ready
/// device-independent values.
/// </summary>
internal class VirtualScrollRecyclerViewScrollListener(Action<double, double, double, double> scrollHandler)
    : Platform.VirtualScrollNativeScrollEventsListener
{
    public override void OnScrolledDp(double scrollX, double scrollY, double totalWidth, double totalHeight)
        => scrollHandler(scrollX, scrollY, totalWidth, totalHeight);
}
