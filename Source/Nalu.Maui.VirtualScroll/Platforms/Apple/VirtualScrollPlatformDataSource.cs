using Foundation;
using UIKit;

namespace Nalu;

internal class VirtualScrollPlatformDataSource(IVirtualScrollAdapter virtualScrollAdapter, IVirtualScroll virtualScroll, VirtualScrollPlatformReuseIdManager reuseIdManager) : UICollectionViewDataSource
{
    public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
    {
        var sectionIndex = indexPath.Section;
        var itemIndex = indexPath.Item.ToInt32();
        
        var item = virtualScrollAdapter.GetItem(sectionIndex, itemIndex);
        var template = virtualScroll.GetItemTemplate(item);
        var reuseId = template is null ? reuseIdManager.DefaultReuseId : reuseIdManager.GetReuseId(template);
        var nativeCell = collectionView.DequeueReusableCell(reuseId, indexPath);
        var layout = collectionView.CollectionViewLayout as VirtualScrollCollectionViewLayout;

        if (nativeCell is VirtualScrollCell virtualScrollCell)
        {
            virtualScrollCell.SupplementaryType = null;
            virtualScrollCell.IndexPath = indexPath;
            var scrollDirection = layout?.ScrollDirection ?? UICollectionViewScrollDirection.Vertical;
            virtualScrollCell.ScrollDirection = scrollDirection;

            var viewFactory = (template ?? throw new InvalidOperationException("Template should not be null for VirtualScrollCell")).CreateContent;
            var handler = virtualScroll.Handler ?? throw new InvalidOperationException("VirtualScroll should not be null");
            virtualScrollCell.SetupView(viewFactory, handler, item);
        }

        return (UICollectionViewCell)nativeCell;
    }

    public override UICollectionReusableView GetViewForSupplementaryElement(UICollectionView collectionView, NSString elementKind, NSIndexPath indexPath)
    {
        var sectionIndex = indexPath.Section;
        var supplementaryType = elementKind.ToString();
        var section = supplementaryType switch
        {
            VirtualScrollPlatformLayoutFactory.ElementKindGlobalFooter or VirtualScrollPlatformLayoutFactory.ElementKindGlobalHeader => VirtualScrollCell.InheritedBindingContext,
            _ => virtualScrollAdapter.GetSection(sectionIndex)
        };

        var template = supplementaryType switch
        {
            VirtualScrollPlatformLayoutFactory.ElementKindGlobalFooter => virtualScroll.GetGlobalFooterTemplate(),
            VirtualScrollPlatformLayoutFactory.ElementKindGlobalHeader => virtualScroll.GetGlobalHeaderTemplate(),
            _ => supplementaryType == VirtualScrollPlatformLayoutFactory.ElementKindSectionHeader
                ? virtualScroll.GetSectionHeaderTemplate(section)
                : supplementaryType == VirtualScrollPlatformLayoutFactory.ElementKindSectionFooter
                    ? virtualScroll.GetSectionFooterTemplate(section)
                    : throw new NotSupportedException($"Supplementary view type {supplementaryType} is not supported.")
        };
        
        var reuseId = template is null ? reuseIdManager.DefaultReuseId : reuseIdManager.GetReuseId(template, supplementaryType);

        var nativeCell = collectionView.DequeueReusableSupplementaryView(elementKind, reuseId, indexPath);
        var layout = collectionView.CollectionViewLayout as VirtualScrollCollectionViewLayout;

        if (nativeCell is VirtualScrollCell virtualScrollCell)
        {
            virtualScrollCell.SupplementaryType = elementKind;
            virtualScrollCell.IndexPath = indexPath;
            var scrollDirection = layout?.ScrollDirection ?? UICollectionViewScrollDirection.Vertical;
            virtualScrollCell.ScrollDirection = scrollDirection;

            var viewFactory = (template ?? throw new InvalidOperationException("Template should not be null for VirtualScrollCell")).CreateContent;
            var handler = virtualScroll.Handler ?? throw new InvalidOperationException("VirtualScroll should not be null");
            virtualScrollCell.SetupView(viewFactory, handler, section);
        }

        return (UICollectionViewCell)nativeCell;
    }

    public override IntPtr GetItemsCount(UICollectionView collectionView, IntPtr section)
    {
        var itemsCount = virtualScrollAdapter.GetItemCount(section.ToInt32());
        return itemsCount;
    }

    public override IntPtr NumberOfSections(UICollectionView collectionView)
    {
        var count = virtualScrollAdapter.GetSectionCount();
        return count;
    }

    public override void MoveItem(UICollectionView collectionView, NSIndexPath sourceIndexPath, NSIndexPath destinationIndexPath)
    {
        var dragHandler = virtualScroll.DragHandler ?? throw new InvalidOperationException("DragHandler should not be null when MoveItem is called.");
        var sourceSectionIndex = sourceIndexPath.Section;
        var sourceItemIndex = sourceIndexPath.Item.ToInt32();
        var destinationSectionIndex = destinationIndexPath.Section;
        var destinationItemIndex = destinationIndexPath.Item.ToInt32();
        var item = virtualScrollAdapter.GetItem(sourceSectionIndex, sourceItemIndex);
        var info = new VirtualScrollDragMoveInfo(item, sourceSectionIndex, sourceItemIndex, destinationSectionIndex, destinationItemIndex);
        dragHandler.MoveItem(info);
        ((VirtualScrollDelegate)collectionView.Delegate).ItemDragMoved(sourceIndexPath, destinationIndexPath);
    }

    public override bool CanMoveItem(UICollectionView collectionView, NSIndexPath indexPath)
    {
        var dragHandler = virtualScroll.DragHandler ?? throw new InvalidOperationException("DragHandler should not be null when CanMoveItem is called.");
        var sectionIndex = indexPath.Section;
        var itemIndex = indexPath.Item.ToInt32();

        var item = virtualScrollAdapter.GetItem(sectionIndex, itemIndex);
        var info = new VirtualScrollDragInfo(item, sectionIndex, itemIndex);
        var canDragItem = dragHandler.CanDragItem(info);

        if (canDragItem)
        {
            ((VirtualScrollDelegate) collectionView.Delegate).ItemDragStarted(indexPath);
        }

        return canDragItem;
    }
}
