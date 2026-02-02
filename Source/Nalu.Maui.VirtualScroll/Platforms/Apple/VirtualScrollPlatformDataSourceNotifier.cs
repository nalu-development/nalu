using Foundation;
using UIKit;

namespace Nalu;

/// <summary>
/// Handles adapter change notifications and applies them to a UICollectionView.
/// </summary>
internal class VirtualScrollPlatformDataSourceNotifier : IDisposable
{
    private readonly UICollectionView _collectionView;
    private readonly IVirtualScrollAdapter _adapter;
    private readonly IDisposable _subscription;
    private readonly Action? _onBatchUpdatesCompleted;
    private int _previousSectionCount;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollPlatformDataSourceNotifier" /> class.
    /// </summary>
    /// <param name="collectionView">The collection view to update.</param>
    /// <param name="adapter">The adapter to subscribe to.</param>
    /// <param name="onBatchUpdatesCompleted">Optional callback invoked after batch updates complete.</param>
    public VirtualScrollPlatformDataSourceNotifier(UICollectionView collectionView, IVirtualScrollAdapter adapter, Action? onBatchUpdatesCompleted = null)
    {
        _collectionView = collectionView ?? throw new ArgumentNullException(nameof(collectionView));
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _onBatchUpdatesCompleted = onBatchUpdatesCompleted;
        _previousSectionCount = adapter.GetSectionCount();
        _subscription = adapter.Subscribe(OnAdapterChanged);
    }

    private void OnAdapterChanged(VirtualScrollChangeSet changeSet)
    {
        if (_disposed)
        {
            return;
        }
        
        if (!NSThread.Current.IsMainThread)
        {
            throw new InvalidOperationException("Changes on the data source must be applied and notified on the main thread.");
        }

        var changes = changeSet.Changes.ToList();
        var newSectionCount = _adapter.GetSectionCount();
        
        // If Reset is present, handle section count changes and reload
        if (changes.Any(c => c.Operation == VirtualScrollChangeOperation.Reset))
        {
            var currentSectionCount = _previousSectionCount;
            
            if (currentSectionCount < newSectionCount)
            {
                // Sections were added - insert them
                var addedCount = newSectionCount - currentSectionCount;
                var insertRange = NSIndexSet.FromNSRange(new NSRange(currentSectionCount, addedCount));
                _collectionView.InsertSections(insertRange);
            }
            else if (currentSectionCount > newSectionCount)
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
        }
        else
        {
            foreach (var change in changeSet.Changes)
            {
                ApplyChange(change);
            }
        }

        // Update tracked section count
        _previousSectionCount = newSectionCount;

        // Use PerformBatchUpdates to handle section count changes, then reload sections
        WaitForUpdatesAndNotify();
    }

    private void WaitForUpdatesAndNotify()
    {
        _collectionView.PerformBatchUpdates(Noop, _ => _onBatchUpdatesCompleted?.Invoke());
        return;

        static void Noop()
        {
        }
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

