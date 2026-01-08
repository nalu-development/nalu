using System.Collections;

namespace Nalu;

/// <summary>
/// An adapter that wraps an <see cref="IEnumerable"/> for use with <see cref="VirtualScroll"/>.
/// </summary>
public class VirtualScrollListAdapter : IReorderableVirtualScrollAdapter
{
    private static readonly NoOpUnsubscriber _noOpUnsubscriber = new();

    private readonly IList _list;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollListAdapter" /> class based on the specified enumerable.
    /// </summary>
    public VirtualScrollListAdapter(IEnumerable enumerable)
    {
        _list = enumerable is IList list ? list : enumerable.Cast<object>().ToArray();
    }

    /// <inheritdoc/>
    public int GetSectionCount() => _list.Count > 0 ? 1 : 0;

    /// <inheritdoc/>
    public int GetItemCount(int sectionIndex) => _list.Count;

    /// <inheritdoc/>
    public object? GetSection(int sectionIndex) => null;

    /// <inheritdoc/>
    public object? GetItem(int sectionIndex, int itemIndex) => _list[itemIndex];

    /// <inheritdoc/>
    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback) => _noOpUnsubscriber;

    /// <inheritdoc/>
    public virtual bool CanDragItem(VirtualScrollDragInfo dragInfo)
    {
        AssertDragSupport();
        return true;
    }

    /// <inheritdoc/>
    public virtual void MoveItem(VirtualScrollDragMoveInfo dragMoveInfo)
    {
        AssertDragSupport();
        var item = _list[dragMoveInfo.CurrentItemIndex];
        _list.RemoveAt(dragMoveInfo.CurrentItemIndex);
        _list.Insert(dragMoveInfo.DestinationItemIndex, item);
    }

    /// <inheritdoc/>
    public virtual bool CanDropItemAt(VirtualScrollDragDropInfo dragDropInfo) => true;

    /// <inheritdoc/>
    public virtual void OnDragStarted(VirtualScrollDragInfo dragInfo)
    {
    }

    /// <inheritdoc/>
    public virtual void OnDragCanceled(VirtualScrollDragInfo dragInfo)
    {
    }

    /// <inheritdoc/>
    public virtual void OnDragEnded(VirtualScrollDragInfo dragInfo)
    {
    }

    private void AssertDragSupport()
    {
        if (_list.IsFixedSize || _list.IsReadOnly)
        {
            throw new InvalidOperationException($"Drag and drop is not supported for the underlying collection type: {_list.GetType().Name}. The collection must not be fixed-size or read-only.");
        }
    }

    private sealed class NoOpUnsubscriber : IDisposable
    {
        public void Dispose()
        {
            // No-op: list adapter doesn't have change notifications
        }
    }
}
