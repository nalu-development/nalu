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

    // Fading edges are positioned at the PADDED bounds by View.draw(), but our safe-area
    // insets are padding with clipToPadding=false — content scrolls under the padding all the
    // way to the physical edge, so the fades must sit there too. The padding-offset hooks
    // extend the fade (and its saveLayer) bounds back to the view's real edges.

    protected override bool IsPaddingOffsetRequired => true;

    protected override int LeftPaddingOffset => -PaddingLeft;

    protected override int TopPaddingOffset => -PaddingTop;

    protected override int RightPaddingOffset => PaddingRight;

    protected override int BottomPaddingOffset => PaddingBottom;

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
    
    WindowInsetsCompat AndroidX.Core.View.IOnApplyWindowInsetsListener.OnApplyWindowInsets(AView? view, WindowInsetsCompat? insets)
    {
        ArgumentNullException.ThrowIfNull(insets);
        var size = insets.GetInsets(_allInsetsType) ?? _zeroInsets;
        if (PaddingBottom != size.Bottom || PaddingLeft != size.Left || PaddingRight != size.Right || PaddingTop != size.Top)
        {
            SetPadding(size.Left, size.Top, size.Right, size.Bottom);
            RequestLayout();
        }

        using var builder = new WindowInsetsCompat.Builder(insets);
        return builder.SetInsets(_allInsetsType, _zeroInsets)!.Build()!;
    }
}
