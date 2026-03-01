using Microsoft.Maui.Layouts;

namespace Nalu;

internal class SlideHolder : ViewBox
{
    public ViewBoxLayoutManager ContainerLayoutManager { get; }

    public SlideHolder(ViewBoxLayoutManager containerLayoutManager)
    {
        ContainerLayoutManager = containerLayoutManager;
    }

    protected override ILayoutManager CreateLayoutManager() => new ContainerBasedViewBoxLayoutManager(this);
        
    private class ContainerBasedViewBoxLayoutManager(SlideHolder slideHolder) : ViewBoxLayoutManager(slideHolder)
    {
        public override Size Measure(double widthConstraint, double heightConstraint)
        {
            if (double.IsPositiveInfinity(widthConstraint))
            {
                var constrainedWidth = slideHolder.ContainerLayoutManager.LastWidthConstraint;
                var size = base.Measure(constrainedWidth, heightConstraint);
                return new Size(constrainedWidth, size.Height);
            }

            if (double.IsPositiveInfinity(heightConstraint))
            {
                var constrainedHeight = slideHolder.ContainerLayoutManager.LastHeightConstraint;
                var size = base.Measure(widthConstraint, constrainedHeight);
                return new Size(size.Width, constrainedHeight);
            }

            return base.Measure(widthConstraint, heightConstraint);
        }
    }
}
