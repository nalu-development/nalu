package com.nalu.maui.virtualscroll;

import android.content.Context;
import android.util.AttributeSet;
import android.view.View;
import android.view.ViewParent;
import android.view.ViewTreeObserver;
import android.view.inputmethod.InputMethodManager;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.recyclerview.widget.RecyclerView;

/**
 * Native base of the VirtualScroll RecyclerView hosting logic that sits on rendering or
 * recycling hot paths: keeping it in Java means the framework's per-frame and per-child
 * callbacks never cross the JNI boundary into managed code.
 *
 * <p>The managed {@code VirtualScrollRecyclerView} derives from this class and keeps the
 * cold-path logic (window insets, scroll adjustment, MAUI integration) in C#.</p>
 */
public abstract class VirtualScrollNativeRecyclerView extends RecyclerView
        implements ViewTreeObserver.OnGlobalFocusChangeListener {

    /**
     * The direct child hosting the currently focused view (null when focus is elsewhere).
     * Distinct from {@link #getFocusedChild()}: tracked through the WINDOW's global focus
     * listener so it stays correct even when focus moves to a view outside this list.
     */
    @Nullable
    private View trackedFocusedChild;

    public VirtualScrollNativeRecyclerView(@NonNull Context context) {
        super(context);
        setClipToPadding(false);
        setClipChildren(true);
    }

    public VirtualScrollNativeRecyclerView(@NonNull Context context, @Nullable AttributeSet attrs) {
        super(context, attrs);
    }

    public VirtualScrollNativeRecyclerView(@NonNull Context context, @Nullable AttributeSet attrs, int defStyleAttr) {
        super(context, attrs, defStyleAttr);
    }

    // --- Fading edges vs safe-area padding (per-frame draw path) ---
    //
    // View.draw() positions fading edges at the PADDED bounds, but safe-area insets are
    // applied as padding with clipToPadding=false — content scrolls under the padding all
    // the way to the physical edge, so the fades must sit there too. The padding-offset
    // hooks extend the fade (and its saveLayer) bounds back to the view's real edges.
    // Only the fading-edge branch of View.draw() consults these.

    @Override
    protected boolean isPaddingOffsetRequired() {
        return true;
    }

    @Override
    protected int getLeftPaddingOffset() {
        return -getPaddingLeft();
    }

    @Override
    protected int getTopPaddingOffset() {
        return -getPaddingTop();
    }

    @Override
    protected int getRightPaddingOffset() {
        return getPaddingRight();
    }

    @Override
    protected int getBottomPaddingOffset() {
        return getPaddingBottom();
    }

    // --- Focus tracking + orphaned-IME handling (per-recycle detach path) ---
    //
    // A child recycled while it holds input focus leaves the soft keyboard ORPHANED:
    // Android never closes the IME on detach, and MAUI's HideSoftInputOnTapped watcher is
    // torn down together with the focus — the keyboard then cannot even be dismissed by
    // tapping the page. Close it deliberately and clear the focus so the MAUI focus state
    // stays coherent. onChildDetachedFromWindow fires for EVERY recycled child while
    // scrolling, which is why the check lives here rather than in managed code.
    //
    // Subscribed on this view's OWN window lifecycle — the observer obtained in
    // onAttachedToWindow is the live (merged) one and is still alive in
    // onDetachedFromWindow, so removal always targets the observer actually holding us.

    @Override
    protected void onAttachedToWindow() {
        super.onAttachedToWindow();
        getViewTreeObserver().addOnGlobalFocusChangeListener(this);
    }

    @Override
    protected void onDetachedFromWindow() {
        getViewTreeObserver().removeOnGlobalFocusChangeListener(this);
        trackedFocusedChild = null;
        super.onDetachedFromWindow();
    }

    @Override
    public void onGlobalFocusChanged(@Nullable View oldFocus, @Nullable View newFocus) {
        trackedFocusedChild = newFocus == null ? null : findDirectChildContaining(newFocus);
    }

    @Override
    public void onChildDetachedFromWindow(@NonNull View child) {
        super.onChildDetachedFromWindow(child);

        if (child == trackedFocusedChild) {
            trackedFocusedChild = null;

            InputMethodManager inputMethodManager =
                    (InputMethodManager) getContext().getSystemService(Context.INPUT_METHOD_SERVICE);

            if (inputMethodManager != null) {
                // This view's window token: the child's own is already null after detach.
                inputMethodManager.hideSoftInputFromWindow(getWindowToken(), 0);
            }

            View focused = child.findFocus();

            if (focused != null) {
                focused.clearFocus();
            }
        }
    }

    /**
     * Walks up from the focused view to our direct child hosting it (null when the focus
     * is not inside this list).
     */
    @Nullable
    private View findDirectChildContaining(@NonNull View view) {
        View current = view;

        for (ViewParent parent = current.getParent(); parent instanceof View; parent = current.getParent()) {
            if (parent == this) {
                return current;
            }

            current = (View) parent;
        }

        return null;
    }
}
