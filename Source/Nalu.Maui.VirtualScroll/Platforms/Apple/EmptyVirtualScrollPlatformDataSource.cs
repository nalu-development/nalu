using Foundation;
using UIKit;

namespace Nalu;

/// <summary>
/// Empty data source implementation for when there's no adapter.
/// </summary>
internal class EmptyVirtualScrollPlatformDataSource : UICollectionViewDataSource
{
    public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath) => throw new InvalidOperationException("Cannot get cell when adapter is not set.");

    public override IntPtr GetItemsCount(UICollectionView collectionView, IntPtr section) => IntPtr.Zero;

    public override IntPtr NumberOfSections(UICollectionView collectionView) => IntPtr.Zero;
}
