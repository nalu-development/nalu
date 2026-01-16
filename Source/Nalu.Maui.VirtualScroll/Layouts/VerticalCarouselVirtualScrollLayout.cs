namespace Nalu;

/// <summary>
/// A vertical carousel layout for virtual scroll that arranges items to fill the available space and applies paging snapping behavior.
/// </summary>
public class VerticalCarouselVirtualScrollLayout : CarouselVirtualScrollLayout
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VerticalCarouselVirtualScrollLayout" /> class.
    /// </summary>
    public VerticalCarouselVirtualScrollLayout()
        : base(ItemsLayoutOrientation.Vertical)
    {
    }
}
