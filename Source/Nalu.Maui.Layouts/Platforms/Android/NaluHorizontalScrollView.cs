using Android.Content;
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

    public void SmoothScrollToPx(int x, int y) => SmoothScrollTo(x, y);

    public void StopAndJumpToPx(int x, int y)
    {
        // A zero-velocity fling supersedes any in-flight fling animation (HorizontalScrollView
        // has no public stop API), then the jump lands on a resting scroller.
        Fling(0);
        ScrollTo(x, y);
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
