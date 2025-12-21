using UIKit;

namespace Nalu;

// ReSharper disable once InconsistentNaming
internal static class UIScrollViewExtensions
{
    /// <summary>
    /// Gets the distance from the top edge. Returns 0 when at the top, positive when scrolled down.
    /// </summary>
    public static double GetDistanceFromTop(this UIScrollView scrollView) =>
        scrollView.ContentOffset.Y + scrollView.ContentInset.Top;

    /// <summary>
    /// Gets the distance from the left edge. Returns 0 when at the left, positive when scrolled right.
    /// </summary>
    public static double GetDistanceFromLeft(this UIScrollView scrollView) =>
        scrollView.ContentOffset.X + scrollView.ContentInset.Left;

    /// <summary>
    /// Gets the distance from the bottom edge. Returns 0 when at the bottom, positive when scrolled up from bottom.
    /// </summary>
    public static double GetDistanceFromBottom(this UIScrollView scrollView)
    {
        var scrollViewHeight = scrollView.Bounds.Height;
        var scrollContentSizeHeight = scrollView.ContentSize.Height;
        var bottomInset = scrollView.ContentInset.Bottom;
        var verticalOffsetForBottom = scrollContentSizeHeight + bottomInset - scrollViewHeight;
        return verticalOffsetForBottom - scrollView.ContentOffset.Y;
    }

    /// <summary>
    /// Gets the distance from the right edge. Returns 0 when at the right, positive when scrolled left from right.
    /// </summary>
    public static double GetDistanceFromRight(this UIScrollView scrollView)
    {
        var scrollViewWidth = scrollView.Bounds.Width;
        var scrollContentSizeWidth = scrollView.ContentSize.Width;
        var rightInset = scrollView.ContentInset.Right;
        var horizontalOffsetForRight = scrollContentSizeWidth + rightInset - scrollViewWidth;
        return horizontalOffsetForRight - scrollView.ContentOffset.X;
    }
}

