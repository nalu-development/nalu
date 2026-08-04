package com.nalu.maui.virtualscroll;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

/**
 * Adapter base caching the item count in Java: RecyclerView internals read
 * {@code getItemCount()} constantly (several times per layout pass, per scroll tick, per
 * prefetch decision), and a managed override would pay a JNI transition on every read.
 *
 * <p>The managed side pushes the count through {@link #updateItemCount(int)} whenever the
 * data changes — one boundary crossing per changeset instead of one per read. The getter is
 * final so the count can never be shadowed back into managed code.</p>
 */
public abstract class VirtualScrollNativeAdapter extends RecyclerView.Adapter<RecyclerView.ViewHolder> {

    private int itemCount;

    @Override
    public final int getItemCount() {
        return itemCount;
    }

    /**
     * Sets the value returned by {@link #getItemCount()}. Must be called on the main thread,
     * before the corresponding notify* calls reach the RecyclerView (the count and the
     * notifications must be consistent by the next layout pass).
     */
    public final void updateItemCount(int value) {
        itemCount = value;
    }
}
