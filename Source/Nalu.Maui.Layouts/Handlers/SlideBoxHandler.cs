using Microsoft.Maui.Handlers;
#if ANDROID
using Android.Content;
using Android.Views;
using Microsoft.Maui.Platform;
#endif

namespace Nalu;

#if ANDROID

/// <summary>
/// Platform view of <see cref="SlideBox" /> on Android: a layout that CLAIMS drags along the
/// sliding axis before its children can swallow them.
/// </summary>
/// <remarks>
/// <para>
/// Android delivers a touch stream to the first view that consumes its DOWN — a scrollable slide
/// child (<c>ScrollView</c>, <c>CollectionView</c>…) does exactly that, and a cross-platform
/// <c>PanGestureRecognizer</c> on the parent then never sees the gesture at all. Interception is
/// the platform's answer (it is how <c>ViewPager2</c> hosts scrollable pages): once the drag is
/// dominant along our axis and past the touch slop we take the stream, the child receives an
/// <c>ACTION_CANCEL</c> and the drag drives <see cref="SlideBox" /> directly.
/// </para>
/// <para>
/// Deliberately conservative: nothing is claimed before the slop (taps and child gestures are
/// untouched), never when the cross-axis movement dominates (a vertical scroll inside a
/// horizontal box keeps scrolling), and never while a descendant asked for
/// <c>RequestDisallowInterceptTouchEvent</c> — the standard opt-out for an inner pager or
/// same-axis scrollable, which Android honors by skipping interception entirely.
/// </para>
/// </remarks>
internal class SlideBoxViewGroup : LayoutViewGroup
{
    private readonly int _touchSlop;
    private float _downX;
    private float _downY;
    private bool _claimed;

    public SlideBoxViewGroup(Context context)
        : base(context)
        => _touchSlop = ViewConfiguration.Get(context)?.ScaledTouchSlop ?? 8;

    private SlideBox? Box => CrossPlatformLayout as SlideBox;

    /// <inheritdoc />
    public override bool OnInterceptTouchEvent(MotionEvent? e)
        => (e is not null && ProcessTouch(e)) || base.OnInterceptTouchEvent(e);

    /// <inheritdoc />
    public override bool OnTouchEvent(MotionEvent? e)
    {
        if (e is null)
        {
            return base.OnTouchEvent(e);
        }

        // Consuming the DOWN is what keeps the stream coming when NO child took it (a slide
        // without scrollables): interception is only consulted while a child holds the touch.
        return ProcessTouch(e)
               || (e.ActionMasked == MotionEventActions.Down && Box is { CanInteractivelyDrag: true })
               || base.OnTouchEvent(e);
    }

    /// <summary>
    /// Feeds the event to the drag state machine; true once the SlideBox owns the gesture.
    /// Safe to call for the same event from both touch entry points (the drag offset is
    /// absolute — measured from the DOWN — so a repeated update is idempotent).
    /// </summary>
    private bool ProcessTouch(MotionEvent e)
    {
        var box = Box;

        switch (e.ActionMasked)
        {
            case MotionEventActions.Down:
                // RAW coordinates: the slides translate under the finger while dragging, so
                // view-local ones would drift with the very movement being measured.
                _downX = e.RawX;
                _downY = e.RawY;
                _claimed = false;

                return false;

            case MotionEventActions.Move when _claimed:
                box?.UpdateDrag(AlongAxisOffset(box));

                return true;

            case MotionEventActions.Move:
            {
                if (box is not { CanInteractivelyDrag: true })
                {
                    return false;
                }

                var horizontal = box.IsHorizontalOrientation;
                var along = horizontal ? e.RawX - _downX : e.RawY - _downY;
                var across = horizontal ? e.RawY - _downY : e.RawX - _downX;

                if (Math.Abs(along) <= _touchSlop || Math.Abs(along) <= Math.Abs(across))
                {
                    return false;
                }

                _claimed = true;
                box.BeginDrag();
                box.UpdateDrag(Context!.FromPixels(along));

                return true;
            }

            case MotionEventActions.Up:
            case MotionEventActions.Cancel:
            {
                if (!_claimed)
                {
                    return false;
                }

                _claimed = false;
                box?.EndDrag(canceled: e.ActionMasked == MotionEventActions.Cancel);

                return true;
            }

            default:
                return false;
        }

        float AlongAxisOffset(SlideBox slideBox)
            => (float) Context!.FromPixels(slideBox.IsHorizontalOrientation ? e.RawX - _downX : e.RawY - _downY);
    }
}

#endif

/// <summary>
/// Handler for <see cref="SlideBox" />.
/// </summary>
/// <remarks>
/// Exists for Android only, where the platform view must intercept axis-aligned drags for slides
/// hosting scrollables (see <c>SlideBoxViewGroup</c>). Every other platform behaves like
/// a plain layout and lets the cross-platform pan recognizer drive the drag — on iOS the parent's
/// recognizer happily recognizes alongside an inner scroll view.
/// </remarks>
public partial class SlideBoxHandler : LayoutHandler
{
#if ANDROID
    /// <inheritdoc />
    protected override LayoutViewGroup CreatePlatformView()
    {
        if (VirtualView is null)
        {
            throw new InvalidOperationException($"{nameof(VirtualView)} must be set to create a {nameof(SlideBoxViewGroup)}.");
        }

        var viewGroup = new SlideBoxViewGroup(Context)
                        {
                            CrossPlatformLayout = VirtualView
                        };

        // Mirrors the base handler: the IsClippedToBounds mapper applies the real value after.
        viewGroup.SetClipChildren(false);

        return viewGroup;
    }
#endif
}
