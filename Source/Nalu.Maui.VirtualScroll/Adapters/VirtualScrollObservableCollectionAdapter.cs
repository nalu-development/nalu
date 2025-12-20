using System.Collections;
using System.Collections.Specialized;

namespace Nalu;

/// <summary>
/// An adapter that wraps an observable collection for use with <see cref="VirtualScroll"/>.
/// </summary>
/// <typeparam name="TItemCollection">The type of the observable collection.</typeparam>
public class VirtualScrollObservableCollectionAdapter<TItemCollection> : IVirtualScrollAdapter
    where TItemCollection : IList, INotifyCollectionChanged
{
    private readonly TItemCollection _collection;
    private const int _sectionIndex = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollObservableCollectionAdapter{TObservableCollection}" /> class based on the specified observable collection.
    /// </summary>
    public VirtualScrollObservableCollectionAdapter(TItemCollection collection)
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
    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback) => new ObservableCollectionAdapterSubscription(_collection, changeCallback);

    private sealed class ObservableCollectionAdapterSubscription : IDisposable
    {
        private readonly TItemCollection _collection;
        private readonly Action<VirtualScrollChangeSet> _changeCallback;
        private bool _disposed;

        public ObservableCollectionAdapterSubscription(TItemCollection collection, Action<VirtualScrollChangeSet> changeCallback)
        {
            _collection = collection;
            _changeCallback = changeCallback;
            _collection.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_disposed)
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

                    if (e.NewItems.Count == 1)
                    {
                        changes.Add(VirtualScrollChangeFactory.InsertItem(_sectionIndex, e.NewStartingIndex));
                    }
                    else
                    {
                        var endIndex = e.NewStartingIndex + e.NewItems.Count - 1;
                        changes.Add(VirtualScrollChangeFactory.InsertItemRange(_sectionIndex, e.NewStartingIndex, endIndex));
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is null || e.OldItems.Count == 0)
                    {
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

