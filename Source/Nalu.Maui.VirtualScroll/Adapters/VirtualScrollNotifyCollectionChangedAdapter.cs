using System.Collections;
using System.Collections.Specialized;

namespace Nalu;

/// <summary>
/// An adapter that wraps an observable collection for use with <see cref="VirtualScroll"/>.
/// </summary>
/// <typeparam name="TItemCollection">The type of the observable collection.</typeparam>
public class VirtualScrollNotifyCollectionChangedAdapter<TItemCollection> : IVirtualScrollAdapter
    where TItemCollection : IList, INotifyCollectionChanged
{
    private readonly TItemCollection _collection;
    private const int _sectionIndex = 0;

    /// <summary>
    /// The underlying observable collection.
    /// </summary>
    protected TItemCollection Collection => _collection;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollNotifyCollectionChangedAdapter{TItemCollection}" /> class based on the specified observable collection.
    /// </summary>
    public VirtualScrollNotifyCollectionChangedAdapter(TItemCollection collection)
    {
        _collection = collection ?? throw new ArgumentNullException(nameof(collection));
    }

    /// <inheritdoc/>
    public int GetSectionCount() => _collection.Count > 0 ? 1 : 0;

    /// <inheritdoc/>
    public int GetItemCount(int sectionIndex) => _collection.Count;

    /// <inheritdoc/>
    public object? GetSection(int sectionIndex) => null;

    /// <inheritdoc/>
    public object? GetItem(int sectionIndex, int itemIndex) => _collection[itemIndex];

    /// <inheritdoc/>
    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback) => new ObservableCollectionAdapterSubscription(_collection, changeCallback, ShouldIgnoreCollectionChanges);

    /// <summary>
    /// Tells if the adapter should ignore collection changes, therefore not notifying subscribers.
    /// </summary>
    /// <returns></returns>
    protected virtual bool ShouldIgnoreCollectionChanges() => false;
    
    private sealed class ObservableCollectionAdapterSubscription : IDisposable
    {
        private readonly TItemCollection _collection;
        private readonly Action<VirtualScrollChangeSet> _changeCallback;
        private readonly Func<bool> _shouldIgnoreCollectionChanges;
        private bool _disposed;
        private bool _isEmpty;

        public ObservableCollectionAdapterSubscription(TItemCollection collection, Action<VirtualScrollChangeSet> changeCallback, Func<bool> shouldIgnoreCollectionChanges)
        {
            _collection = collection;
            _changeCallback = changeCallback;
            _shouldIgnoreCollectionChanges = shouldIgnoreCollectionChanges;
            _isEmpty = _collection.Count == 0;
            _collection.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_disposed || _shouldIgnoreCollectionChanges())
            {
                return;
            }

            var changes = new List<VirtualScrollChange>();

            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    if (e.NewItems is null || e.NewItems.Count == 0)
                    {
                        break;
                    }

                    // If transitioning from empty to non-empty, insert section first
                    if (_isEmpty)
                    {
                        changes.Add(VirtualScrollChangeFactory.InsertSection(_sectionIndex));
                        _isEmpty = false;
                        // Don't emit InsertItem - the section insert already includes the items
                    }
                    else
                    {
                        // Section already exists, just insert the items
                        if (e.NewItems.Count == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.InsertItem(_sectionIndex, e.NewStartingIndex));
                        }
                        else
                        {
                            var endIndex = e.NewStartingIndex + e.NewItems.Count - 1;
                            changes.Add(VirtualScrollChangeFactory.InsertItemRange(_sectionIndex, e.NewStartingIndex, endIndex));
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is null || e.OldItems.Count == 0)
                    {
                        break;
                    }

                    // Collection has already been updated, so check current count
                    var isEmptyAfterRemove = _collection.Count == 0;
                    // If transitioning from non-empty to empty, remove section after items
                    if (isEmptyAfterRemove && !_isEmpty)
                    {
                        changes.Add(VirtualScrollChangeFactory.RemoveSection(_sectionIndex));
                        _isEmpty = true;
                        break;
                    }

                    if (e.OldItems.Count == 1)
                    {
                        changes.Add(VirtualScrollChangeFactory.RemoveItem(_sectionIndex, e.OldStartingIndex));
                    }
                    else
                    {
                        var endIndex = e.OldStartingIndex + e.OldItems.Count - 1;
                        changes.Add(VirtualScrollChangeFactory.RemoveItemRange(_sectionIndex, e.OldStartingIndex, endIndex));
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.NewItems is null || e.OldItems is null || e.NewItems.Count == 0 || e.OldItems.Count == 0)
                    {
                        break;
                    }

                    var willBeEmptyAfterReplace = _collection.Count == 0;

                    var replaceCount = Math.Min(e.NewItems.Count, e.OldItems.Count);
                    var startIndex = e.NewStartingIndex;

                    // Replace the overlapping items
                    if (replaceCount == 1)
                    {
                        changes.Add(VirtualScrollChangeFactory.ReplaceItem(_sectionIndex, startIndex));
                    }
                    else if (replaceCount > 1)
                    {
                        var replaceEndIndex = startIndex + replaceCount - 1;
                        changes.Add(VirtualScrollChangeFactory.ReplaceItemRange(_sectionIndex, startIndex, replaceEndIndex));
                    }

                    // Add remaining new items if there are more new items than old items
                    if (e.NewItems.Count > e.OldItems.Count)
                    {
                        var remainingNewCount = e.NewItems.Count - e.OldItems.Count;
                        var insertStartIndex = startIndex + replaceCount;
                        
                        // Check if this will transition from empty to non-empty
                        if (_isEmpty)
                        {
                            changes.Add(VirtualScrollChangeFactory.InsertSection(_sectionIndex));
                            _isEmpty = false;
                        }
                        
                        if (remainingNewCount == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.InsertItem(_sectionIndex, insertStartIndex));
                        }
                        else
                        {
                            var insertEndIndex = insertStartIndex + remainingNewCount - 1;
                            changes.Add(VirtualScrollChangeFactory.InsertItemRange(_sectionIndex, insertStartIndex, insertEndIndex));
                        }
                    }
                    // Remove remaining old items if there are more old items than new items
                    else if (e.OldItems.Count > e.NewItems.Count)
                    {
                        var remainingOldCount = e.OldItems.Count - e.NewItems.Count;
                        var removeStartIndex = startIndex + replaceCount;
                        if (remainingOldCount == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.RemoveItem(_sectionIndex, removeStartIndex));
                        }
                        else
                        {
                            var removeEndIndex = removeStartIndex + remainingOldCount - 1;
                            changes.Add(VirtualScrollChangeFactory.RemoveItemRange(_sectionIndex, removeStartIndex, removeEndIndex));
                        }
                        
                        // Check if this will transition from non-empty to empty
                        if (willBeEmptyAfterReplace && !_isEmpty)
                        {
                            changes.Add(VirtualScrollChangeFactory.RemoveSection(_sectionIndex));
                            _isEmpty = true;
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is null || e.OldItems.Count == 0)
                    {
                        break;
                    }

                    if (e.OldItems.Count == 1)
                    {
                        changes.Add(VirtualScrollChangeFactory.MoveItem(_sectionIndex, e.OldStartingIndex, e.NewStartingIndex));
                    }
                    else
                    {
#if IOS
                        // On iOS, use individual MoveItem operations for each item
                        var itemCount = e.OldItems.Count;
                        var fromIndex = e.OldStartingIndex;
                        var toIndex = e.NewStartingIndex;
                        
                        // If moving forward, process from end to start to avoid index shifting issues
                        if (toIndex > fromIndex)
                        {
                            for (var i = itemCount - 1; i >= 0; i--)
                            {
                                changes.Add(VirtualScrollChangeFactory.MoveItem(_sectionIndex, fromIndex + i, toIndex + i));
                            }
                        }
                        else
                        {
                            // If moving backward, process from start to end
                            for (var i = 0; i < itemCount; i++)
                            {
                                changes.Add(VirtualScrollChangeFactory.MoveItem(_sectionIndex, fromIndex + i, toIndex + i));
                            }
                        }
#else
                        // For multiple items on non-iOS platforms, handle it as remove + insert
                        var endIndex = e.OldStartingIndex + e.OldItems.Count - 1;
                        changes.Add(VirtualScrollChangeFactory.RemoveItemRange(_sectionIndex, e.OldStartingIndex, endIndex));
                        var insertEndIndex = e.NewStartingIndex + e.OldItems.Count - 1;
                        changes.Add(VirtualScrollChangeFactory.InsertItemRange(_sectionIndex, e.NewStartingIndex, insertEndIndex));
#endif
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    _isEmpty = _collection.Count == 0;
                    changes.Add(VirtualScrollChangeFactory.Reset());
                    break;
            }

            if (changes.Count > 0)
            {
                _changeCallback(new VirtualScrollChangeSet(changes));
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _collection.CollectionChanged -= OnCollectionChanged;
                _disposed = true;
            }
        }
    }
}
