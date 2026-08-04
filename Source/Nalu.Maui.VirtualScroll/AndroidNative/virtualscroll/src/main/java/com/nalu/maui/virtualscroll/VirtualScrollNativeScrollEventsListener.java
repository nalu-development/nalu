package com.nalu.maui.virtualscroll;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

/**
 * Scroll-events listener computing offsets, ranges and the density conversion natively:
 * the managed consumer is invoked ONCE per frame with four ready device-independent values,
 * instead of crossing the boundary and then calling back into Java four more times for the
 * compute reads.
 *
 * <p>Attached only while the MAUI Scrolled event has subscribers; the abstract callback is
 * the single intentional JNI transition (the event's consumer is managed by definition).</p>
 */
public abstract class VirtualScrollNativeScrollEventsListener extends RecyclerView.OnScrollListener {

    @Override
    public final void onScrolled(@NonNull RecyclerView recyclerView, int dx, int dy) {
        float density = recyclerView.getResources().getDisplayMetrics().density;

        onScrolledDp(
                recyclerView.computeHorizontalScrollOffset() / density,
                recyclerView.computeVerticalScrollOffset() / density,
                recyclerView.computeHorizontalScrollRange() / density,
                recyclerView.computeVerticalScrollRange() / density);
    }

    /** Receives the scroll position and total scrollable range in device-independent units. */
    public abstract void onScrolledDp(double scrollX, double scrollY, double totalWidth, double totalHeight);
}
