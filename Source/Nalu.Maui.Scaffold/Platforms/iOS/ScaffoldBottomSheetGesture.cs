using CoreGraphics;
using Nalu.Internals;
using UIKit;

namespace Nalu;

/// <summary>
/// Native drag controller for <see cref="ScaffoldBottomSheetView"/> built on the
/// <see cref="ControlledTouchGestureRecognizer"/> pattern: touches are OBSERVED passively —
/// content scrolls natively with zero interference — and the moment the sheet should own the
/// gesture it CLAIMS it (stops propagation), which makes UIKit cancel the inner scroll pan
/// outright. No offset pinning, no simultaneous-recognition races, no content jitter.
/// </summary>
/// <remarks>
/// The contract, matching platform sheet behavior:
/// <list type="bullet">
/// <item>Sheet below its tallest detent → the first slop-exceeding move claims the gesture
/// and drags the SHEET (both directions).</item>
/// <item>Sheet fully open → content scrolls normally; a DOWNWARD move while the scroll rests
/// at its top claims the gesture back for the sheet.</item>
/// <item>No scrollable under the touch → plain sheet drag.</item>
/// </list>
/// Release settles to the nearest detent (or dismisses) through the sheet's own logic.
/// </remarks>
internal sealed class ScaffoldBottomSheetGesture
{
    /// <summary>Movement (in points) before the sheet claims a gesture — taps stay untouched.</summary>
    private const double _claimSlop = 4;

    private readonly ScaffoldBottomSheetView _sheet;
    private readonly ControlledTouchGestureRecognizer _recognizer;
    private UIScrollView? _scrollView;
    private nfloat _startY;
    private nfloat _lastY;
    private bool _claimed;
    private bool _scrollPanDisabled;

    private ScaffoldBottomSheetGesture(ScaffoldBottomSheetView sheet)
    {
        _sheet = sheet;
        _recognizer = new ControlledTouchGestureRecognizer(OnTouch);
    }

    /// <summary>Attaches the controller to the sheet's mounted platform view.</summary>
    public static void Attach(ScaffoldBottomSheetView sheet, UIView platformView)
        => platformView.AddGestureRecognizer(new ScaffoldBottomSheetGesture(sheet)._recognizer);

    private void OnTouch(NativeTouchEvent e)
    {
        switch (e.State)
        {
            case UIGestureRecognizerState.Began:
            {
                // Track Y in the WINDOW frame: the sheet view moves with the drag, so its own
                // coordinate space would swallow the very deltas being applied.
                _startY = _recognizer.LocationInView(null).Y;
                _lastY = _startY;
                _claimed = false;
                _sheet.BeginDrag();
                _scrollView = FindScrollViewAt(e.Location);

                // A touch CATCHING a scroll that is at (or bouncing around) its top claims
                // IMMEDIATELY: a decelerating scroll view's pan activates on the touch-down
                // itself, which fails this recognizer before any Moved event can arrive —
                // Began is the only chance to win the gesture. Semantically safe: with no
                // movement the claim just stops the fling at the top (settle no-ops).
                if (_scrollView is { } caughtScrollView
                    && _sheet.IsFullyOpen
                    && (caughtScrollView.Decelerating || caughtScrollView.Tracking)
                    && caughtScrollView.ContentOffset.Y + caughtScrollView.AdjustedContentInset.Top < 0)
                {
                    Claim(caughtScrollView);
                    e.Propagates = false;
                }

                break;
            }

            case UIGestureRecognizerState.Changed:
            {
                var y = _recognizer.LocationInView(null).Y;
                var delta = y - _lastY;
                _lastY = y;

                if (_claimed)
                {
                    _sheet.DragBy(delta);
                    e.Propagates = false;

                    break;
                }

                var total = y - _startY;

                if (Math.Abs(total) < _claimSlop)
                {
                    break;
                }

                var shouldClaim = _scrollView is not { } scrollView
                                  || !_sheet.IsFullyOpen
                                  || (total > 0 && scrollView.ContentOffset.Y + scrollView.AdjustedContentInset.Top <= 0.5);

                if (shouldClaim)
                {
                    // Apply the accumulated movement so there is no dead zone.
                    if (_scrollView is { } claimedScrollView)
                    {
                        Claim(claimedScrollView);
                    }
                    else
                    {
                        _claimed = true;
                    }

                    _sheet.DragBy(total);
                    e.Propagates = false;
                }

                break;
            }

            case UIGestureRecognizerState.Ended:
            case UIGestureRecognizerState.Cancelled:
            {
                if (_claimed)
                {
                    _ = _sheet.SettleFromGestureAsync();
                }

                if (_scrollPanDisabled && _scrollView is { } releasedScrollView)
                {
                    releasedScrollView.PanGestureRecognizer.Enabled = true;
                }

                _scrollPanDisabled = false;
                _claimed = false;
                _scrollView = null;

                break;
            }
        }
    }

    /// <summary>
    /// Claims the gesture for the sheet: stops propagation upstream (cancels pressed
    /// buttons) and disables the scroll's own pan — an already-active recognizer cannot be
    /// beaten through state arbitration, but toggling Enabled cancels it outright, fling
    /// included (re-enabled on release).
    /// </summary>
    private void Claim(UIScrollView scrollView)
    {
        _claimed = true;
        scrollView.PanGestureRecognizer.Enabled = false;
        _scrollPanDisabled = true;
    }

    /// <summary>The deepest enabled scrollable under the touch point (null → plain sheet drag).</summary>
    private UIScrollView? FindScrollViewAt(CGPoint location)
    {
        if (_recognizer.View is not { } view)
        {
            return null;
        }

        var hit = view.HitTest(location, null);

        for (var current = hit; current is not null && !ReferenceEquals(current, view); current = current.Superview)
        {
            if (current is UIScrollView { ScrollEnabled: true } scrollView)
            {
                return scrollView;
            }
        }

        return null;
    }
}
