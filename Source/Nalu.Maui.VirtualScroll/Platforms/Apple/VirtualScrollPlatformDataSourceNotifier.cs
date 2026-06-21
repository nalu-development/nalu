using Foundation;
using UIKit;

namespace Nalu;

/// <summary>
/// Handles adapter change notifications and applies them to a UICollectionView.
/// </summary>
internal class VirtualScrollPlatformDataSourceNotifier : IDisposable
{
    private readonly List<VirtualScrollChange> _pendingChanges = new(10);
    private UICollectionView _collectionView;
    private IVirtualScrollAdapter _adapter;
    private IDisposable _subscription;
    private int _previousSectionCount;
    private bool _disposed;
    private RunLoopBatcher? _batcher;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollPlatformDataSourceNotifier" /> class.
    /// </summary>
    /// <param name="collectionView">The collection view to update.</param>
    /// <param name="adapter">The adapter to subscribe to.</param>
    public VirtualScrollPlatformDataSourceNotifier(UICollectionView collectionView, IVirtualScrollAdapter adapter)
    {
        _collectionView = collectionView ?? throw new ArgumentNullException(nameof(collectionView));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _previousSectionCount = adapter.GetSectionCount();
        _subscription = adapter.Subscribe(BatchChanges);
    }

    private void BatchChanges(VirtualScrollChangeSet changeSet)
    {
        if (_disposed)
        {
            return;
        }
        
        if (!NSThread.Current.IsMainThread)
        {
            throw new InvalidOperationException("Changes on the data source must be applied and notified on the main thread.");
        }
        
        _pendingChanges.AddRange(changeSet.Changes);
        _batcher ??= new RunLoopBatcher(ApplyPendingChanges);
    }

    internal void ApplyPendingChanges()
    {
        if (_pendingChanges.Count == 0)
        {
            return;
        }

        _collectionView.PerformBatchUpdates(
            () =>
            {
                OnAdapterChanged();
                UpdateSourceCount();
            },
            null
        );

        _batcher?.Dispose();
        _batcher = null;
    }

    private void UpdateSourceCount() => (_collectionView.DataSource as VirtualScrollPlatformDataSource)?.UpdateCounts();

    private void OnAdapterChanged()
    {
        if (_disposed)
        {
            return;
        }
        
        if (!NSThread.Current.IsMainThread)
        {
            throw new InvalidOperationException("Changes on the data source must be applied and notified on the main thread.");
        }

        var newSectionCount = _adapter.GetSectionCount();
        
        // If Reset is present, handle section count changes and reload
        if (_pendingChanges.Any(c => c.Operation == VirtualScrollChangeOperation.Reset))
        {
            var currentSectionCount = _previousSectionCount;

            if (currentSectionCount > newSectionCount)
            {
                // Sections were removed - delete them
                var removeRange = NSIndexSet.FromNSRange(new NSRange(newSectionCount, currentSectionCount - newSectionCount));
                _collectionView.DeleteSections(removeRange);
            }

            // Reload all existing sections that will exist after the reset
            var remainingSectionCount = Math.Min(currentSectionCount, newSectionCount);
            if (remainingSectionCount > 0)
            {
                var reloadRange = NSIndexSet.FromNSRange(new NSRange(0, remainingSectionCount));
                _collectionView.ReloadSections(reloadRange);
            }

            if (currentSectionCount < newSectionCount)
            {
                // Sections were added - insert them
                var addedCount = newSectionCount - currentSectionCount;
                var insertRange = NSIndexSet.FromNSRange(new NSRange(currentSectionCount, addedCount));
                _collectionView.InsertSections(insertRange);
            }
        }
        else
        {
            foreach (var change in _pendingChanges)
            {
                ApplyChange(change);
            }
        }

        // Update tracked section count
        _previousSectionCount = newSectionCount;
        _pendingChanges.Clear();
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
            _subscription = null!;
            _batcher?.Dispose();
            _batcher = null;
            _adapter = null!;
            _collectionView = null!;
            _disposed = true;
        }
    }
}

