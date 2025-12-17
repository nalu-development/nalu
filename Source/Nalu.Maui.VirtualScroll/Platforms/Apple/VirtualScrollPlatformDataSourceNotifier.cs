using Foundation;
using UIKit;

namespace Nalu;

/// <summary>
/// Handles adapter change notifications and applies them to a UICollectionView using performBatchUpdates.
/// </summary>
internal class VirtualScrollPlatformDataSourceNotifier : IDisposable
{
    private readonly UICollectionView _collectionView;
    private readonly IDisposable _subscription;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollPlatformDataSourceNotifier" /> class.
    /// </summary>
    /// <param name="collectionView">The collection view to update.</param>
    /// <param name="adapter">The adapter to subscribe to.</param>
    public VirtualScrollPlatformDataSourceNotifier(UICollectionView collectionView, IVirtualScrollAdapter adapter)
    {
        _collectionView = collectionView ?? throw new ArgumentNullException(nameof(collectionView));
        _subscription = adapter.Subscribe(OnAdapterChanged);
    }

    private void OnAdapterChanged(VirtualScrollChangeSet changeSet)
    {
        if (_disposed || _collectionView.Window is null)
        {
            return;
        }

        _collectionView.PerformBatchUpdates(() =>
        {
            foreach (var change in changeSet.Changes)
            {
                ApplyChange(change);
            }
        }, null);
    }

    private void ApplyChange(VirtualScrollChange change)
    {
        if (change.IsSectionChange)
        {
            ApplySectionChange(change);
        }
        else
        {
            ApplyItemChange(change);
        }
    }

    private void ApplySectionChange(VirtualScrollChange change)
    {
        switch (change.Operation)
        {
            case VirtualScrollChangeOperation.InsertSection:
                _collectionView.InsertSections(NSIndexSet.FromIndex(change.StartSectionIndex));
                break;

            case VirtualScrollChangeOperation.InsertSectionRange:
                var insertRange = NSIndexSet.FromNSRange(new NSRange(change.StartSectionIndex, change.EndSectionIndex - change.StartSectionIndex + 1));
                _collectionView.InsertSections(insertRange);
                break;

            case VirtualScrollChangeOperation.RemoveSection:
                _collectionView.DeleteSections(NSIndexSet.FromIndex(change.StartSectionIndex));
                break;

            case VirtualScrollChangeOperation.RemoveSectionRange:
                var removeRange = NSIndexSet.FromNSRange(new NSRange(change.StartSectionIndex, change.EndSectionIndex - change.StartSectionIndex + 1));
                _collectionView.DeleteSections(removeRange);
                break;

            case VirtualScrollChangeOperation.ReplaceSection:
                _collectionView.ReloadSections(NSIndexSet.FromIndex(change.StartSectionIndex));
                break;

            case VirtualScrollChangeOperation.ReplaceSectionRange:
                var replaceRange = NSIndexSet.FromNSRange(new NSRange(change.StartSectionIndex, change.EndSectionIndex - change.StartSectionIndex + 1));
                _collectionView.ReloadSections(replaceRange);
                break;

            case VirtualScrollChangeOperation.MoveSection:
                _collectionView.MoveSection(change.StartSectionIndex, change.EndSectionIndex);
                break;

            case VirtualScrollChangeOperation.RefreshSection:
                _collectionView.ReloadSections(NSIndexSet.FromIndex(change.StartSectionIndex));
                break;
        }
    }

    private void ApplyItemChange(VirtualScrollChange change)
    {
        switch (change.Operation)
        {
            case VirtualScrollChangeOperation.InsertItem:
                _collectionView.InsertItems([NSIndexPath.FromItemSection(change.StartItemIndex, change.StartSectionIndex)]);
                break;

            case VirtualScrollChangeOperation.InsertItemRange:
                var insertIndexPaths = CreateIndexPaths(change.StartSectionIndex, change.StartItemIndex, change.EndItemIndex);
                _collectionView.InsertItems(insertIndexPaths);
                break;

            case VirtualScrollChangeOperation.RemoveItem:
                _collectionView.DeleteItems([NSIndexPath.FromItemSection(change.StartItemIndex, change.StartSectionIndex)]);
                break;

            case VirtualScrollChangeOperation.RemoveItemRange:
                var removeIndexPaths = CreateIndexPaths(change.StartSectionIndex, change.StartItemIndex, change.EndItemIndex);
                _collectionView.DeleteItems(removeIndexPaths);
                break;

            case VirtualScrollChangeOperation.ReplaceItem:
                _collectionView.ReloadItems([NSIndexPath.FromItemSection(change.StartItemIndex, change.StartSectionIndex)]);
                break;

            case VirtualScrollChangeOperation.ReplaceItemRange:
                var replaceIndexPaths = CreateIndexPaths(change.StartSectionIndex, change.StartItemIndex, change.EndItemIndex);
                _collectionView.ReloadItems(replaceIndexPaths);
                break;

            case VirtualScrollChangeOperation.MoveItem:
                var fromIndexPath = NSIndexPath.FromItemSection(change.StartItemIndex, change.StartSectionIndex);
                var toIndexPath = NSIndexPath.FromItemSection(change.EndItemIndex, change.EndSectionIndex);
                _collectionView.MoveItem(fromIndexPath, toIndexPath);
                break;

            case VirtualScrollChangeOperation.RefreshItem:
                _collectionView.ReloadItems([NSIndexPath.FromItemSection(change.StartItemIndex, change.StartSectionIndex)]);
                break;

            case VirtualScrollChangeOperation.Reset:
                _collectionView.ReloadData();
                break;
        }
    }

    private static NSIndexPath[] CreateIndexPaths(int sectionIndex, int startItemIndex, int endItemIndex)
    {
        var count = endItemIndex - startItemIndex + 1;
        var indexPaths = new NSIndexPath[count];
        for (var i = 0; i < count; i++)
        {
            indexPaths[i] = NSIndexPath.FromItemSection(startItemIndex + i, sectionIndex);
        }
        return indexPaths;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _subscription.Dispose();
            _disposed = true;
        }
    }
}

