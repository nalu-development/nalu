package com.nalu.maui.virtualscroll;

import androidx.annotation.NonNull;
import androidx.recyclerview.widget.RecyclerView;

/**
 * Maintains the scroll-delta state behind ItemsUpdatingScrollMode.KeepScrollOffset: after an
 * item insertion shifts the scroll offset, the next scroll pass undoes that shift. Once armed
 * the listener runs on EVERY scroll frame — pure Java so the per-frame callback (and its two
 * computeScrollOffset reads) never cross the JNI boundary.
 *
 * <p>Managed code only arms it via {@link #undoNextScrollAdjustment()} (which lazily attaches
 * the listener) and detaches it on dispose.</p>
 */
public final class VirtualScrollNativeOffsetTracker extends RecyclerView.OnScrollListener {

    private final RecyclerView recyclerView;
    private boolean attached;
    private boolean undoNextScrollAdjustment;
    private int lastScrollX;
    private int lastScrollY;

    public VirtualScrollNativeOffsetTracker(@NonNull RecyclerView recyclerView) {
        this.recyclerView = recyclerView;
    }

    /** Arms the one-shot undo and snapshots the current offsets; attaches lazily. */
    public void undoNextScrollAdjustment() {
        if (!attached) {
            attached = true;
            recyclerView.addOnScrollListener(this);
        }

        undoNextScrollAdjustment = true;
        lastScrollX = recyclerView.computeHorizontalScrollOffset();
        lastScrollY = recyclerView.computeVerticalScrollOffset();
    }

    /** Detaches the scroll listener (idempotent). */
    public void detach() {
        if (attached) {
            attached = false;
            recyclerView.removeOnScrollListener(this);
        }
    }

    @Override
    public void onScrolled(@NonNull RecyclerView view, int dx, int dy) {
        int newScrollX = recyclerView.computeHorizontalScrollOffset();
        int newScrollY = recyclerView.computeVerticalScrollOffset();

        int deltaX = Math.max(newScrollX - lastScrollX, 0);
        int deltaY = Math.max(newScrollY - lastScrollY, 0);

        lastScrollX = newScrollX;
        lastScrollY = newScrollY;

        if (undoNextScrollAdjustment) {
            // This last scroll adjustment happened because a new item was added and shifted
            // the offset; KeepScrollOffset means we undo that shift and stay where we were.
            undoNextScrollAdjustment = false;
            recyclerView.scrollBy(-deltaX, -deltaY);
        }
    }
}
