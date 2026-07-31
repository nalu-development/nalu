using Android.Content;
using Android.Runtime;
using Android.Util;
using Android.Views;
using AndroidX.Core.View;
using AndroidX.RecyclerView.Widget;
using Google.Android.Material.Carousel;
using AView = Android.Views.View;

namespace Nalu;

public class VirtualScrollRecyclerView : RecyclerView, IOnApplyWindowInsetsListener, ViewTreeObserver.IOnGlobalFocusChangeListener
{
    private AView? _focusedChild;

    private VirtualScrollRecyclerViewScrollHelper? _scrollHelper;
    public ItemsLayoutOrientation Orientation { get; set; } = ItemsLayoutOrientation.Vertical;
    public Action? OnLayoutCallback { get; set; }
    internal VirtualScrollRecyclerViewScrollHelper ScrollHelper => _scrollHelper ??= new VirtualScrollRecyclerViewScrollHelper(this);

    public VirtualScrollRecyclerView(IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
    {
    }

    public VirtualScrollRecyclerView(Context context) : base(context)
    {
        ViewCompat.SetOnApplyWindowInsetsListener(this, this);
        SetClipToPadding(false);
        SetClipChildren(true);
    }

    public VirtualScrollRecyclerView(Context context, IAttributeSet? attrs) : base(context, attrs)
    {
    }

    public VirtualScrollRecyclerView(Context context, IAttributeSet? attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
    {
    }

    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        base.OnLayout(changed, l, t, r, b);
        // After a direct (non-animated) scroll operation, we may need to make adjustments
        // to align the target item; if an adjustment is pending, execute it here.
        // (Deliberately checking the private member here rather than the property accessor; the accessor will
        // create a new ScrollHelper if needed, and there's no reason to do that until a Scroll is requested.)
        _scrollHelper?.AdjustScroll();

        // Re-evaluate the rest-position self-padding now that geometry is known (the initial
        // insets dispatch may pre-date layout); posted — padding cannot mutate mid-pass.
        if (NeedsSelfPaddingUpdate())
        {
            Post(ApplySelfPadding);
        }
    }

    // Focus tracking: the window's global focus listener marks the direct child hosting the
    // focus, so the per-detach check on the recycling hot path is a pure managed reference
    // comparison (no JNI). Subscribed on the RecyclerView's OWN window lifecycle — the
    // observer obtained in OnAttachedToWindow is the live (merged) one and is still alive in
    // OnDetachedFromWindow, so removal always targets the observer actually holding us.

    protected override void OnAttachedToWindow()
    {
        base.OnAttachedToWindow();
        ViewTreeObserver?.AddOnGlobalFocusChangeListener(this);
    }

    protected override void OnDetachedFromWindow()
    {
        ViewTreeObserver?.RemoveOnGlobalFocusChangeListener(this);
        _focusedChild = null;
        base.OnDetachedFromWindow();
    }

    void ViewTreeObserver.IOnGlobalFocusChangeListener.OnGlobalFocusChanged(AView? oldFocus, AView? newFocus)
        => _focusedChild = newFocus is null ? null : FindDirectChildContaining(newFocus);

    /// <summary>
    /// JAVA identity, never managed reference equality: binding wrappers reaching us through
    /// callbacks may be fresh instances for the same Java object, and raw Handle equality is
    /// equally unreliable (distinct JNI refs to one object have distinct pointers) —
    /// <see cref="JNIEnv.IsSameObject"/> is the JNI-blessed check.
    /// </summary>
    private static bool IsSameJavaObject(AView? a, AView b)
        => a is not null && JNIEnv.IsSameObject(a.Handle, b.Handle);

    /// <summary>
    /// Walks up from the focused view to our direct child hosting it (null when the focus is
    /// not inside this list).
    /// </summary>
    private AView? FindDirectChildContaining(AView view)
    {
        for (AView current = view; current.Parent is AView parentView; current = parentView)
        {
            if (IsSameJavaObject(parentView, this))
            {
                return current;
            }
        }

        return null;
    }

    /// <summary>
    /// A child recycled while it holds input focus leaves the soft keyboard ORPHANED: Android
    /// never closes the IME on detach, and MAUI's HideSoftInputOnTapped watcher is torn down
    /// together with the focus — the keyboard then cannot even be dismissed by tapping the
    /// page. Close it deliberately and clear the focus so the MAUI focus state stays coherent.
    /// </summary>
    public override void OnChildDetachedFromWindow(AView child)
    {
        base.OnChildDetachedFromWindow(child);

        if (IsSameJavaObject(_focusedChild, child) && Context?.GetSystemService(Context.InputMethodService) is Android.Views.InputMethods.InputMethodManager inputMethodManager)
        {
            _focusedChild = null;

            // The RecyclerView's window token: the child's own is already null after detach.
            inputMethodManager.HideSoftInputFromWindow(WindowToken, Android.Views.InputMethods.HideSoftInputFlags.None);
            child.FindFocus()?.ClearFocus();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _scrollHelper?.Dispose();
            _scrollHelper = null;
            ViewCompat.SetOnApplyWindowInsetsListener(this, null);
        }

        base.Dispose(disposing);
    }

    private static readonly int _allInsetsType = WindowInsetsCompat.Type.SystemBars() |
                                                 WindowInsetsCompat.Type.DisplayCutout() |
                                                 WindowInsetsCompat.Type.NavigationBars() |
                                                 WindowInsetsCompat.Type.StatusBars() |
                                                 WindowInsetsCompat.Type.Ime();
    private static readonly AndroidX.Core.Graphics.Insets _zeroInsets = AndroidX.Core.Graphics.Insets.None!;

    private AndroidX.Core.Graphics.Insets? _lastInsets;

    WindowInsetsCompat AndroidX.Core.View.IOnApplyWindowInsetsListener.OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
    {
        ArgumentNullException.ThrowIfNull(insets);
        _lastInsets = insets.GetInsets(_allInsetsType) ?? _zeroInsets;
        ApplySelfPadding();

        // Insets are always consumed: cells must never see them.
        using var builder = new WindowInsetsCompat.Builder(insets);
        return builder.SetInsets(_allInsetsType, _zeroInsets)!.Build()!;
    }

    /// <summary>
    /// Self-padding emulates UIKit's POSITIONAL safe-area model (iOS gets this natively):
    /// each inset band is applied only where it intersects the list's REST footprint — the
    /// layout position with every ancestor scroll offset ignored. Rest coordinates keep the
    /// padding STABLE while an ancestor scroll view scrolls the list under a system bar
    /// (padding chased against the live position would relayout per frame and displace cells),
    /// while a full-screen list (rest position at the window edges) keeps its historical
    /// edge-to-edge padding, and a strip resting mid-page gets none.
    /// </summary>
    private void ApplySelfPadding()
    {
        if (_lastInsets is not { } insets)
        {
            return;
        }

        var size = ComputeRestIntersection(insets);

        if (PaddingBottom != size.Bottom || PaddingLeft != size.Left || PaddingRight != size.Right || PaddingTop != size.Top)
        {
            SetPadding(size.Left, size.Top, size.Right, size.Bottom);
            RequestLayout();
        }
    }

    private bool NeedsSelfPaddingUpdate()
    {
        if (_lastInsets is not { } insets)
        {
            return false;
        }

        var size = ComputeRestIntersection(insets);

        return PaddingBottom != size.Bottom || PaddingLeft != size.Left || PaddingRight != size.Right || PaddingTop != size.Top;
    }

    private AndroidX.Core.Graphics.Insets ComputeRestIntersection(AndroidX.Core.Graphics.Insets size)
    {
        // Rest position: accumulate LAYOUT offsets up the chain, deliberately ignoring every
        // ancestor's ScrollX/ScrollY (scroll containers keep children's layout coordinates).
        var left = Left;
        var top = Top;
        AView root = this;

        for (var parent = Parent; parent is AView parentView; parent = parentView.Parent)
        {
            // Inside a RECYCLING container the layout position itself is arbitrary
            // (items are re-laid-out as they scroll): never self-pad there.
            if (parentView is RecyclerView or Android.Widget.AbsListView)
            {
                return _zeroInsets;
            }

            left += parentView.Left;
            top += parentView.Top;
            root = parentView;
        }

        if (root.Width <= 0 || root.Height <= 0 || Width <= 0 || Height <= 0)
        {
            // Pre-layout dispatch: geometry unknown — keep the historical full padding;
            // OnLayout re-applies with the real rest position right after.
            return size;
        }

        var right = left + Width;
        var bottom = top + Height;

        return AndroidX.Core.Graphics.Insets.Of(
            Math.Max(0, size.Left - left),
            Math.Max(0, size.Top - top),
            Math.Max(0, right - (root.Width - size.Right)),
            Math.Max(0, bottom - (root.Height - size.Bottom))
        )!;
    }
}
