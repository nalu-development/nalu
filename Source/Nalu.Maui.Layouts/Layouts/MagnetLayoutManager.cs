using Microsoft.Maui.Layouts;

namespace Nalu;

internal class MagnetLayoutManager(Magnet magnet) : LayoutManager(magnet)
{
    public Magnet Magnet { get; } = magnet;

    // True once ArrangeChildren ran after the last Measure: a further arrange without a measure in between
    // (e.g. a recycled cell re-bound and re-arranged at the same size) must re-measure the children.
    private bool _arrangedSinceMeasure;

    public override Size Measure(double widthConstraint, double heightConstraint)
    {
        var padding = Magnet.Padding;
        var horizontalPadding = padding.HorizontalThickness;
        var verticalPadding = padding.VerticalThickness;

        if (Magnet.IsTransitioning)
        {
            return Magnet.TransitionMeasure();
        }

        Magnet.EnsureCompiled();

        var maxWidth = 0.0;
        var maxHeight = 0.0;
        var layout = Layout;
        var childCount = layout.Count;

        for (var index = 0; index < childCount; ++index)
        {
            var child = layout[index];

            if (Magnet.GetBoundNode(child) is not null || child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var size = child.Measure(widthConstraint, heightConstraint);
            maxWidth = Math.Max(size.Width, maxWidth);
            maxHeight = Math.Max(size.Height, maxHeight);
        }

        if (Magnet.HasNodes)
        {
            var stage = Magnet.Engine.Measure(widthConstraint - horizontalPadding, heightConstraint - verticalPadding);
            maxWidth = Math.Max(stage.Width, maxWidth);
            maxHeight = Math.Max(stage.Height, maxHeight);
        }

        _arrangedSinceMeasure = false;

        return new Size(maxWidth + horizontalPadding, maxHeight + verticalPadding);
    }

    public override Size ArrangeChildren(Rect bounds)
    {
        var padding = Magnet.Padding;
        var horizontalPadding = padding.HorizontalThickness;
        var verticalPadding = padding.VerticalThickness;
        var width = bounds.Width - horizontalPadding;
        var height = bounds.Height - verticalPadding;
        var left = bounds.X + padding.Left;
        var top = bounds.Y + padding.Top;

        if (Magnet.IsTransitioning)
        {
            Magnet.LastArrangeBounds = bounds;
            Magnet.TransitionArrange(bounds);

            return bounds.Size;
        }

        Magnet.LastArrangeBounds = bounds;

        if (Magnet.HasNodes)
        {
            Magnet.EnsureCompiled();
            var engine = Magnet.Engine;
            var reuse = engine.HasMeasured
                        && !_arrangedSinceMeasure
                        && MatchesAxis(width, engine.LastMeasureArgs.Width, engine.LastMeasured.Width)
                        && MatchesAxis(height, engine.LastMeasureArgs.Height, engine.LastMeasured.Height);

            engine.Arrange(width, height, !reuse);
            ArrangeNodes(engine, left, top);
        }

        var layout = Layout;
        var childCount = layout.Count;

        for (var index = 0; index < childCount; ++index)
        {
            var child = layout[index];

            if (Magnet.GetBoundNode(child) is not null || child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            child.Arrange(new Rect(new Point(left, top), child.DesiredSize));
        }

        _arrangedSinceMeasure = true;

        return bounds.Size;
    }

    internal static void ArrangeNodes(MagnetLayout.Engine.MagnetEngine engine, double left, double top)
    {
        var nodes = engine.Nodes;

        for (var i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] is not MagnetView || engine.IsCollapsed(i) || engine.GetView(i) is not { } view)
            {
                continue;
            }

            view.Arrange(engine.GetFrame(i).Offset(left, top));
        }
    }

    private static bool MatchesAxis(double arranged, double measureArg, double measured)
        => Math.Abs(arranged - measureArg) < 0.5 || Math.Abs(arranged - measured) < 0.5;
}
