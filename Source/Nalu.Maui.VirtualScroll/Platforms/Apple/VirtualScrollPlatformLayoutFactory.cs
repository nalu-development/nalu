using Foundation;
using UIKit;

namespace Nalu;

internal class VirtualScrollCollectionViewLayoutSetup
{
    public required UICollectionViewLayout Layout { get; init; }
    public required Action<UICollectionView> ConfigureCollectionView { get; init; }
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
    
    // ReSharper disable InconsistentNaming
    public static readonly NSString NSElementKindGlobalHeader = new(ElementKindGlobalHeader);
    public static readonly NSString NSElementKindGlobalFooter = new(ElementKindGlobalFooter);
    public static readonly NSString NSElementKindSectionHeader = new(ElementKindSectionHeader);
    public static readonly NSString NSElementKindSectionFooter = new(ElementKindSectionFooter);
    // ReSharper restore InconsistentNaming
    
    private class CellSizingInfo
    {
        public required NSCollectionLayoutDimension ItemWidth { get; init; }
        public required NSCollectionLayoutDimension ItemHeight { get; init; }
        public required NSCollectionLayoutDimension HeaderWidth { get; init; }
        public required NSCollectionLayoutDimension HeaderHeight { get; init; }
        public required NSCollectionLayoutDimension FooterWidth { get; init; }
        public required NSCollectionLayoutDimension FooterHeight { get; init; }
        public required NSCollectionLayoutDimension SectionHeaderWidth { get; init; }
        public required NSCollectionLayoutDimension SectionHeaderHeight { get; init; }
        public required NSCollectionLayoutDimension SectionFooterWidth { get; init; }
        public required NSCollectionLayoutDimension SectionFooterHeight { get; init; }
    }
    
    /// <summary>
    /// Creates a list layout for the specified linear layout.
    /// </summary>
    public static VirtualScrollCollectionViewLayoutSetup CreateList(LinearVirtualScrollLayout linearLayout, IVirtualScrollLayoutInfo layoutInfo)
    {
        if (linearLayout.Orientation == ItemsLayoutOrientation.Horizontal)
        {
            var horizontalLayout = CreateListLayout(
                UICollectionViewScrollDirection.Horizontal,
                layoutInfo,
                new CellSizingInfo
                {
                    ItemWidth = NSCollectionLayoutDimension.CreateEstimated((float) linearLayout.EstimatedItemSize),
                    ItemHeight = NSCollectionLayoutDimension.CreateFractionalHeight(1.0f),
                    HeaderWidth = NSCollectionLayoutDimension.CreateEstimated((float) linearLayout.EstimatedHeaderSize),
                    HeaderHeight = NSCollectionLayoutDimension.CreateFractionalHeight(1.0f),
                    FooterWidth = NSCollectionLayoutDimension.CreateEstimated((float) linearLayout.EstimatedFooterSize),
                    FooterHeight = NSCollectionLayoutDimension.CreateFractionalHeight(1.0f),
                    SectionHeaderWidth = NSCollectionLayoutDimension.CreateEstimated((float) linearLayout.EstimatedHeaderSize),
                    SectionHeaderHeight = NSCollectionLayoutDimension.CreateFractionalHeight(1.0f),
                    SectionFooterWidth = NSCollectionLayoutDimension.CreateEstimated((float) linearLayout.EstimatedFooterSize),
                    SectionFooterHeight = NSCollectionLayoutDimension.CreateFractionalHeight(1.0f)
                }
            );

            return new VirtualScrollCollectionViewLayoutSetup
                   {
                       Layout =  horizontalLayout,
                       ConfigureCollectionView = collectionView =>
                       {
                           if (OperatingSystem.IsIOSVersionAtLeast(17, 4))
                           {
                               collectionView.BouncesHorizontally = true;
                               collectionView.BouncesVertically = false;
                           }
                           collectionView.PagingEnabled = false;
                           collectionView.DirectionalLockEnabled = true;
                       }
                   };
        }

        var verticalLayout = CreateListLayout(
            UICollectionViewScrollDirection.Vertical,
            layoutInfo,
            new CellSizingInfo
            {
                ItemWidth = NSCollectionLayoutDimension.CreateFractionalWidth(1.0f),
                ItemHeight = NSCollectionLayoutDimension.CreateEstimated((float) linearLayout.EstimatedItemSize),
                HeaderWidth = NSCollectionLayoutDimension.CreateFractionalWidth(1.0f),
                HeaderHeight = NSCollectionLayoutDimension.CreateEstimated((float) linearLayout.EstimatedHeaderSize),
                FooterWidth = NSCollectionLayoutDimension.CreateFractionalWidth(1.0f),
                FooterHeight = NSCollectionLayoutDimension.CreateEstimated((float) linearLayout.EstimatedFooterSize),
                SectionHeaderWidth = NSCollectionLayoutDimension.CreateFractionalWidth(1.0f),
                SectionHeaderHeight = NSCollectionLayoutDimension.CreateEstimated((float) linearLayout.EstimatedSectionHeaderSize),
                SectionFooterWidth = NSCollectionLayoutDimension.CreateFractionalWidth(1.0f),
                SectionFooterHeight = NSCollectionLayoutDimension.CreateEstimated((float) linearLayout.EstimatedSectionFooterSize)
            }
        );

        return new VirtualScrollCollectionViewLayoutSetup
               {
                   Layout =  verticalLayout,
                   ConfigureCollectionView = collectionView =>
                   {
                       if (OperatingSystem.IsIOSVersionAtLeast(17, 4))
                       {
                           collectionView.BouncesHorizontally = false;
                           collectionView.BouncesVertically = true;
                       }

                       collectionView.DirectionalLockEnabled = true;
                       collectionView.PagingEnabled = false;
                   }
               };
    }

    public static VirtualScrollCollectionViewLayoutSetup CreateCarousel(CarouselVirtualScrollLayout carouselLayout, IVirtualScrollLayoutInfo layoutInfo)
    {
        var scrollDirection = carouselLayout.Orientation == ItemsLayoutOrientation.Horizontal
            ? UICollectionViewScrollDirection.Horizontal
            : UICollectionViewScrollDirection.Vertical;

        var layout = CreateListLayout(
            scrollDirection,
            layoutInfo,
            new CellSizingInfo
            {
                ItemWidth = NSCollectionLayoutDimension.CreateFractionalWidth(1.0f),
                ItemHeight = NSCollectionLayoutDimension.CreateFractionalHeight(1.0f),
                HeaderWidth = NSCollectionLayoutDimension.CreateFractionalWidth(1.0f),
                HeaderHeight = NSCollectionLayoutDimension.CreateFractionalHeight(1.0f),
                FooterWidth = NSCollectionLayoutDimension.CreateFractionalWidth(1.0f),
                FooterHeight = NSCollectionLayoutDimension.CreateFractionalHeight(1.0f),
                SectionHeaderWidth = NSCollectionLayoutDimension.CreateFractionalWidth(1.0f),
                SectionHeaderHeight = NSCollectionLayoutDimension.CreateFractionalHeight(1.0f),
                SectionFooterWidth = NSCollectionLayoutDimension.CreateFractionalWidth(1.0f),
                SectionFooterHeight = NSCollectionLayoutDimension.CreateFractionalHeight(1.0f)
            }
        );

        return new VirtualScrollCollectionViewLayoutSetup
               {
                   Layout = layout,
                   ConfigureCollectionView = collectionView =>
                   {
                       if (OperatingSystem.IsIOSVersionAtLeast(17, 4))
                       {
                           collectionView.BouncesHorizontally = scrollDirection == UICollectionViewScrollDirection.Horizontal;
                           collectionView.BouncesVertically = scrollDirection == UICollectionViewScrollDirection.Vertical;
                       }
                       collectionView.DirectionalLockEnabled = true;
                       collectionView.PagingEnabled = true;
                   }
               };
    }

    private static NSCollectionLayoutBoundarySupplementaryItem[] CreateGlobalSupplementaryItems(
        IVirtualScrollLayoutInfo layoutInfo,
        UICollectionViewScrollDirection scrollDirection,
        CellSizingInfo sizingInfo)
    {
        var items = new List<NSCollectionLayoutBoundarySupplementaryItem>();

        if (layoutInfo.HasGlobalHeader)
        {
            items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                          NSCollectionLayoutSize.Create(sizingInfo.HeaderWidth, sizingInfo.HeaderHeight),
                          ElementKindGlobalHeader,
                          scrollDirection == UICollectionViewScrollDirection.Vertical
                              ? NSRectAlignment.Top
                              : NSRectAlignment.Leading));
        }

        if (layoutInfo.HasGlobalFooter)
        {
            items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                          NSCollectionLayoutSize.Create(sizingInfo.FooterWidth, sizingInfo.FooterHeight),
                          ElementKindGlobalFooter,
                          scrollDirection == UICollectionViewScrollDirection.Vertical
                              ? NSRectAlignment.Bottom
                              : NSRectAlignment.Trailing));
        }

        return items.Count > 0 ? items.ToArray() : [];
    }

    private static NSCollectionLayoutBoundarySupplementaryItem[] CreateSectionSupplementaryItems(
        IVirtualScrollLayoutInfo layoutInfo, UICollectionViewScrollDirection scrollDirection,
        CellSizingInfo sizingInfo)
    {
        var items = new List<NSCollectionLayoutBoundarySupplementaryItem>();

        if (layoutInfo.HasSectionHeader)
        {
            items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                          NSCollectionLayoutSize.Create(sizingInfo.SectionHeaderWidth, sizingInfo.SectionHeaderHeight),
                          ElementKindSectionHeader,
                          scrollDirection == UICollectionViewScrollDirection.Vertical
                              ? NSRectAlignment.Top
                              : NSRectAlignment.Leading));
        }

        if (layoutInfo.HasSectionFooter)
        {
            items.Add(NSCollectionLayoutBoundarySupplementaryItem.Create(
                          NSCollectionLayoutSize.Create(sizingInfo.SectionFooterWidth, sizingInfo.SectionFooterHeight),
                          ElementKindSectionFooter,
                          scrollDirection == UICollectionViewScrollDirection.Vertical
                              ? NSRectAlignment.Bottom
                              : NSRectAlignment.Trailing));
        }

        return items.Count > 0 ? items.ToArray() : [];
    }

    private static UICollectionViewLayout CreateListLayout(
        UICollectionViewScrollDirection scrollDirection,
        IVirtualScrollLayoutInfo layoutInfo,
        CellSizingInfo sizingInfo)
    {
        var layoutConfiguration = new UICollectionViewCompositionalLayoutConfiguration();
        layoutConfiguration.ScrollDirection = scrollDirection;

        // Create global header and footer
        layoutConfiguration.BoundarySupplementaryItems = CreateGlobalSupplementaryItems(
            layoutInfo,
            scrollDirection,
            sizingInfo);

        // ReSharper disable UnusedParameter.Local
        var layout = new VirtualScrollCollectionViewLayout((sectionIndex, environment) => 
        {
            var itemSize = NSCollectionLayoutSize.Create(sizingInfo.ItemWidth, sizingInfo.ItemHeight);
            var item = NSCollectionLayoutItem.Create(layoutSize: itemSize);

            // Create the group (one layout item per group, it's a depth level we don't use)
            // Section
            //     ├─ Group
            //     │   ├─ Item
            //     ├─ Group
            //     │   ├─ Item

            // Group dimensions: match item dimensions
            var sectionGroupWidth = sizingInfo.ItemWidth;
            var sectionGroupHeight = sizingInfo.ItemHeight;
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
                sizingInfo);

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
    
    /// <summary>
    /// Called when the collection view finalizes layout updates.
    /// Note: Per Apple docs, this is called within the animation block, not after animations complete.
    /// See: https://developer.apple.com/documentation/uikit/uicollectionviewlayout/finalizecollectionviewupdates()
    /// </summary>
    public Action? OnLayoutUpdateCompleted { get; set; }
    
    public VirtualScrollCollectionViewLayout(UICollectionViewCompositionalLayoutSectionProvider sectionProvider, UICollectionViewCompositionalLayoutConfiguration configuration) : base(sectionProvider, configuration)
    {
    }
    
    public override void FinalizeCollectionViewUpdates()
    {
        base.FinalizeCollectionViewUpdates();
        OnLayoutUpdateCompleted?.Invoke();
    }
}
