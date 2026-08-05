using Microsoft.Maui.Layouts;

namespace Nalu;

/// <summary>
/// Lays out every realized slide in the page slot: bounds minus padding minus the peek bands.
/// Slides are positioned by translation (see <see cref="SlideBox" />), so all children share
/// the same frame.
/// </summary>
public class SlideBoxLayoutManager(SlideBox slideBox) : ILayoutManager
{
    /// <inheritdoc />
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var padding = slideBox.Padding;
        var peek = slideBox.PeekAreaInsets;
        var horizontal = slideBox.Orientation == SlideBoxOrientation.Horizontal;

        var peekHorizontal = horizontal ? peek.Left + peek.Right : 0;
        var peekVertical = horizontal ? 0 : peek.Top + peek.Bottom;

        var childWidthConstraint = double.IsPositiveInfinity(widthConstraint) ? double.PositiveInfinity : widthConstraint - padding.HorizontalThickness - peekHorizontal;
        var childHeightConstraint = double.IsPositiveInfinity(heightConstraint) ? double.PositiveInfinity : heightConstraint - padding.VerticalThickness - peekVertical;

        double measuredWidth = 0;
        double measuredHeight = 0;

        foreach (var child in slideBox)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var measure = child.Measure(childWidthConstraint, childHeightConstraint);
            measuredWidth = Math.Max(measuredWidth, measure.Width);
            measuredHeight = Math.Max(measuredHeight, measure.Height);
        }

        measuredWidth += padding.HorizontalThickness + peekHorizontal;
        measuredHeight += padding.VerticalThickness + peekVertical;

        IView layoutView = slideBox;
        var finalWidth = LayoutManager.ResolveConstraints(widthConstraint, layoutView.Width, measuredWidth, layoutView.MinimumWidth, layoutView.MaximumWidth);
        var finalHeight = LayoutManager.ResolveConstraints(heightConstraint, layoutView.Height, measuredHeight, layoutView.MinimumHeight, layoutView.MaximumHeight);

        return new Size(finalWidth, finalHeight);
    }

    /// <inheritdoc />
    public Size ArrangeChildren(Rect bounds)
    {
        var padding = slideBox.Padding;
        var peek = slideBox.PeekAreaInsets;
        var horizontal = slideBox.Orientation == SlideBoxOrientation.Horizontal;

        var slot = new Rect(
            bounds.Left + padding.Left + (horizontal ? peek.Left : 0),
            bounds.Top + padding.Top + (horizontal ? 0 : peek.Top),
            bounds.Width - padding.HorizontalThickness - (horizontal ? peek.Left + peek.Right : 0),
            bounds.Height - padding.VerticalThickness - (horizontal ? 0 : peek.Top + peek.Bottom)
        );

        // Every child gets the same frame regardless of visibility: a slide flipped visible
        // mid-transition must already own a valid frame.
        foreach (var child in slideBox)
        {
            child.Arrange(slot);
        }

        return bounds.Size;
    }
}
