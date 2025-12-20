using Foundation;
using UIKit;

namespace Nalu;

/// <summary>
/// Factory for creating platform-specific layouts for virtual scroll.
/// </summary>
internal static class VirtualScrollPlatformLayoutFactory
{
    public const string ElementKindGlobalHeader = "UICollectionElementKindGlobalHeader";
    public const string ElementKindGlobalFooter = "UICollectionElementKindGlobalFooter";
    public static readonly string ElementKindSectionHeader = UICollectionElementKindSectionKey.Header.ToString();
    public static readonly string ElementKindSectionFooter = UICollectionElementKindSectionKey.Footer.ToString();
    
    // ReSharper disable InconsistentNaming
    public static readonly NSString NSElementKindGlobalHeader = new(ElementKindGlobalHeader);
    public static readonly NSString NSElementKindGlobalFooter = new(ElementKindGlobalFooter);
    public static readonly NSString NSElementKindSectionHeader = new(ElementKindSectionHeader);
    public static readonly NSString NSElementKindSectionFooter = new(ElementKindSectionFooter);
    // ReSharper restore InconsistentNaming
    
    /// <summary>
    /// Creates a list layout for the specified linear layout.
    /// </summary>
    /// <param name="linearLayout">The linear layout configuration.</param>
    /// <param name="layoutInfo">Information about headers and footers in the layout.</param>
    /// <returns>A UICollectionViewLayout configured for a list layout.</returns>
    public static UICollectionViewLayout CreateList(LinearVirtualScrollLayout linearLayout, IVirtualScrollLayoutInfo layoutInfo)
    {
        var scrollDirection = linearLayout.Orientation == ItemsLayoutOrientation.Vertical
            ? UICollectionViewScrollDirection.Vertical
            : UICollectionViewScrollDirection.Horizontal;

        return CreateListLayout(scrollDirection, layoutInfo);
    }

    private static NSCollectionLayoutBoundarySupplementaryItem[] CreateGlobalSupplementaryItems(
        IVirtualScrollLayoutInfo layoutInfo, UICollectionViewScrollDirection scrollDirection,
        NSCollectionLayoutDimension width, NSCollectionLayoutDimension height)
    {
        var items = new List<NSCollectionLayoutBoundarySupplementaryItem>();

        if (layoutInfo.HasGlobalHeader)
        {
            items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                          NSCollectionLayoutSize.Create(width, height),
                          ElementKindGlobalHeader,
                          scrollDirection == UICollectionViewScrollDirection.Vertical
                              ? NSRectAlignment.Top
                              : NSRectAlignment.Leading));
        }

        if (layoutInfo.HasGlobalFooter)
        {
            items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                          NSCollectionLayoutSize.Create(width, height),
                          ElementKindGlobalFooter,
                          scrollDirection == UICollectionViewScrollDirection.Vertical
                              ? NSRectAlignment.Bottom
                              : NSRectAlignment.Trailing));
        }

        return items.Count > 0 ? items.ToArray() : [];
    }

    private static NSCollectionLayoutBoundarySupplementaryItem[] CreateSectionSupplementaryItems(
        IVirtualScrollLayoutInfo layoutInfo, UICollectionViewScrollDirection scrollDirection,
        NSCollectionLayoutDimension width, NSCollectionLayoutDimension height)
    {
        var items = new List<NSCollectionLayoutBoundarySupplementaryItem>();

        if (layoutInfo.HasSectionHeader)
        {
            items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                          NSCollectionLayoutSize.Create(width, height),
                          ElementKindSectionHeader,
                          scrollDirection == UICollectionViewScrollDirection.Vertical
                              ? NSRectAlignment.Top
                              : NSRectAlignment.Leading));
        }

        if (layoutInfo.HasSectionFooter)
        {
            items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                          NSCollectionLayoutSize.Create(width, height),
                          ElementKindSectionFooter,
                          scrollDirection == UICollectionViewScrollDirection.Vertical
                              ? NSRectAlignment.Bottom
                              : NSRectAlignment.Trailing));
        }

        return items.Count > 0 ? items.ToArray() : [];
    }

    private static UICollectionViewLayout CreateListLayout(
        UICollectionViewScrollDirection scrollDirection,
        IVirtualScrollLayoutInfo layoutInfo)
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

        layoutConfiguration.BoundarySupplementaryItems = CreateGlobalSupplementaryItems(
            layoutInfo,
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
            section.BoundarySupplementaryItems = CreateSectionSupplementaryItems(
                layoutInfo,
                scrollDirection,
                sectionGroupWidth,
                sectionGroupHeight);

            return section;
        }, layoutConfiguration)
        {
            ScrollDirection = scrollDirection
        };
        // ReSharper restore UnusedParameter.Local

        return layout;
    }
}

internal class VirtualScrollCollectionViewLayout : UICollectionViewCompositionalLayout
{
    public required UICollectionViewScrollDirection ScrollDirection { get; init; }
    
    public VirtualScrollCollectionViewLayout(UICollectionViewCompositionalLayoutSectionProvider sectionProvider, UICollectionViewCompositionalLayoutConfiguration configuration) : base(sectionProvider, configuration)
    {
    }
}
