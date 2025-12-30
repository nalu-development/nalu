using AndroidX.RecyclerView.Widget;

namespace Nalu;

/// <summary>
/// Handles flattened adapter change notifications and applies them to a RecyclerView using Notify* methods.
/// </summary>
internal class VirtualScrollPlatformFlattenedAdapterNotifier : IDisposable
{
    private readonly VirtualScrollRecyclerViewAdapter _adapter;
    private readonly RecyclerView _recyclerView;
    private readonly IDisposable _subscription;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollPlatformFlattenedAdapterNotifier" /> class.
    /// </summary>
    /// <param name="adapter">The RecyclerView adapter to update.</param>
    /// <param name="recyclerView">The RecyclerView that owns the adapter.</param>
    /// <param name="flattenedAdapter">The flattened adapter to subscribe to.</param>
    public VirtualScrollPlatformFlattenedAdapterNotifier(VirtualScrollRecyclerViewAdapter adapter, RecyclerView recyclerView, IVirtualScrollFlattenedAdapter flattenedAdapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _recyclerView = recyclerView ?? throw new ArgumentNullException(nameof(recyclerView));
        _subscription = flattenedAdapter.Subscribe(OnAdapterChanged);
    }

    private void OnAdapterChanged(VirtualScrollFlattenedChangeSet changeSet)
    {
        if (_disposed)
        {
            return;
        }

        // If RecyclerView is currently computing layout, defer notifications until after layout completes
        // This prevents IndexOutOfBoundsException when adapter changes occur during layout validation
        if (_recyclerView.IsComputingLayout)
        {
            _recyclerView.Post(() =>
            {
                if (!_disposed)
                {
                    ApplyChanges(changeSet);
                }
            });
        }
        else
        {
            ApplyChanges(changeSet);
        }
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

