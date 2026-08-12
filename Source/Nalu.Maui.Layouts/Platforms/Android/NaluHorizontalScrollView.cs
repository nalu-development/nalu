using Android.Content;
using Android.Runtime;
using Android.Views;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;

namespace Nalu;

/// <summary>
/// The horizontal ScrollBox scroller: an <see cref="Android.Widget.HorizontalScrollView" /> with
/// the same insets-isolation and fading-edge overrides as <see cref="NaluNestedScrollView" />.
/// </summary>
/// <remarks>
/// The framework HorizontalScrollView participates in nested scrolling as a CHILD since API 21
/// (through the View-level nested-scroll dispatch), which is what SwipeRefreshLayout and
/// scroll-observing ancestors rely on.
/// </remarks>
internal sealed class NaluHorizontalScrollView : Android.Widget.HorizontalScrollView, IScrollBoxScroller
{
    private readonly ScrollBoxInsetsController _insets;

    /// <summary>Drops the frames of a fling superseded by a programmatic jump (see StopAndJumpToPx).</summary>
    private bool _suppressAnimatedScrollFrames;

    /// <summary>
    /// Activation constructor — see <see cref="NaluNestedScrollView(IntPtr, JniHandleOwnership)" />.
    /// </summary>
    public NaluHorizontalScrollView(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
        _insets = new ScrollBoxInsetsController(this);
    }

    public NaluHorizontalScrollView(Context context)
        : base(context)
    {
        _insets = new ScrollBoxInsetsController(this);
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

    /// <summary>See <see cref="NaluNestedScrollView.RequestFitSystemWindows" />.</summary>
#pragma warning disable CS0672
    public override void RequestFitSystemWindows()
    {
        // Deliberately empty: content must not trigger a whole-window insets dispatch.
    }
#pragma warning restore CS0672

    /// <summary>See <see cref="NaluNestedScrollView.DispatchApplyWindowInsets" />.</summary>
    public override WindowInsets? DispatchApplyWindowInsets(WindowInsets? insets)
    {
        if (insets is not null)
        {
            _insets.OnDispatchApplyWindowInsets(insets);
        }

        return insets;
    }

    protected override bool IsPaddingOffsetRequired => true;

    protected override int LeftPaddingOffset => -PaddingLeft;

    protected override int TopPaddingOffset => -PaddingTop;

    protected override int RightPaddingOffset => PaddingRight;

    protected override int BottomPaddingOffset => PaddingBottom;
}
