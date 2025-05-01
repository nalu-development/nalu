using Microsoft.Maui.Layouts;

namespace Nalu;

/// <summary>
/// Layout manager for <see cref="IExpanderViewBox" />.
/// </summary>
/// <param name="expander">The layout using this <see cref="ILayoutManager" />.</param>
public class ExpanderViewBoxLayoutManager(IExpanderViewBox expander) : ILayoutManager
{
    /// <inheritdoc cref="ILayoutManager.Measure" />
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var padding = expander.Padding;

        var paddingHorizontalThickness = padding.HorizontalThickness;
        var paddingVerticalThickness = padding.VerticalThickness;
        var childWidthConstraint = double.IsPositiveInfinity(widthConstraint) ? double.PositiveInfinity : widthConstraint - paddingHorizontalThickness;
        var childHeightConstraint = double.IsPositiveInfinity(heightConstraint) ? double.PositiveInfinity : heightConstraint - paddingVerticalThickness;

        var expanderHeightConstraint = expander.CollapsedHeight;
        var expanderWidthConstraint = expander.CollapsedWidth;

        if (expanderHeightConstraint >= 0)
        {
            childHeightConstraint = double.PositiveInfinity;
        }

        if (expanderWidthConstraint >= 0)
        {
            childWidthConstraint = double.PositiveInfinity;
        }

        double measuredHeight = 0;
        double measuredWidth = 0;

        if (expander.PresentedContent is { Visibility: not Visibility.Collapsed } child)
        {
            var measure = child.Measure(childWidthConstraint, childHeightConstraint);
            measuredHeight = measure.Height;
            measuredWidth = measure.Width;
        }

        measuredHeight += paddingVerticalThickness;
        measuredWidth += paddingHorizontalThickness;

        IView layoutView = expander;

        var isExpanded = expander.IsExpanded;
        var arrangeWidth = isExpanded || expanderWidthConstraint < 0 ? measuredWidth : Math.Min(expanderWidthConstraint, measuredWidth);
        var arrangeHeight = isExpanded || expanderHeightConstraint < 0 ? measuredHeight : Math.Min(expanderHeightConstraint, measuredHeight);
        var willCollapseWidth = expanderWidthConstraint >= 0 && measuredWidth > expanderWidthConstraint;
        var willCollapseHeight = expanderHeightConstraint >= 0 && measuredHeight > expanderHeightConstraint;

        expander.SetArrangeSize(arrangeWidth, arrangeHeight, willCollapseWidth || willCollapseHeight);

        if (expanderHeightConstraint >= 0)
        {
            measuredHeight = expander.ArrangeHeight;
        }

        if (expanderWidthConstraint >= 0)
        {
            measuredWidth = expander.ArrangeWidth;
        }

        var finalHeight = LayoutManager.ResolveConstraints(heightConstraint, layoutView.Height, measuredHeight, layoutView.MinimumHeight, layoutView.MaximumHeight);
        var finalWidth = LayoutManager.ResolveConstraints(widthConstraint, layoutView.Width, measuredWidth, layoutView.MinimumWidth, layoutView.MaximumWidth);

        return new Size(finalWidth, finalHeight);
    }

    /// <inheritdoc cref="ILayoutManager.ArrangeChildren" />
    public Size ArrangeChildren(Rect bounds)
    {
        var padding = expander.Padding;

        var top = padding.Top + bounds.Y;
        var left = padding.Left + bounds.X;
        var paddingHorizontalThickness = padding.HorizontalThickness;
        var paddingVerticalThickness = padding.VerticalThickness;
        var availableWidth = bounds.Width - paddingHorizontalThickness;
        var availableHeight = bounds.Height - paddingVerticalThickness;
        var width = paddingHorizontalThickness;
        var height = paddingVerticalThickness;

        if (expander.PresentedContent is { Visibility: not Visibility.Collapsed } child)
        {
            var childWidth = expander.CollapsedWidth >= 0 ? Math.Max(child.DesiredSize.Width, availableWidth) : availableWidth;
            var childHeight = expander.CollapsedHeight >= 0 ? Math.Max(child.DesiredSize.Height, availableHeight) : availableHeight;
            var destination = new Rect(left, top, childWidth, childHeight);

            child.Arrange(destination);
            width += availableWidth;
            height += availableHeight;
        }

        var actual = new Size(width, height);

        return actual.AdjustForFill(bounds, expander);
    }
}
