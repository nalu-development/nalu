using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Layouts;
using Nalu.MagnetLayout;

namespace Nalu;

internal class MagnetLayoutManager : LayoutManager
{
    // TODO: create an interface so we can unit test this layout manager the right way
    public Magnet Magnet { get; }

    public MagnetLayoutManager(Magnet magnet)
        : base(magnet)
    {
        Magnet = magnet;
    }

    public override Size Measure(double widthConstraint, double heightConstraint)
    {
        if (Magnet.Stage is not { } stage)
        {
            return NoStageMeasure(widthConstraint, heightConstraint);
        }

        var maxWidth = 0.0;
        var maxHeight = 0.0;
        var hasMagnetViews = false;

        foreach (var child in Layout)
        {
            if (TryGetMagnetView(stage, child, out var magnetView))
            {
                hasMagnetViews = true;
                magnetView.View = child;
            }
            else
            {
                var size = child.Measure(widthConstraint, heightConstraint);
                maxWidth = Math.Max(size.Width, maxWidth);
                maxHeight = Math.Max(size.Height, maxHeight);
            }
        }

        var padding = Magnet.Padding;
        var horizontalPadding = padding.HorizontalThickness;
        var verticalPadding = padding.VerticalThickness;
        var width = widthConstraint - horizontalPadding;
        var height = heightConstraint - verticalPadding;

        if (hasMagnetViews)
        {
            stage.PrepareForMeasure(width, height);

            maxWidth = Math.Max(stage.Right.CurrentValue, maxWidth);
            maxHeight = Math.Max(stage.Bottom.CurrentValue, maxHeight);
        }

        var measured = new Size(maxWidth + horizontalPadding, maxHeight + verticalPadding);

        return measured;
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

        foreach (var child in Layout)
        {
            if (child.Visibility is Visibility.Collapsed)
            {
                continue;
            }

            if (TryGetMagnetView(stage, child, out var magnetView))
            {
                var frame = magnetView.GetFrame().Offset(left, top);
                child.Arrange(frame);
            }
            else
            {
                var frame = new Rect(Point.Zero, child.DesiredSize).Offset(left, top);
                child.Arrange(frame);
            }
        }

        return bounds.Size;
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
