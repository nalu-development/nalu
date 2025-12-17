using UIKit;

namespace Nalu;

/// <summary>
/// Information about global header and footer.
/// </summary>
internal class LayoutGlobalInfo
{
    public bool HasHeader { get; set; }
    public bool HasFooter { get; set; }
}

/// <summary>
/// Information about section headers and footers.
/// </summary>
internal class LayoutSectionInfo
{
    public bool HasHeader { get; set; }
    public bool HasFooter { get; set; }
}

/// <summary>
/// Factory for creating platform-specific layouts for virtual scroll.
/// </summary>
internal static class VirtualScrollPlatformLayoutFactory
{
    public const string ElementKindGlobalHeader = "UICollectionElementKindGlobalHeader";
    public const string ElementKindGlobalFooter = "UICollectionElementKindGlobalFooter";
    public static readonly string ElementKindSectionHeader = UICollectionElementKindSectionKey.Header.ToString();
    public static readonly string ElementKindSectionFooter = UICollectionElementKindSectionKey.Footer.ToString();
    
    /// <summary>
    /// Creates a list layout for the specified linear layout.
    /// </summary>
    /// <param name="linearLayout">The linear layout configuration.</param>
    /// <param name="virtualScroll">The virtual scroll instance to extract header/footer information.</param>
    /// <returns>A UICollectionViewLayout configured for a list layout.</returns>
    public static UICollectionViewLayout CreateList(LinearVirtualScrollLayout linearLayout, IVirtualScroll virtualScroll)
    {
        var scrollDirection = linearLayout.Orientation == ItemsLayoutOrientation.Vertical
            ? UICollectionViewScrollDirection.Vertical
            : UICollectionViewScrollDirection.Horizontal;

        // Extract header/footer information
        var headerFooterInfo = new LayoutGlobalInfo
        {
            HasHeader = virtualScroll.Header is not null,
            HasFooter = virtualScroll.Footer is not null
        };

        var groupingInfo = new LayoutSectionInfo
        {
            HasHeader = virtualScroll.SectionHeaderTemplate is not null,
            HasFooter = virtualScroll.SectionFooterTemplate is not null
        };

        return CreateListLayout(scrollDirection, groupingInfo, headerFooterInfo);
    }

    private static NSCollectionLayoutBoundarySupplementaryItem[] CreateSupplementaryItems(
        LayoutSectionInfo? groupingInfo,
        LayoutGlobalInfo? layoutHeaderFooterInfo,
        UICollectionViewScrollDirection scrollDirection,
        NSCollectionLayoutDimension width,
        NSCollectionLayoutDimension height)
    {
        if (groupingInfo is not null && (groupingInfo.HasHeader || groupingInfo.HasFooter))
        {
            var items = new List<NSCollectionLayoutBoundarySupplementaryItem>();

            if (groupingInfo.HasHeader)
            {
                items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                    NSCollectionLayoutSize.Create(width, height),
                    ElementKindSectionHeader,
                    scrollDirection == UICollectionViewScrollDirection.Vertical
                        ? NSRectAlignment.Top
                        : NSRectAlignment.Leading));
            }

            if (groupingInfo.HasFooter)
            {
                items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                    NSCollectionLayoutSize.Create(width, height),
                    ElementKindSectionFooter,
                    scrollDirection == UICollectionViewScrollDirection.Vertical
                        ? NSRectAlignment.Bottom
                        : NSRectAlignment.Trailing));
            }

            return items.ToArray();
        }

        if (layoutHeaderFooterInfo is not null && (layoutHeaderFooterInfo.HasHeader || layoutHeaderFooterInfo.HasFooter))
        {
            var items = new List<NSCollectionLayoutBoundarySupplementaryItem>();

            if (layoutHeaderFooterInfo.HasHeader)
            {
                items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                    NSCollectionLayoutSize.Create(width, height),
                    ElementKindGlobalHeader,
                    scrollDirection == UICollectionViewScrollDirection.Vertical
                        ? NSRectAlignment.Top
                        : NSRectAlignment.Leading));
            }

            if (layoutHeaderFooterInfo.HasFooter)
            {
                items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                    NSCollectionLayoutSize.Create(width, height),
                    ElementKindGlobalFooter,
                    scrollDirection == UICollectionViewScrollDirection.Vertical
                        ? NSRectAlignment.Bottom
                        : NSRectAlignment.Trailing));
            }

            return items.ToArray();
        }

        return [];
    }

    private static UICollectionViewLayout CreateListLayout(
        UICollectionViewScrollDirection scrollDirection,
        LayoutSectionInfo sectionInfo,
        LayoutGlobalInfo globalInfo)
    {
        var layoutConfiguration = new UICollectionViewCompositionalLayoutConfiguration();
        layoutConfiguration.ScrollDirection = scrollDirection;

        // Create global header and footer
        var groupWidth = scrollDirection == UICollectionViewScrollDirection.Vertical
            ? NSCollectionLayoutDimension.CreateFractionalWidth(1.0f)
            : NSCollectionLayoutDimension.CreateEstimated(44.0f);

        var groupHeight = scrollDirection == UICollectionViewScrollDirection.Vertical
            ? NSCollectionLayoutDimension.CreateEstimated(44.0f)
            : NSCollectionLayoutDimension.CreateFractionalHeight(1.0f);

        layoutConfiguration.BoundarySupplementaryItems = CreateSupplementaryItems(
            null,
            globalInfo,
            scrollDirection,
            groupWidth,
            groupHeight);

        // ReSharper disable UnusedParameter.Local
        var layout = new VirtualScrollCollectionViewLayout((sectionIndex, environment) => 
        {
            // Item dimensions:
            // - For vertical: full width, estimated height
            // - For horizontal: estimated width, full height
            var itemWidth = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateFractionalWidth(1.0f)
                : NSCollectionLayoutDimension.CreateEstimated(44.0f); // Estimated width for horizontal

            var itemHeight = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateEstimated(44.0f) // Estimated height for vertical
                : NSCollectionLayoutDimension.CreateFractionalHeight(1.0f);

            var itemSize = NSCollectionLayoutSize.Create(itemWidth, itemHeight);
            var item = NSCollectionLayoutItem.Create(layoutSize: itemSize);

            // Group dimensions: match item dimensions
            var sectionGroupWidth = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateFractionalWidth(1.0f)
                : itemWidth;

            var sectionGroupHeight = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? itemHeight
                : NSCollectionLayoutDimension.CreateFractionalHeight(1.0f);

            var groupSize = NSCollectionLayoutSize.Create(sectionGroupWidth, sectionGroupHeight);

            // Create the group
            // For vertical list: group layouts horizontally (single column, items stack vertically)
            // For horizontal list: group layouts vertically (single row, items stack horizontally)
            var group = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutGroup.CreateHorizontal(groupSize, item, 1)
                : NSCollectionLayoutGroup.CreateVertical(groupSize, item, 1);

            // Create the section
            var section = NSCollectionLayoutSection.Create(group: group);
            section.InterGroupSpacing = 0;

            // Create header and footer for section
            section.BoundarySupplementaryItems = CreateSupplementaryItems(
                sectionInfo,
                null,
                scrollDirection,
                sectionGroupWidth,
                sectionGroupHeight);

            return section;
        }, layoutConfiguration)
        {
            ScrollDirection = scrollDirection,
            HasGlobalHeader = globalInfo.HasHeader,
            HasGlobalFooter = globalInfo.HasFooter,
            HasSectionHeaders = sectionInfo.HasHeader,
            HasSectionFooters = sectionInfo.HasFooter
        };
        // ReSharper restore UnusedParameter.Local

        return layout;
    }
}

internal class VirtualScrollCollectionViewLayout : UICollectionViewCompositionalLayout
{
    public required UICollectionViewScrollDirection ScrollDirection { get; init; }
    public required bool HasGlobalHeader { get; init; }
    public required bool HasGlobalFooter { get; init; }
    public required bool HasSectionHeaders { get; init; }
    public required bool HasSectionFooters { get; init; }
    
    public VirtualScrollCollectionViewLayout(UICollectionViewCompositionalLayoutSectionProvider sectionProvider, UICollectionViewCompositionalLayoutConfiguration configuration) : base(sectionProvider, configuration)
    {
    }
}
