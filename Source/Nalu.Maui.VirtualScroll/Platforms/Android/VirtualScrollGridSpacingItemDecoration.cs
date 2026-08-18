using Android.Content;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Platform;
using ARect = Android.Graphics.Rect;
using AView = Android.Views.View;

namespace Nalu;

/// <summary>
/// Applies <see cref="GridVirtualScrollLayout.ItemSpacing" /> and
/// <see cref="GridVirtualScrollLayout.LineSpacing" />: <see cref="GridLayoutManager" /> has no
/// notion of gaps, so the space has to be taken out of the cells themselves.
/// </summary>
/// <remarks>
/// The cross-axis gap is split between neighbours rather than added to each cell, so every cell in
/// a line keeps the same size — adding a full gap to each would make the outer columns narrower
/// than the inner ones.
/// </remarks>
internal sealed class VirtualScrollGridSpacingItemDecoration : RecyclerView.ItemDecoration
{
    private readonly int _itemSpacing;
    private readonly int _lineSpacing;
    private readonly bool _horizontal;

    public VirtualScrollGridSpacingItemDecoration(Context context, GridVirtualScrollLayout gridLayout)
    {
        _itemSpacing = (int) Math.Round(context.ToPixels(gridLayout.ItemSpacing));
        _lineSpacing = (int) Math.Round(context.ToPixels(gridLayout.LineSpacing));
        _horizontal = gridLayout.Orientation == ItemsLayoutOrientation.Horizontal;
    }

    public bool IsEmpty => _itemSpacing == 0 && _lineSpacing == 0;

    public override void GetItemOffsets(ARect outRect, AView view, RecyclerView parent, RecyclerView.State state)
    {
        outRect.Set(0, 0, 0, 0);

        if (parent.GetLayoutManager() is not GridLayoutManager layoutManager)
        {
            return;
        }

        var position = parent.GetChildAdapterPosition(view);

        if (position == RecyclerView.NoPosition)
        {
            return;
        }

        var spanCount = layoutManager.SpanCount;
        var spanIndex = 0;
        var spanSize = spanCount;

        if (view.LayoutParameters is GridLayoutManager.LayoutParams layoutParams)
        {
            spanIndex = layoutParams.SpanIndex;
            spanSize = layoutParams.SpanSize;
        }

        // Full-line positions (headers, footers) span the whole cross axis: no gap to carve out.
        if (_itemSpacing > 0 && spanSize < spanCount && spanIndex >= 0)
        {
            var leading = spanIndex * _itemSpacing / spanCount;
            var trailing = _itemSpacing - ((spanIndex + 1) * _itemSpacing / spanCount);

            if (_horizontal)
            {
                outRect.Top = leading;
                outRect.Bottom = trailing;
            }
            else if (parent.LayoutDirection == Android.Views.LayoutDirection.Rtl)
            {
                outRect.Right = leading;
                outRect.Left = trailing;
            }
            else
            {
                outRect.Left = leading;
                outRect.Right = trailing;
            }
        }

        // A line holds consecutive positions and SpanIndex is the offset within it, so the line
        // starts at position - SpanIndex and only the very first line starts at 0. Derived from
        // the layout params the layout manager has already computed rather than from the span
        // lookup, whose group index would walk the sections on every laid-out cell.
        var isFirstLine = position - Math.Max(spanIndex, 0) == 0;

        if (_lineSpacing > 0 && !isFirstLine)
        {
            // Applied before the line rather than after it, so the content ends flush with the
            // last line instead of trailing a gap the user can scroll into.
            if (!_horizontal)
            {
                outRect.Top = _lineSpacing;
            }
            else if (parent.LayoutDirection == Android.Views.LayoutDirection.Rtl)
            {
                outRect.Right = _lineSpacing;
            }
            else
            {
                outRect.Left = _lineSpacing;
            }
        }
    }
}
