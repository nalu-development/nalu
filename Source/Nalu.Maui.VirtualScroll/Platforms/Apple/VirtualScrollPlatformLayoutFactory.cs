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
        => CreateListLayout(linearLayout, layoutInfo);

    private static NSCollectionLayoutBoundarySupplementaryItem[] CreateGlobalSupplementaryItems(
        IVirtualScrollLayoutInfo layoutInfo, UICollectionViewScrollDirection scrollDirection,
        LinearVirtualScrollLayout linearLayout)
    {
        var items = new List<NSCollectionLayoutBoundarySupplementaryItem>();

        if (layoutInfo.HasGlobalHeader)
        {
            var width = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateFractionalWidth(1.0f)
                : NSCollectionLayoutDimension.CreateEstimated((float)linearLayout.EstimatedHeaderSize);

            var height = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateEstimated((float)linearLayout.EstimatedHeaderSize)
                : NSCollectionLayoutDimension.CreateFractionalHeight(1.0f);
            
            items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                          NSCollectionLayoutSize.Create(width, height),
                          ElementKindGlobalHeader,
                          scrollDirection == UICollectionViewScrollDirection.Vertical
                              ? NSRectAlignment.Top
                              : NSRectAlignment.Leading));
        }

        if (layoutInfo.HasGlobalFooter)
        {
            var width = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateFractionalWidth(1.0f)
                : NSCollectionLayoutDimension.CreateEstimated((float)linearLayout.EstimatedFooterSize);

            var height = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateEstimated((float)linearLayout.EstimatedFooterSize)
                : NSCollectionLayoutDimension.CreateFractionalHeight(1.0f);

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
        LinearVirtualScrollLayout linearLayout)
    {
        var items = new List<NSCollectionLayoutBoundarySupplementaryItem>();

        if (layoutInfo.HasSectionHeader)
        {
            var width = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateFractionalWidth(1.0f)
                : NSCollectionLayoutDimension.CreateEstimated((float)linearLayout.EstimatedSectionHeaderSize);

            var height = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateEstimated((float)linearLayout.EstimatedSectionHeaderSize)
                : NSCollectionLayoutDimension.CreateFractionalHeight(1.0f);
            
            items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                          NSCollectionLayoutSize.Create(width, height),
                          ElementKindSectionHeader,
                          scrollDirection == UICollectionViewScrollDirection.Vertical
                              ? NSRectAlignment.Top
                              : NSRectAlignment.Leading));
        }

        if (layoutInfo.HasSectionFooter)
        {
            var width = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateFractionalWidth(1.0f)
                : NSCollectionLayoutDimension.CreateEstimated((float)linearLayout.EstimatedSectionFooterSize);

            var height = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateEstimated((float)linearLayout.EstimatedSectionFooterSize)
                : NSCollectionLayoutDimension.CreateFractionalHeight(1.0f);
            
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
        LinearVirtualScrollLayout linearLayout,
        IVirtualScrollLayoutInfo layoutInfo)
    {
        var scrollDirection = linearLayout.Orientation == ItemsLayoutOrientation.Vertical
            ? UICollectionViewScrollDirection.Vertical
            : UICollectionViewScrollDirection.Horizontal;

        var layoutConfiguration = new UICollectionViewCompositionalLayoutConfiguration();
        layoutConfiguration.ScrollDirection = scrollDirection;

        // Create global header and footer
        layoutConfiguration.BoundarySupplementaryItems = CreateGlobalSupplementaryItems(
            layoutInfo,
            scrollDirection,
            linearLayout);

        // ReSharper disable UnusedParameter.Local
        var layout = new VirtualScrollCollectionViewLayout((sectionIndex, environment) => 
        {
            // Item dimensions:
            // - For vertical: full width, estimated height
            // - For horizontal: estimated width, full height
            var itemWidth = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateFractionalWidth(1.0f)
                : NSCollectionLayoutDimension.CreateEstimated((float)linearLayout.EstimatedItemSize);

            var itemHeight = scrollDirection == UICollectionViewScrollDirection.Vertical
                ? NSCollectionLayoutDimension.CreateEstimated((float)linearLayout.EstimatedItemSize)
                : NSCollectionLayoutDimension.CreateFractionalHeight(1.0f);

            var itemSize = NSCollectionLayoutSize.Create(itemWidth, itemHeight);
            var item = NSCollectionLayoutItem.Create(layoutSize: itemSize);

            // Create the group (one layout item per group, it's a depth level we don't use)
            // Section
            //     ├─ Group
            //     │   ├─ Item
            //     ├─ Group
            //     │   ├─ Item

            // Group dimensions: match item dimensions
            var sectionGroupWidth = itemWidth;
            var sectionGroupHeight = itemHeight;
            var groupSize = NSCollectionLayoutSize.Create(sectionGroupWidth, sectionGroupHeight);

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
                linearLayout);

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
