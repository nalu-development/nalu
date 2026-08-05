using CoreGraphics;
using UIKit;

namespace Nalu;

/// <summary>
/// Native drag controller for <see cref="ScaffoldBottomSheetView"/>: a pan recognizer that
/// COOPERATES with an inner <see cref="UIScrollView"/> instead of stealing its gesture
/// (a MAUI pan on the sheet would win outright and kill content scrolling).
/// </summary>
/// <remarks>
/// The contract, matching platform sheet behavior:
/// <list type="bullet">
/// <item>Sheet below its tallest detent → the drag moves the SHEET (both directions); the
/// inner scroll offset is pinned so content doesn't move underneath.</item>
/// <item>Sheet fully open → content scrolls normally; a DOWNWARD drag while the scroll rests
/// at its top hands the gesture back to the sheet.</item>
/// <item>No scrollable under the touch → plain sheet drag.</item>
/// </list>
/// Release settles to the nearest detent (or dismisses) through the sheet's own logic.
/// </remarks>
internal sealed class ScaffoldBottomSheetGesture : UIPanGestureRecognizer
{
    private readonly ScaffoldBottomSheetView _sheet;
    private UIScrollView? _scrollView;
    private CGPoint _pinnedOffset;
    private nfloat _lastY;
    private bool _sheetConsuming;

    private ScaffoldBottomSheetGesture(ScaffoldBottomSheetView sheet)
    {
        _sheet = sheet;
        AddTarget(OnPan);
        ShouldRecognizeSimultaneously = static (_, _) => true;
    }

    /// <summary>Attaches the controller to the sheet's mounted platform view.</summary>
    public static void Attach(ScaffoldBottomSheetView sheet, UIView platformView)
        => platformView.AddGestureRecognizer(new ScaffoldBottomSheetGesture(sheet));

    private void OnPan()
    {
        switch (State)
        {
            case UIGestureRecognizerState.Began:
            {
                _lastY = TranslationInView(View).Y;
                _scrollView = FindScrollViewUnderTouch();
                _sheetConsuming = false;

                break;
            }

            case UIGestureRecognizerState.Changed:
            {
                var y = TranslationInView(View).Y;
                var delta = y - _lastY;
                _lastY = y;

                if (delta == 0)
                {
                    break;
                }

                if (_scrollView is not { } scrollView)
                {
                    _sheet.DragBy(delta);
                    _sheetConsuming = true;

                    break;
                }

                var scrollAtTop = scrollView.ContentOffset.Y <= -scrollView.AdjustedContentInset.Top + 0.5;

                // The sheet takes over while it is below the top detent, or when a downward
                // drag starts from the scroll's top. Once consuming, it keeps consuming until
                // fully open again — no mid-gesture flip-flop.
                if (_sheetConsuming || !_sheet.IsFullyOpen || (delta > 0 && scrollAtTop))
                {
                    var consumed = _sheet.DragBy(delta);
                    _sheetConsuming = consumed != 0 || !_sheet.IsFullyOpen;

                    if (_sheetConsuming)
                    {
                        // Pin the content: the scroll pan runs simultaneously and would
                        // otherwise move it under the finger.
                        _pinnedOffset = new CGPoint(scrollView.ContentOffset.X, -scrollView.AdjustedContentInset.Top);
                        scrollView.SetContentOffset(_pinnedOffset, false);
                    }
                }

                break;
            }

            case UIGestureRecognizerState.Ended:
            case UIGestureRecognizerState.Cancelled:
            {
                if (_sheetConsuming)
                {
                    // The scroll pan ran simultaneously and hands into a deceleration at
                    // release: a final non-animated pin kills it (the content must stay put
                    // while the sheet was consuming the gesture).
                    _scrollView?.SetContentOffset(_pinnedOffset, false);
                    _ = _sheet.SettleFromGestureAsync();
                }

                _scrollView = null;
                _sheetConsuming = false;

                break;
            }
        }
    }

    /// <summary>The deepest scrollable under the touch point (null → plain sheet drag).</summary>
    private UIScrollView? FindScrollViewUnderTouch()
    {
        if (View is not { } view)
        {
            return null;
        }

        var hit = view.HitTest(LocationInView(view), null);

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
