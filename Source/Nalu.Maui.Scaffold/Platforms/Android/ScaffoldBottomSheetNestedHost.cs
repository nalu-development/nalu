using Android.Content;
using Android.Widget;
using AndroidX.Core.View;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>
/// Android host for <see cref="ScaffoldBottomSheetView"/> providing the cooperative hand-off
/// between the sheet drag and an inner scrollable (NestedScrollView, RecyclerView, …):
/// scrolling up while the sheet is below its tallest detent EXPANDS the sheet first; an
/// unconsumed downward scroll (content already at its top) pulls the sheet down; release
/// settles to the nearest detent through the sheet's own logic.
/// </summary>
/// <remarks>
/// Nested-scroll dispatch walks the platform parent chain, so simply being an ancestor is
/// enough — the sheet's MAUI pan keeps covering handle/non-scrollable surfaces (on Android
/// the scrollable child consumes touches first, so the two never fight).
/// </remarks>
internal sealed class ScaffoldBottomSheetNestedHost : FrameLayout, INestedScrollingParent3, IScaffoldOverlayPanelHost
{
    /// <inheritdoc />
    public Action? ContentMeasureInvalidated { get; set; }

    /// <summary>A descendant's <c>requestLayout()</c> bubbles here natively — the presenter re-resolves the sheet's Content detent.</summary>
    public override void RequestLayout()
    {
        base.RequestLayout();
        ContentMeasureInvalidated?.Invoke();
    }

    private readonly ScaffoldBottomSheetView _sheet;
    private readonly NestedScrollingParentHelper _helper;
    private readonly float _density;
    private readonly int _touchSlop;
    private bool _sheetMoved;
    private bool _nestedActive;
    private bool _draggingSelf;
    private float _downY;
    private float _lastSelfY;

    public ScaffoldBottomSheetNestedHost(Context context, ScaffoldBottomSheetView sheet) : base(context)
    {
        _sheet = sheet;
        _helper = new NestedScrollingParentHelper(this);
        _density = context.Resources!.DisplayMetrics!.Density;
        _touchSlop = Android.Views.ViewConfiguration.Get(context)!.ScaledTouchSlop;
    }

    /// <summary>The sheet is placed above the keyboard by the presenter: its subtree never sees the IME (see <see cref="ScaffoldOverlayImeIsolation"/>).</summary>
    public override Android.Views.WindowInsets? DispatchApplyWindowInsets(Android.Views.WindowInsets? insets)
        => base.DispatchApplyWindowInsets(ScaffoldOverlayImeIsolation.StripIme(this, insets));

    // --- Raw-touch drag for NON-scrollable surfaces (handle, plain content) ---
    //
    // Scrollable children own their gestures through the nested-scroll session (started on
    // their DOWN); when no session exists, a vertical move past the slop claims the gesture
    // for the sheet. Taps stay untouched (below-slop moves are never intercepted).

    public override bool OnInterceptTouchEvent(Android.Views.MotionEvent? ev)
    {
        switch (ev?.ActionMasked)
        {
            case Android.Views.MotionEventActions.Down:
                _downY = ev.RawY;
                _draggingSelf = false;

                break;

            case Android.Views.MotionEventActions.Move when !_nestedActive && !_draggingSelf && Math.Abs(ev.RawY - _downY) > _touchSlop:
                Console.WriteLine("[SHEET] intercept-steal");
                _draggingSelf = true;
                _lastSelfY = ev.RawY;

                return true;
        }

        return base.OnInterceptTouchEvent(ev);
    }

    public override bool OnTouchEvent(Android.Views.MotionEvent? ev)
    {
        if (ev is null)
        {
            return base.OnTouchEvent(ev);
        }

        switch (ev.ActionMasked)
        {
            // Nothing under the touch consumed the DOWN (handle, labels): claim the gesture
            // so a drag can drive the sheet.
            case Android.Views.MotionEventActions.Down:
                _downY = ev.RawY;
                _lastSelfY = ev.RawY;
                _draggingSelf = false;

                return true;

            case Android.Views.MotionEventActions.Move when !_draggingSelf && Math.Abs(ev.RawY - _downY) > _touchSlop:
                _draggingSelf = true;
                _lastSelfY = ev.RawY;

                return true;

            case Android.Views.MotionEventActions.Move when _draggingSelf:
                var delta = (ev.RawY - _lastSelfY) / _density;
                _lastSelfY = ev.RawY;
                _sheet.DragBy(delta);

                return true;

            case Android.Views.MotionEventActions.Up:
            case Android.Views.MotionEventActions.Cancel:
                if (_draggingSelf)
                {
                    _draggingSelf = false;
                    _ = _sheet.SettleFromGestureAsync();
                }

                return true;
        }

        return true;
    }

    public bool OnStartNestedScroll(AView? child, AView? target, Android.Views.ScrollAxis axes, int type)
        => (axes & Android.Views.ScrollAxis.Vertical) != 0;

    public void OnNestedScrollAccepted(AView? child, AView? target, Android.Views.ScrollAxis axes, int type)
    {
        Console.WriteLine($"[SHEET] nestedAccepted type={type}");
        _helper.OnNestedScrollAccepted(child!, target!, (int) axes, type);

        if (type == ViewCompat.TypeTouch)
        {
            _sheetMoved = false;
            _nestedActive = true;
        }
    }

    public void OnNestedPreScroll(AView? target, int dx, int dy, int[]? consumed, int type)
    {
        // Finger moving up while the sheet is not fully open: the sheet expands FIRST,
        // consuming the delta before the content scrolls.
        if (dy > 0 && !_sheet.IsFullyOpen)
        {
            var consumedDp = _sheet.DragBy(-dy / _density);

            if (consumedDp != 0)
            {
                if (consumed is not null)
                {
                    consumed[1] = (int) Math.Round(-consumedDp * _density);
                }

                _sheetMoved = true;
            }
        }
    }

    public void OnNestedScroll(AView? target, int dxConsumed, int dyConsumed, int dxUnconsumed, int dyUnconsumed, int type, int[]? consumed)
    {
        // Content refused a downward scroll (already at its top): pull the sheet down.
        // Touch only — a fling reaching the top must not fling the sheet away.
        if (dyUnconsumed < 0 && type == ViewCompat.TypeTouch)
        {
            var consumedDp = _sheet.DragBy(-dyUnconsumed / _density);

            if (consumedDp != 0)
            {
                if (consumed is not null)
                {
                    consumed[1] += (int) Math.Round(-consumedDp * _density);
                }

                _sheetMoved = true;
            }
        }
    }

    public void OnNestedScroll(AView? target, int dxConsumed, int dyConsumed, int dxUnconsumed, int dyUnconsumed, int type)
        => OnNestedScroll(target, dxConsumed, dyConsumed, dxUnconsumed, dyUnconsumed, type, [0, 0]);

    public void OnStopNestedScroll(AView? target, int type)
    {
        _helper.OnStopNestedScroll(target!, type);

        if (type == ViewCompat.TypeTouch)
        {
            _nestedActive = false;
        }

        if (type == ViewCompat.TypeTouch && _sheetMoved)
        {
            _sheetMoved = false;
            _ = _sheet.SettleFromGestureAsync();
        }
    }

    public override bool OnNestedPreFling(AView? target, float velocityX, float velocityY)
        // A fling launched while the sheet is mid-drag belongs to the sheet's settle, not to
        // the content.
        => !_sheet.IsFullyOpen || base.OnNestedPreFling(target!, velocityX, velocityY);
}
