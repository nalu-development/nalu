package com.nalu.maui.virtualscroll;

import android.content.Context;
import android.util.AttributeSet;
import android.view.View;
import android.view.ViewParent;
import android.view.ViewTreeObserver;
import android.view.inputmethod.InputMethodManager;
import android.widget.AbsListView;

import androidx.annotation.NonNull;
import androidx.annotation.Nullable;
import androidx.core.graphics.Insets;
import androidx.core.view.OnApplyWindowInsetsListener;
import androidx.core.view.WindowInsetsCompat;
import androidx.recyclerview.widget.RecyclerView;

/**
 * Native base of the VirtualScroll RecyclerView hosting logic that sits on rendering,
 * layout or recycling hot paths: keeping it in Java means the framework's per-frame and
 * per-child callbacks never cross the JNI boundary into managed code.
 *
 * <p>The managed {@code VirtualScrollRecyclerView} derives from this class and keeps the
 * MAUI integration (scroll adjustment, adapter wiring) in C#.</p>
 */
public abstract class VirtualScrollNativeRecyclerView extends RecyclerView
        implements ViewTreeObserver.OnGlobalFocusChangeListener, OnApplyWindowInsetsListener {

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

    // --- Positional safe-area self-padding (per-layout path) ---
    //
    // Self-padding emulates UIKit's POSITIONAL safe-area model (iOS gets this natively):
    // each inset band is applied only where it intersects the list's REST footprint — the
    // layout position with every ancestor scroll offset ignored. Rest coordinates keep the
    // padding STABLE while an ancestor scroll view scrolls the list under a system bar
    // (padding chased against the live position would relayout per frame and displace
    // cells), while a full-screen list (rest position at the window edges) keeps its
    // historical edge-to-edge padding, and a strip resting mid-page gets none.
    // The re-check runs on EVERY layout pass and walks the ancestor chain — Java-side so
    // neither the walk nor the per-ancestor reads cross the JNI boundary, and computed
    // into scratch fields so the steady-state pass (padding unchanged) allocates NOTHING.

    private static final int ALL_INSETS_TYPE = WindowInsetsCompat.Type.systemBars()
            | WindowInsetsCompat.Type.displayCutout()
            | WindowInsetsCompat.Type.navigationBars()
            | WindowInsetsCompat.Type.statusBars()
            | WindowInsetsCompat.Type.ime();

    @Nullable
    private Insets lastInsets;

    // Rest-intersection scratch (main-thread confined, valid until the next compute) and the
    // pre-bound apply runnable — no per-pass lambda allocation on the change path either.
    private int restLeft;
    private int restTop;
    private int restRight;
    private int restBottom;
    private final Runnable applySelfPaddingRunnable = this::applySelfPadding;

    @NonNull
    @Override
    public WindowInsetsCompat onApplyWindowInsets(@NonNull View view, @NonNull WindowInsetsCompat insets) {
        lastInsets = insets.getInsets(ALL_INSETS_TYPE);
        applySelfPadding();

        // Return value unused: dispatchApplyWindowInsets below invokes this directly and
        // never forwards anything to children.
        return insets;
    }

    // --- Insets isolation: the recycler is a hard window-insets BOUNDARY ---
    //
    // Cells never need window insets (the safe area belongs to this scroller via the
    // positional self-padding above — MAUI's own CollectionView exempts cell subtrees the
    // same way), yet MAUI attaches a managed insets listener to every layout of every cell,
    // and each recycle re-attach requests a WHOLE-window insets dispatch on its first
    // layout pass. During a fling that is O(cells) full-tree dispatches, each crossing JNI
    // once per MAUI view — profiled at ~24% of CPU time. Both overrides below keep all of
    // that out, entirely in Java.

    /**
     * Swallows {@code requestApplyInsets()} bubbles from cells. Deprecated for CALLERS
     * since API 20, but this is the {@link ViewParent} ABI channel the framework itself
     * still routes {@code View.requestApplyInsets()} through (verified through API 36) —
     * removing it would break every custom ViewGroup ever compiled against it.
     */
    @Override
    @SuppressWarnings("deprecation")
    public void requestFitSystemWindows() {
        // Deliberately empty: a cell (re-)attached by recycling must not trigger a
        // whole-window insets dispatch.
    }

    /**
     * Self-handling only: applies the positional self-padding and returns the insets
     * UNCONSUMED so later siblings keep receiving them — but never traverses into the
     * cells, so their managed listeners are never invoked.
     */
    @NonNull
    @Override
    public android.view.WindowInsets dispatchApplyWindowInsets(@NonNull android.view.WindowInsets insets) {
        onApplyWindowInsets(this, WindowInsetsCompat.toWindowInsetsCompat(insets, this));
        return insets;
    }

    @Override
    protected void onLayout(boolean changed, int l, int t, int r, int b) {
        super.onLayout(changed, l, t, r, b);

        // Re-evaluate the rest-position self-padding now that geometry is known (the
        // initial insets dispatch may pre-date layout); posted — padding cannot mutate
        // mid-pass.
        Insets insets = lastInsets;

        if (insets != null) {
            computeRestIntersection(insets);

            if (selfPaddingDiffers()) {
                post(applySelfPaddingRunnable);
            }
        }
    }

    private void applySelfPadding() {
        Insets insets = lastInsets;

        if (insets == null) {
            return;
        }

        // Recomputed here (not reused from onLayout): geometry may have changed between the
        // post and this run, and the insets dispatch calls in directly.
        computeRestIntersection(insets);

        if (selfPaddingDiffers()) {
            setPadding(restLeft, restTop, restRight, restBottom);
            requestLayout();
        }
    }

    private boolean selfPaddingDiffers() {
        return getPaddingBottom() != restBottom || getPaddingLeft() != restLeft
                || getPaddingRight() != restRight || getPaddingTop() != restTop;
    }

    /** Computes the rest-footprint/inset intersection into the scratch fields. */
    private void computeRestIntersection(@NonNull Insets size) {
        // Rest position: accumulate LAYOUT offsets up the chain, deliberately ignoring
        // every ancestor's scrollX/scrollY (scroll containers keep children's layout
        // coordinates).
        int left = getLeft();
        int top = getTop();
        View root = this;

        for (ViewParent parent = getParent(); parent instanceof View; parent = ((View) parent).getParent()) {
            View parentView = (View) parent;

            // Inside a RECYCLING container the layout position itself is arbitrary
            // (items are re-laid-out as they scroll): never self-pad there.
            if (parentView instanceof RecyclerView || parentView instanceof AbsListView) {
                restLeft = 0;
                restTop = 0;
                restRight = 0;
                restBottom = 0;

                return;
            }

            left += parentView.getLeft();
            top += parentView.getTop();
            root = parentView;
        }

        if (root.getWidth() <= 0 || root.getHeight() <= 0 || getWidth() <= 0 || getHeight() <= 0) {
            // Pre-layout dispatch: geometry unknown — keep the historical full padding;
            // onLayout re-applies with the real rest position right after.
            restLeft = size.left;
            restTop = size.top;
            restRight = size.right;
            restBottom = size.bottom;

            return;
        }

        int right = left + getWidth();
        int bottom = top + getHeight();

        restLeft = Math.max(0, size.left - left);
        restTop = Math.max(0, size.top - top);
        restRight = Math.max(0, right - (root.getWidth() - size.right));
        restBottom = Math.max(0, bottom - (root.getHeight() - size.bottom));
    }
}
