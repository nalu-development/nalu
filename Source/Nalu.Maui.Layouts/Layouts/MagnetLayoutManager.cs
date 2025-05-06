using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Layouts;
using Nalu.MagnetLayout;

namespace Nalu;

internal class MagnetLayoutManager : LayoutManager
{
    // TODO: create an interface so we can unit test this layout manager the right way
    public Magnet Magnet { get; }

    public MagnetLayoutManager(Magnet magnet) : base(magnet)
    {
        Magnet = magnet;
    }

    public override Size Measure(double widthConstraint, double heightConstraint)
    {
        if (Magnet.Stage is not { } stage)
        {
            return NoStageMeasure(widthConstraint, heightConstraint);
        }

        foreach (var child in Layout)
        {
            if (TryGetMagnetView(stage, child, out var magnetView))
            {
                magnetView.View = child;
            }
            else
            {
                child.Measure(widthConstraint, heightConstraint);
            }
        }

        var padding = Magnet.Padding;
        var horizontalPadding = padding.HorizontalThickness;
        var verticalPadding = padding.VerticalThickness;
        var width = widthConstraint - horizontalPadding;
        var height = heightConstraint - verticalPadding;

        stage.PrepareForMeasure(width, height);

        var measured = MeasureStage(widthConstraint, heightConstraint, stage);

        return new Size(measured.Width + horizontalPadding, measured.Height + verticalPadding);
    }

    public override Size ArrangeChildren(Rect bounds)
    {
        if (Magnet.Stage is not { } stage)
        {
            return NoStageArrange(bounds);
        }
        
        foreach (var child in Layout)
        {
            if (TryGetMagnetView(stage, child, out var magnetView))
            {
                magnetView.View = child;
            }
        }
        
        var padding = Magnet.Padding;
        var horizontalPadding = padding.HorizontalThickness;
        var verticalPadding = padding.VerticalThickness;
        var width = bounds.Width - horizontalPadding;
        var height = bounds.Height - verticalPadding;
        var left = bounds.X + padding.Left;
        var top = bounds.Y + padding.Top;

        stage.PrepareForArrange(width, height);

        var measured = MeasureStage(bounds.Width, bounds.Height, stage,
                                    (view, frame) =>
                                    {
                                        frame = frame.Offset(left, top);
                                        return view.Arrange(frame);
                                    });

        var size = new Size(measured.Width + horizontalPadding, measured.Height + verticalPadding);
        return size.AdjustForFill(bounds, Magnet);
    }

    private Size MeasureStage(double widthConstraint, double heightConstraint, IMagnetStage stage, Func<IView, Rect, Size>? arrange = null)
    {
        if (Layout.Count == 0)
        {
            return Size.Zero;
        }

        var minLeft = widthConstraint;
        var maxRight = 0.0;
        var minTop = heightConstraint;
        var maxBottom = 0.0;
        
        foreach (var child in Layout)
        {
            if (TryGetMagnetView(stage, child, out var magnetView))
            {
                var viewMargin = magnetView.GetEffectiveMargin();
                var viewLeft = magnetView.Left - viewMargin.Left;
                var viewTop = magnetView.Top - viewMargin.Top;
                var viewRight = magnetView.Right + viewMargin.Right;
                var viewBottom = magnetView.Bottom + viewMargin.Bottom;
                minLeft = Math.Min(viewLeft, minLeft);
                maxRight = Math.Max(viewRight, maxRight);
                minTop = Math.Min(viewTop, minTop);
                maxBottom = Math.Max(viewBottom, maxBottom);

                arrange?.Invoke(child, magnetView.GetFrame());
            }
            else
            {
                var size = child.DesiredSize;

                minLeft = Math.Min(0, minLeft);
                minTop = Math.Min(0, minTop);
                maxRight = Math.Max(size.Width, maxRight);
                maxBottom = Math.Max(size.Height, maxBottom);

                arrange?.Invoke(child, new Rect(Point.Zero, size));
            }
        }

        var measuredWidth = maxRight - minLeft;
        var measuredHeight = maxBottom - minTop;
        return new Size(measuredWidth, measuredHeight);
    }

    private Size NoStageMeasure(double widthConstraint, double heightConstraint)
    {
        var padding = Magnet.Padding;

        var maxWidth = 0.0;
        var maxHeight = 0.0;

        foreach (var child in Magnet)
        {
            if (child.Visibility is Visibility.Collapsed)
            {
                continue;
            }

            var size = child.Measure(widthConstraint, heightConstraint);
            maxWidth = Math.Max(size.Width, maxWidth);
            maxHeight = Math.Max(size.Height, maxHeight);
        }

        var measuredWidth = maxWidth + padding.Left + padding.Right;
        var measuredHeight = maxHeight + padding.Top + padding.Bottom;

        return new Size(measuredWidth, measuredHeight);
    }

    private Size NoStageArrange(Rect bounds)
    {
        var padding = Magnet.Padding;
        var paddingLeft = padding.Left;
        var paddingTop = padding.Top;

        var maxWidth = 0.0;
        var maxHeight = 0.0;

        foreach (var child in Layout)
        {
            if (child.Visibility is Visibility.Collapsed)
            {
                continue;
            }

            var frame = new Rect(paddingLeft + bounds.Left, paddingTop + bounds.Top, child.DesiredSize.Width, child.DesiredSize.Height);
            maxWidth = Math.Max(frame.Width, maxWidth);
            maxHeight = Math.Max(frame.Height, maxHeight);
            child.Arrange(frame);
        }

        var measuredWidth = maxWidth + padding.Left + padding.Right;
        var measuredHeight = maxHeight + padding.Top + padding.Bottom;
        var size = new Size(measuredWidth, measuredHeight);

        return size.AdjustForFill(bounds, Magnet);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetMagnetView(IMagnetStage stage, IView child, [NotNullWhen(true)] out MagnetView? magnetView)
    {
        if (Magnet.GetStageId(child) is { } stageId && stage.TryGetElement(stageId, out var element) && element is MagnetView view)
        {
            magnetView = view;
            return true;
        }

        magnetView = null;
        return false;
    }
}
