namespace Nalu;

/// <summary>
/// Handles flattened adapter change notifications and applies them to a Windows ItemsRepeater data source.
/// </summary>
internal class VirtualScrollPlatformFlattenedAdapterNotifier : IDisposable
{
    private readonly VirtualScrollItemsSource _itemsSource;
    private readonly IDisposable _subscription;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollPlatformFlattenedAdapterNotifier" /> class.
    /// </summary>
    /// <param name="itemsSource">The ItemsRepeater data source to update.</param>
    /// <param name="flattenedAdapter">The flattened adapter to subscribe to.</param>
    public VirtualScrollPlatformFlattenedAdapterNotifier(VirtualScrollItemsSource itemsSource, IVirtualScrollFlattenedAdapter flattenedAdapter)
    {
        _itemsSource = itemsSource ?? throw new ArgumentNullException(nameof(itemsSource));
        _subscription = flattenedAdapter.Subscribe(OnAdapterChanged);
    }

    private void OnAdapterChanged(VirtualScrollFlattenedChangeSet changeSet)
    {
        if (_disposed)
        {
            return;
        }

        // Ensure we're on the UI thread
        if (Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() is null)
        {
            throw new InvalidOperationException("Changes on the data source must be applied and notified on the UI thread.");
        }

        ApplyChanges(changeSet);
    }

    private void ApplyChanges(VirtualScrollFlattenedChangeSet changeSet)
    {
        foreach (var change in changeSet.Changes)
        {
            ApplyChange(change);
        }
    }

    private void ApplyChange(VirtualScrollFlattenedChange change)
    {
        switch (change.Operation)
        {
            case VirtualScrollFlattenedChangeOperation.InsertItem:
                _itemsSource.NotifyItemInserted(change.StartItemIndex);
                break;

            case VirtualScrollFlattenedChangeOperation.InsertItemRange:
                var insertCount = change.EndItemIndex - change.StartItemIndex + 1;
                _itemsSource.NotifyItemRangeInserted(change.StartItemIndex, insertCount);
                break;

            case VirtualScrollFlattenedChangeOperation.RemoveItem:
                _itemsSource.NotifyItemRemoved(change.StartItemIndex);
                break;

            case VirtualScrollFlattenedChangeOperation.RemoveItemRange:
                var removeCount = change.EndItemIndex - change.StartItemIndex + 1;
                _itemsSource.NotifyItemRangeRemoved(change.StartItemIndex, removeCount);
                break;

            case VirtualScrollFlattenedChangeOperation.ReplaceItem:
                _itemsSource.NotifyItemChanged(change.StartItemIndex);
                break;

            case VirtualScrollFlattenedChangeOperation.ReplaceItemRange:
                var replaceCount = change.EndItemIndex - change.StartItemIndex + 1;
                _itemsSource.NotifyItemRangeChanged(change.StartItemIndex, replaceCount);
                break;

            case VirtualScrollFlattenedChangeOperation.MoveItem:
                _itemsSource.NotifyItemMoved(change.StartItemIndex, change.EndItemIndex);
                break;

            case VirtualScrollFlattenedChangeOperation.RefreshItem:
                _itemsSource.NotifyItemChanged(change.StartItemIndex);
                break;

            case VirtualScrollFlattenedChangeOperation.Reset:
                _itemsSource.NotifyReset();
                break;
        }
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
