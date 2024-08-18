namespace Nalu;

using Microsoft.Maui.Layouts;
using ILayout = Microsoft.Maui.ILayout;

/// <summary>
/// Layout manager for <see cref="ContentLayout"/>.
/// </summary>
/// <param name="layout">The layout using this <see cref="ILayoutManager"/>.</param>
public class ContentLayoutLayoutManager(ILayout layout) : LayoutManager(layout)
{
    /// <inheritdoc />
    public override Size Measure(double widthConstraint, double heightConstraint)
    {
        var childrenCount = Layout.Count;
        if (childrenCount > 1)
        {
            throw new InvalidOperationException($"{Layout.GetType().Name} can only have one child");
        }

        var padding = Layout.Padding;

        double measuredHeight = 0;
        double measuredWidth = 0;
        var childWidthConstraint = double.IsPositiveInfinity(widthConstraint) ? double.PositiveInfinity : widthConstraint - padding.HorizontalThickness;
        var childHeightConstraint = double.IsPositiveInfinity(heightConstraint) ? double.PositiveInfinity : heightConstraint - padding.VerticalThickness;

        if (childrenCount > 0 && Layout[0] is { Visibility: not Visibility.Collapsed } child)
        {
            var measure = child.Measure(childWidthConstraint, childHeightConstraint);
            measuredHeight += measure.Height;
            measuredWidth = Math.Max(measuredWidth, measure.Width);
        }

        measuredHeight += padding.VerticalThickness;
        measuredWidth += padding.HorizontalThickness;

        IView layoutView = Layout;
        var finalHeight = ResolveConstraints(heightConstraint, layoutView.Height, measuredHeight, layoutView.MinimumHeight, layoutView.MaximumHeight);
        var finalWidth = ResolveConstraints(widthConstraint, layoutView.Width, measuredWidth, layoutView.MinimumWidth, layoutView.MaximumWidth);

        return new Size(finalWidth, finalHeight);
    }

    /// <inheritdoc />
    public override Size ArrangeChildren(Rect bounds)
    {
        var childrenCount = Layout.Count;
        if (childrenCount > 1)
        {
            throw new InvalidOperationException($"{Layout.GetType().Name} can only have one child");
        }

        var padding = Layout.Padding;

        var top = padding.Top + bounds.Y;
        var left = padding.Left + bounds.X;
        var availableWidth = bounds.Width - padding.HorizontalThickness;
        var availableHeight = bounds.Height - padding.VerticalThickness;
        var width = padding.HorizontalThickness;
        var height = padding.VerticalThickness;

        if (childrenCount > 0 && Layout[0] is { Visibility: not Visibility.Collapsed } child)
        {
            var destination = new Rect(left, top, availableWidth, availableHeight);
            var arranged = child.Arrange(destination);
            width += arranged.Width;
            height += arranged.Height;
        }

        var actual = new Size(width, height);

        return actual.AdjustForFill(bounds, Layout);
    }
}
