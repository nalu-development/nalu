using Android.Content;
using AndroidX.RecyclerView.Widget;

namespace Nalu;

/// <summary>
/// SmoothScroller that handles different ScrollToPosition options.
/// </summary>
internal class VirtualScrollRecyclerViewLinearSmoothScroller : LinearSmoothScroller
{
    private readonly ScrollToPosition _scrollToPosition;

    public VirtualScrollRecyclerViewLinearSmoothScroller(Context context, ScrollToPosition scrollToPosition) : base(context)
    {
        _scrollToPosition = scrollToPosition;
    }

    protected override int VerticalSnapPreference => _scrollToPosition switch
    {
        ScrollToPosition.Start => SnapToStart,
        ScrollToPosition.End => SnapToEnd,
        _ => SnapToAny
    };

    protected override int HorizontalSnapPreference => _scrollToPosition switch
    {
        ScrollToPosition.Start => SnapToStart,
        ScrollToPosition.End => SnapToEnd,
        _ => SnapToAny
    };
}

