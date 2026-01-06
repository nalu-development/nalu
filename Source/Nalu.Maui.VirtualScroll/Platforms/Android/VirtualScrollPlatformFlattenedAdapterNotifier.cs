namespace Nalu;

/// <summary>
/// Handles flattened adapter change notifications and applies them to a RecyclerView using Notify* methods.
/// </summary>
internal class VirtualScrollPlatformFlattenedAdapterNotifier : IDisposable
{
    private readonly VirtualScrollRecyclerViewAdapter _adapter;
    private readonly IDisposable _subscription;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollPlatformFlattenedAdapterNotifier" /> class.
    /// </summary>
    /// <param name="adapter">The RecyclerView adapter to update.</param>
    /// <param name="flattenedAdapter">The flattened adapter to subscribe to.</param>
    public VirtualScrollPlatformFlattenedAdapterNotifier(VirtualScrollRecyclerViewAdapter adapter, IVirtualScrollFlattenedAdapter flattenedAdapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _subscription = flattenedAdapter.Subscribe(OnAdapterChanged);
    }

    private void OnAdapterChanged(VirtualScrollFlattenedChangeSet changeSet)
    {
        if (_disposed)
        {
            return;
        }

        if (Android.OS.Looper.MyLooper() != Android.OS.Looper.MainLooper)
        {
            throw new InvalidOperationException("Changes on the data source must be applied and notified on the main thread.");
        }

        // System.Diagnostics.Debug.WriteLine("OnAdapterChanged (Flattened): " + JsonSerializer.Serialize(changeSet, new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() }}));
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
                _adapter.NotifyItemInserted(change.StartItemIndex);
                break;

            case VirtualScrollFlattenedChangeOperation.InsertItemRange:
                var insertCount = change.EndItemIndex - change.StartItemIndex + 1;
                _adapter.NotifyItemRangeInserted(change.StartItemIndex, insertCount);
                break;

            case VirtualScrollFlattenedChangeOperation.RemoveItem:
                _adapter.NotifyItemRemoved(change.StartItemIndex);
                break;

            case VirtualScrollFlattenedChangeOperation.RemoveItemRange:
                var removeCount = change.EndItemIndex - change.StartItemIndex + 1;
                _adapter.NotifyItemRangeRemoved(change.StartItemIndex, removeCount);
                break;

            case VirtualScrollFlattenedChangeOperation.ReplaceItem:
                _adapter.NotifyItemChanged(change.StartItemIndex);
                break;

            case VirtualScrollFlattenedChangeOperation.ReplaceItemRange:
                var replaceCount = change.EndItemIndex - change.StartItemIndex + 1;
                _adapter.NotifyItemRangeChanged(change.StartItemIndex, replaceCount);
                break;

            case VirtualScrollFlattenedChangeOperation.MoveItem:
                _adapter.NotifyItemMoved(change.StartItemIndex, change.EndItemIndex);
                break;

            case VirtualScrollFlattenedChangeOperation.RefreshItem:
                _adapter.NotifyItemChanged(change.StartItemIndex);
                break;

            case VirtualScrollFlattenedChangeOperation.Reset:
                _adapter.NotifyDataSetChanged();
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
