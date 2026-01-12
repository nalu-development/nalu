using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace Nalu;

/// <summary>
/// Data source for Windows ItemsRepeater that wraps the flattened adapter.
/// </summary>
internal class VirtualScrollItemsSource : IReadOnlyList<int>, INotifyCollectionChanged
{
    private readonly IVirtualScrollFlattenedAdapter _flattenedAdapter;

    public VirtualScrollItemsSource(IVirtualScrollFlattenedAdapter flattenedAdapter)
    {
        _flattenedAdapter = flattenedAdapter;
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public int Count => _flattenedAdapter.GetItemCount();

    public int this[int index] => index; // ItemsRepeater uses index as data, we'll use flattened index

    public void NotifyReset()
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public void NotifyItemInserted(int index)
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, index, index));
    }

    public void NotifyItemRangeInserted(int startIndex, int count)
    {
        var items = new List<int>();
        for (var i = startIndex; i < startIndex + count; i++)
        {
            items.Add(i);
        }
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items, startIndex));
    }

    public void NotifyItemRemoved(int index)
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, index, index));
    }

    public void NotifyItemRangeRemoved(int startIndex, int count)
    {
        var items = new List<int>();
        for (var i = startIndex; i < startIndex + count; i++)
        {
            items.Add(i);
        }
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, items, startIndex));
    }

    public void NotifyItemChanged(int index)
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, index, index));
    }

    public void NotifyItemRangeChanged(int startIndex, int count)
    {
        var items = new List<int>();
        for (var i = startIndex; i < startIndex + count; i++)
        {
            items.Add(i);
        }
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, items, items, startIndex));
    }

    public void NotifyItemMoved(int oldIndex, int newIndex)
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, newIndex, oldIndex, newIndex));
    }

    public IEnumerator<int> GetEnumerator()
    {
        for (var i = 0; i < Count; i++)
        {
            yield return i;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
