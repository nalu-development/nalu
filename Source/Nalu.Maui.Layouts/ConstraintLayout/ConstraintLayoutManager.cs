namespace Nalu;

using Microsoft.Maui.Layouts;
using Microsoft.Maui.Primitives;

/// <summary>
/// The <see cref="ConstraintLayout"/> layout manager.
/// </summary>
/// <param name="layout">The layout.</param>
public class ConstraintLayoutManager(IConstraintLayout layout) : ILayoutManager
{
    /// <inheritdoc />
    public Size Measure(double widthConstraint, double heightConstraint)
    {
        if (layout.Scene is not { } scene)
        {
            return Size.Zero;
        }

        if (!double.IsPositiveInfinity(widthConstraint) && !double.IsPositiveInfinity(heightConstraint) &&
            layout is { HorizontalLayoutAlignment: LayoutAlignment.Fill, VerticalLayoutAlignment: LayoutAlignment.Fill })
        {
            return new Size(widthConstraint, heightConstraint);
        }

        var paddingLeft = layout.Padding.Left;
        var paddingTop = layout.Padding.Top;
        var paddingRight = layout.Padding.Right;
        var paddingBottom = layout.Padding.Bottom;
        var right = double.IsPositiveInfinity(widthConstraint) ? widthConstraint : widthConstraint - paddingRight - paddingLeft;
        var bottom = double.IsPositiveInfinity(heightConstraint) ? heightConstraint : heightConstraint - paddingBottom - paddingTop;
        scene.Apply(paddingLeft, paddingTop, right, bottom);

        bottom = paddingTop;
        right = paddingLeft;
        foreach (var element in scene)
        {
            if (element is ISceneViewConstraint view)
            {
                if (bottom < view.Bottom.Value)
                {
                    bottom = view.Bottom.Value;
                }

                if (right < view.Right.Value)
                {
                    right = view.Right.Value;
                }
            }
        }

        if (!double.IsPositiveInfinity(widthConstraint) && layout is { HorizontalLayoutAlignment: LayoutAlignment.Fill })
        {
            return new Size(widthConstraint, bottom + paddingBottom);
        }

        if (!double.IsPositiveInfinity(heightConstraint) && layout is { VerticalLayoutAlignment: LayoutAlignment.Fill })
        {
            return new Size(right + paddingRight, heightConstraint);
        }

        return new Size(right + paddingRight, bottom + paddingBottom);
    }

    /// <inheritdoc />
    public Size ArrangeChildren(Rect bounds)
    {
        if (layout.Scene is not { } scene)
        {
            return Size.Zero;
        }

        var dx = bounds.Left;
        var dy = bounds.Top;
        var paddingLeft = layout.Padding.Left;
        var paddingTop = layout.Padding.Top;
        var paddingRight = layout.Padding.Right;
        var paddingBottom = layout.Padding.Bottom;
        var widthConstraint = bounds.Width;
        var heightConstraint = bounds.Height;
        var left = paddingLeft;
        var top = paddingTop;
        var right = double.IsPositiveInfinity(widthConstraint) ? widthConstraint : widthConstraint - paddingRight - paddingLeft;
        var bottom = double.IsPositiveInfinity(heightConstraint) ? heightConstraint : heightConstraint - paddingBottom - paddingTop;
        scene.Apply(left, top, right, bottom);

        bottom = paddingTop;
        right = paddingLeft;
        foreach (var element in scene)
        {
            if (element is ISceneViewConstraint view && scene.GetView(view.Id) is { } targetView)
            {
                if (bottom > view.Bottom.Value)
                {
                    bottom = view.Bottom.Value;
                }

                if (right > view.Right.Value)
                {
                    right = view.Right.Value;
                }

                var l = view.Left.Value;
                var t = view.Top.Value;
                var x = dx + l;
                var y = dy + t;
                var width = view.Right.Value - l;
                var height = view.Bottom.Value - t;
                targetView.Arrange(new Rect(x, y, width, height));
            }
        }

        var size = new Size(right + paddingRight - bounds.Left, bottom + paddingBottom - bounds.Top);
        return size.AdjustForFill(bounds, layout);
    }
}
