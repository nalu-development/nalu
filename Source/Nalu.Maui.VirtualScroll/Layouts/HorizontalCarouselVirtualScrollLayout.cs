namespace Nalu;

/// <summary>
/// A horizontal carousel layout for virtual scroll that arranges items to fill the available space and applies paging snapping behavior.
/// </summary>
public class HorizontalCarouselVirtualScrollLayout : CarouselVirtualScrollLayout
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HorizontalCarouselVirtualScrollLayout" /> class.
    /// </summary>
    public HorizontalCarouselVirtualScrollLayout()
        : base(ItemsLayoutOrientation.Horizontal)
    {
    }
}
