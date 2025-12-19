using AndroidX.RecyclerView.Widget;
using ARect = Android.Graphics.Rect;

namespace Nalu;

/// <summary>
/// Helper class for scrolling RecyclerView with different ScrollToPosition options.
/// </summary>
// Forked from https://github.com/dotnet/maui/blob/main/src/Controls/src/Core/Handlers/Items/Android/ScrollHelper.cs
internal class VirtualScrollRecyclerViewScrollHelper : RecyclerView.OnScrollListener
{
    private readonly RecyclerView _recyclerView;
    private Action? _pendingScrollAdjustment;
    private bool _undoNextScrollAdjustment;
    private bool _maintainingScrollOffsets;
    private int _lastScrollX;
    private int _lastScrollY;
    private int _lastDeltaX;
    private int _lastDeltaY;

    public VirtualScrollRecyclerViewScrollHelper(RecyclerView recyclerView)
    {
        _recyclerView = recyclerView;
    }

    /// <summary>
    /// Used to maintain scroll offset when using ItemsUpdatingScrollMode KeepScrollOffset.
    /// </summary>
    public void UndoNextScrollAdjustment()
    {
        // Don't start tracking the scroll offsets until we really need to
        if (!_maintainingScrollOffsets)
        {
            _maintainingScrollOffsets = true;
            _recyclerView.AddOnScrollListener(this);
        }

        _undoNextScrollAdjustment = true;

        _lastScrollX = _recyclerView.ComputeHorizontalScrollOffset();
        _lastScrollY = _recyclerView.ComputeVerticalScrollOffset();
    }

    /// <summary>
    /// Adjusts scroll after a pending operation.
    /// </summary>
    public void AdjustScroll() => _pendingScrollAdjustment?.Invoke();

    /// <summary>
    /// Animates scrolling to a position with the specified ScrollToPosition.
    /// </summary>
    public void AnimateScrollToPosition(int index, ScrollToPosition scrollToPosition)
    {
        if (scrollToPosition == ScrollToPosition.MakeVisible)
        {
            // MakeVisible matches the Android default of SnapAny, so we can just use the default
            _recyclerView.SmoothScrollToPosition(index);
        }
        else
        {
            // If we want a different ScrollToPosition, we need to create a SmoothScroller which can handle it
            var smoothScroller = new VirtualScrollRecyclerViewLinearSmoothScroller(_recyclerView.Context!, scrollToPosition)
            {
                TargetPosition = index
            };

            // And kick off the scroll operation
            _recyclerView.GetLayoutManager()?.StartSmoothScroll(smoothScroller);
        }
    }

    /// <summary>
    /// Immediately jumps to a position with the specified ScrollToPosition.
    /// </summary>
    public void JumpScrollToPosition(int index, ScrollToPosition scrollToPosition)
    {
        if (scrollToPosition == ScrollToPosition.MakeVisible)
        {
            // MakeVisible is the default behavior, so we don't need to do anything special
            _recyclerView.ScrollToPosition(index);
            return;
        }

        if (!(_recyclerView.GetLayoutManager() is LinearLayoutManager linearLayoutManager))
        {
            // We don't have the ScrollToPositionWithOffset method available, so we don't have a way to 
            // handle the Forms ScrollToPosition; just default back to the MakeVisible behavior
            _recyclerView.ScrollToPosition(index);
            return;
        }

        // If ScrollToPosition is Start, then we can just use an offset of 0 and we're fine
        if (scrollToPosition == ScrollToPosition.Start)
        {
            linearLayoutManager.ScrollToPositionWithOffset(index, 0);
            return;
        }

        // For handling End or Center, things get more complicated because we need to know the size of
        // the View we're targeting. 

        // The item may not actually exist; it may have never been realized, or it may have been recycled
        // So we need to get it on screen using ScrollToPosition, then once it's on screen we can use the 
        // width/height to make adjustments for Center/End.

        // ScrollToPosition queues up the scroll operation. It doesn't do it immediately; it requests a layout
        // After that layout is finished, the view will be available for measurement, and then we can adjust
        // the scroll to get it into the right place

        // Set up our pending adjustment
        if (linearLayoutManager.CanScrollVertically())
        {
            _pendingScrollAdjustment = () => AdjustVerticalScroll(index, scrollToPosition);
        }
        else
        {
            _pendingScrollAdjustment = () => AdjustHorizontalScroll(index, scrollToPosition);
        }

        // Kick off the Scroll to get the item into view; once it's in view, the pending adjustment will kick in
        _recyclerView.ScrollToPosition(index);
    }

    private ARect? GetViewRect(int index)
    {
        var holder = _recyclerView.FindViewHolderForAdapterPosition(index);
        var view = holder?.ItemView;

        if (view == null)
        {
            return null;
        }

        var viewRect = new ARect();
        view.GetGlobalVisibleRect(viewRect);

        return viewRect;
    }

    private void AdjustVerticalScroll(int index, ScrollToPosition scrollToPosition)
    {
        _pendingScrollAdjustment = null;

        var viewRect = GetViewRect(index);

        if (viewRect == null)
        {
            return;
        }

        var offset = 0;

        var rvRect = new ARect();
        _recyclerView.GetGlobalVisibleRect(rvRect);

        // Account for RecyclerView padding (which may include window insets)
        var paddingTop = _recyclerView.PaddingTop;
        var paddingBottom = _recyclerView.PaddingBottom;

        if (scrollToPosition == ScrollToPosition.Center)
        {
            // Center relative to the visible content area (excluding padding)
            var contentCenterY = rvRect.Top + paddingTop + (rvRect.Height() - paddingTop - paddingBottom) / 2;
            offset = viewRect.CenterY() - contentCenterY;
        }
        else if (scrollToPosition == ScrollToPosition.End)
        {
            // End relative to the visible content area (excluding bottom padding)
            var contentBottom = rvRect.Bottom - paddingBottom;
            offset = viewRect.Bottom - contentBottom;
        }

        _recyclerView.ScrollBy(0, offset);
    }

    private void AdjustHorizontalScroll(int index, ScrollToPosition scrollToPosition)
    {
        _pendingScrollAdjustment = null;

        var viewRect = GetViewRect(index);

        if (viewRect == null)
        {
            return;
        }

        var offset = 0;

        var rvRect = new ARect();
        _recyclerView.GetGlobalVisibleRect(rvRect);

        // Account for RecyclerView padding (which may include window insets)
        var paddingLeft = _recyclerView.PaddingLeft;
        var paddingRight = _recyclerView.PaddingRight;

        if (scrollToPosition == ScrollToPosition.Center)
        {
            // Center relative to the visible content area (excluding padding)
            var contentCenterX = rvRect.Left + paddingLeft + (rvRect.Width() - paddingLeft - paddingRight) / 2;
            offset = viewRect.CenterX() - contentCenterX;
        }
        else if (scrollToPosition == ScrollToPosition.End)
        {
            // End relative to the visible content area (excluding right padding)
            var contentRight = rvRect.Right - paddingRight;
            offset = viewRect.Right - contentRight;
        }

        _recyclerView.ScrollBy(offset, 0);
    }

    private void TrackOffsets()
    {
        var newXOffset = _recyclerView.ComputeHorizontalScrollOffset();
        var newYOffset = _recyclerView.ComputeVerticalScrollOffset();

        _lastDeltaX = Math.Max(newXOffset - _lastScrollX, 0);
        _lastDeltaY = Math.Max(newYOffset - _lastScrollY, 0);

        _lastScrollX = newXOffset;
        _lastScrollY = newYOffset;

        if (_undoNextScrollAdjustment)
        {
            // This last scroll adjustment happened because a new item was added, and it caused the scroll
            // offset to shift; since the ItemsUpdatingScrollMode is set to KeepScrollOffset; we need to undo 
            // that shift and stay where we were before the item was added

            _undoNextScrollAdjustment = false;
            _recyclerView.ScrollBy(-_lastDeltaX, -_lastDeltaY);

            _lastDeltaX = 0;
            _lastDeltaY = 0;
        }
    }

    public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
    {
        base.OnScrolled(recyclerView, dx, dy);
        TrackOffsets();
    }
}

