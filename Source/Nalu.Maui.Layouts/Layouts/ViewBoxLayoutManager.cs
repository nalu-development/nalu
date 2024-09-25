namespace Nalu;

using Microsoft.Maui.Layouts;

/// <summary>
/// Layout manager for <see cref="ViewBox"/>.
/// </summary>
/// <param name="contentView">The layout using this <see cref="ILayoutManager"/>.</param>
public class ViewBoxLayoutManager(IContentView contentView) : ILayoutManager
{
    /// <inheritdoc cref="ILayoutManager.Measure"/>
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        var padding = contentView.Padding;

        var paddingHorizontalThickness = padding.HorizontalThickness;
        var paddingVerticalThickness = padding.VerticalThickness;
        var childWidthConstraint = double.IsPositiveInfinity(widthConstraint) ? double.PositiveInfinity : widthConstraint - paddingHorizontalThickness;
        var childHeightConstraint = double.IsPositiveInfinity(heightConstraint) ? double.PositiveInfinity : heightConstraint - paddingVerticalThickness;

        double measuredHeight = 0;
        double measuredWidth = 0;
        if (contentView.PresentedContent is { Visibility: not Visibility.Collapsed } child)
        {
            var measure = child.Measure(childWidthConstraint, childHeightConstraint);
            measuredHeight = measure.Height;
            measuredWidth = measure.Width;
        }

        measuredHeight += paddingVerticalThickness;
        measuredWidth += paddingHorizontalThickness;

        IView layoutView = contentView;
        var finalHeight = LayoutManager.ResolveConstraints(heightConstraint, layoutView.Height, measuredHeight, layoutView.MinimumHeight, layoutView.MaximumHeight);
        var finalWidth = LayoutManager.ResolveConstraints(widthConstraint, layoutView.Width, measuredWidth, layoutView.MinimumWidth, layoutView.MaximumWidth);

        return new Size(finalWidth, finalHeight);
    }

    /// <inheritdoc cref="ILayoutManager.ArrangeChildren"/>
    public Size ArrangeChildren(Rect bounds)
    {
        var padding = contentView.Padding;

        var top = padding.Top + bounds.Y;
        var left = padding.Left + bounds.X;
        var paddingHorizontalThickness = padding.HorizontalThickness;
        var paddingVerticalThickness = padding.VerticalThickness;
        var availableWidth = bounds.Width - paddingHorizontalThickness;
        var availableHeight = bounds.Height - paddingVerticalThickness;
        var width = paddingHorizontalThickness;
        var height = paddingVerticalThickness;

        if (contentView.PresentedContent is { Visibility: not Visibility.Collapsed } child)
        {
            var destination = new Rect(left, top, availableWidth, availableHeight);
            var arranged = child.Arrange(destination);
            width += arranged.Width;
            height += arranged.Height;
        }

        var actual = new Size(width, height);

        return actual.AdjustForFill(bounds, contentView);
    }
}
