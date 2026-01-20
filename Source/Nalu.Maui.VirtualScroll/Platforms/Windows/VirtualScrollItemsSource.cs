using System.Collections;
using System.Collections.Specialized;

namespace Nalu;

/// <summary>
/// Data source for Windows ItemsRepeater that wraps the flattened adapter.
/// </summary>
internal class VirtualScrollItemsSource : IReadOnlyList<VirtualScrollItemWrapper>, INotifyCollectionChanged
{
    private readonly IVirtualScrollFlattenedAdapter _flattenedAdapter;

    public VirtualScrollItemsSource(IVirtualScrollFlattenedAdapter flattenedAdapter)
    {
        _flattenedAdapter = flattenedAdapter;
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count => _flattenedAdapter.GetItemCount();

    public VirtualScrollItemWrapper this[int index]
    {
        get
        {
            var item = _flattenedAdapter.GetItem(index);
            return new VirtualScrollItemWrapper(index, item);   
        }
    }

    public void NotifyReset() => CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));

    public void NotifyItemInserted(int index)
    {
        var item = new VirtualScrollItemWrapper(index, _flattenedAdapter.GetItem(index));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    public void NotifyItemRangeInserted(int startIndex, int count)
    {
        var items = new List<VirtualScrollItemWrapper>();
        for (var i = startIndex; i < startIndex + count; i++)
        {
            items.Add(new VirtualScrollItemWrapper(i, _flattenedAdapter.GetItem(i)));
        }
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items, startIndex));
    }

    public void NotifyItemRemoved(int index)
    {
        // For Remove, the adapter has already been updated, so we can't get the removed item
        // Create a wrapper with the index - ItemsRepeater will use the index to identify what to remove
        // We use a default item since we can't retrieve the actual removed item
        var removedItem = new VirtualScrollItemWrapper(index, default(VirtualScrollFlattenedItem));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItem, index));
    }

    public void NotifyItemRangeRemoved(int startIndex, int count)
    {
        // For Remove, the adapter has already been updated, so we can't get the removed items
        // Create wrappers with indices - ItemsRepeater will use the indices to identify what to remove
        var items = new List<VirtualScrollItemWrapper>();
        for (var i = 0; i < count; i++)
        {
            items.Add(new VirtualScrollItemWrapper(startIndex + i, default(VirtualScrollFlattenedItem)));
        }
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, items, startIndex));
    }

    public void NotifyItemChanged(int index)
    {
        var item = new VirtualScrollItemWrapper(index, _flattenedAdapter.GetItem(index));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, item, index));
    }

    public void NotifyItemRangeChanged(int startIndex, int count)
    {
        var items = new List<VirtualScrollItemWrapper>();
        for (var i = startIndex; i < startIndex + count; i++)
        {
            items.Add(new VirtualScrollItemWrapper(i, _flattenedAdapter.GetItem(i)));
        }
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, items, items, startIndex));
    }

    public void NotifyItemMoved(int oldIndex, int newIndex)
    {
        var item = new VirtualScrollItemWrapper(newIndex, _flattenedAdapter.GetItem(newIndex));
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, item, newIndex, oldIndex));
    }

    public IEnumerator<VirtualScrollItemWrapper> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return new VirtualScrollItemWrapper(i, _flattenedAdapter.GetItem(i));
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
