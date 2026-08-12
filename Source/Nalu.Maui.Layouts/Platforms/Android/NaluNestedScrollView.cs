using Android.Content;
using Android.Runtime;
using Android.Views;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;

namespace Nalu;

/// <summary>
/// The vertical ScrollBox scroller: an <see cref="AndroidX.Core.Widget.NestedScrollView" /> with
/// the VirtualScroll insets-isolation and fading-edge techniques applied as managed overrides.
/// </summary>
internal sealed class NaluNestedScrollView : AndroidX.Core.Widget.NestedScrollView, IScrollBoxScroller
{
    private readonly ScrollBoxInsetsController _insets;

    /// <summary>Drops the frames of a fling superseded by a programmatic jump (see StopAndJumpToPx).</summary>
    private bool _suppressAnimatedScrollFrames;

    /// <summary>
    /// Activation constructor: Java.Interop calls it when it must materialize the managed peer
    /// from an EXISTING native handle (e.g. while resolving <c>this</c> inside a framework
    /// layout callback). Without it the runtime throws "Unable to activate instance of type …
    /// from native handle" mid-layout.
    /// </summary>
    public NaluNestedScrollView(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
        _insets = new ScrollBoxInsetsController(this);
    }

    public NaluNestedScrollView(Context context)
        : base(context)
    {
        _insets = new ScrollBoxInsetsController(this);

        // Safe-area insets are applied as self-padding: content must scroll under them all the
        // way to the physical edge.
        SetClipToPadding(false);
        SetClipChildren(true);
    }

    public AView View => this;

    public AViewGroup ViewGroup => this;

    public bool ScrollGesturesEnabled { get; set; } = true;

    public bool IsUserInteracting { get; private set; }

    public Action? LayoutCallback { get; set; }

    public Action? ScrollChangedCallback { get; set; }

    public void SmoothScrollToPx(int x, int y)
    {
        // An animated scroll is driven BY ComputeScroll: it must not be suppressed.
        _suppressAnimatedScrollFrames = false;
        SmoothScrollTo(x, y);
    }

    public void StopAndJumpToPx(int x, int y)
    {
        // A programmatic jump WINS over any scrolling already in flight. Simply calling ScrollTo
        // is not enough: a fling keeps driving the offset from ComputeScroll on later frames and
        // would drag the content back to where the fling was heading. There is no public API to
        // abort the internal scroller, so the arbitration lives here — this subclass drops the
        // fling's remaining frames until the user touches again (or we start an animated scroll,
        // which needs ComputeScroll to drive it).
        _suppressAnimatedScrollFrames = true;
        ScrollTo(x, y);
    }

    public override void ComputeScroll()
    {
        if (_suppressAnimatedScrollFrames)
        {
            return;
        }

        base.ComputeScroll();
    }

    protected override void OnScrollChanged(int l, int t, int oldl, int oldt)
    {
        base.OnScrollChanged(l, t, oldl, oldt);
        ScrollChangedCallback?.Invoke();
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);
        _insets.OnLayout();
        LayoutCallback?.Invoke();
    }

    public override bool DispatchTouchEvent(MotionEvent? e)
    {
        switch (e?.ActionMasked)
        {
            case MotionEventActions.Down:
                IsUserInteracting = true;
                // The user is taking over: their gestures drive ComputeScroll again.
                _suppressAnimatedScrollFrames = false;

                break;
            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
                IsUserInteracting = false;

                break;
        }

        return base.DispatchTouchEvent(e);
    }

    public override bool OnInterceptTouchEvent(MotionEvent? ev) => ScrollGesturesEnabled && base.OnInterceptTouchEvent(ev);

    public override bool OnTouchEvent(MotionEvent? e) => ScrollGesturesEnabled && base.OnTouchEvent(e);

    // --- Insets isolation: the scroller is a hard window-insets BOUNDARY ---
    //
    // The content never needs window insets (the safe area belongs to this scroller via the
    // positional self-padding), yet MAUI attaches a managed insets listener to every layout in
    // the content, and each one re-dispatches full-tree insets passes. Both overrides below keep
    // all of that out. See VirtualScrollNativeRecyclerView for the original analysis.

    /// <summary>
    /// Swallows <c>requestApplyInsets()</c> bubbles from the content. Deprecated for CALLERS
    /// since API 20, but this is the ViewParent ABI channel the framework itself still routes
    /// <c>View.requestApplyInsets()</c> through (verified through API 36).
    /// </summary>
    // Deprecated for CALLERS since API 20, but the framework itself still routes
    // View.requestApplyInsets() through this ViewParent channel — overriding it is intentional.
#pragma warning disable CS0672
    public override void RequestFitSystemWindows()
    {
        // Deliberately empty: content must not trigger a whole-window insets dispatch.
    }
#pragma warning restore CS0672

    /// <summary>
    /// Self-handling only: applies the positional self-padding and returns the insets UNCONSUMED
    /// so later siblings keep receiving them — but never traverses into the content.
    /// </summary>
    public override WindowInsets? DispatchApplyWindowInsets(WindowInsets? insets)
    {
        if (insets is not null)
        {
            _insets.OnDispatchApplyWindowInsets(insets);
        }

        return insets;
    }

    // --- Fading edges vs safe-area padding ---
    //
    // View.draw() positions fading edges at the PADDED bounds, but safe-area insets are applied
    // as padding with clipToPadding=false — content scrolls under the padding all the way to the
    // physical edge, so the fades must sit there too. Only the fading-edge branch of View.draw()
    // consults these offsets.

    protected override bool IsPaddingOffsetRequired => true;

    protected override int LeftPaddingOffset => -PaddingLeft;

    protected override int TopPaddingOffset => -PaddingTop;

    protected override int RightPaddingOffset => PaddingRight;

    protected override int BottomPaddingOffset => PaddingBottom;
}
