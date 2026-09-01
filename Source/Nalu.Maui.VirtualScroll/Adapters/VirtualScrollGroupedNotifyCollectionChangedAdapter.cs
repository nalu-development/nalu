using System.Collections;
using System.Collections.Specialized;

namespace Nalu;

/// <summary>
/// An adapter that wraps a grouped observable collection for use with <see cref="VirtualScroll"/>.
/// </summary>
/// <typeparam name="TSectionCollection">The type of the sections' collection.</typeparam>
/// <typeparam name="TItemCollection">The type of the items collection within each section.</typeparam>
public class VirtualScrollGroupedNotifyCollectionChangedAdapter<TSectionCollection, TItemCollection> : IVirtualScrollAdapter
    where TSectionCollection : IList, INotifyCollectionChanged
    where TItemCollection : IList, INotifyCollectionChanged
{
    private readonly TSectionCollection _sections;
    private readonly Func<object, TItemCollection> _sectionItemsGetter;

    /// <summary>
    /// The underlying observable collection.
    /// </summary>
    protected TSectionCollection Sections => _sections;
    
    /// <summary>
    /// Gets the items collection for the specified section.
    /// </summary>
    protected TItemCollection GetSectionItems(int sectionIndex) => _sectionItemsGetter(_sections[sectionIndex] ?? throw new InvalidOperationException($"Section at index {sectionIndex} is null. All sections must be non-null."));

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollGroupedNotifyCollectionChangedAdapter{TSectionCollection,TItemCollection}" /> class.
    /// </summary>
    /// <param name="sections">The collection of sections.</param>
    /// <param name="sectionItemsGetter">A function that extracts the items collection from a section object.</param>
    public VirtualScrollGroupedNotifyCollectionChangedAdapter(TSectionCollection sections, Func<object, TItemCollection> sectionItemsGetter)
    {
        _sections = sections ?? throw new ArgumentNullException(nameof(sections));
        _sectionItemsGetter = sectionItemsGetter ?? throw new ArgumentNullException(nameof(sectionItemsGetter));
    }

    /// <inheritdoc/>
    public int GetSectionCount() => _sections.Count;

    /// <inheritdoc/>
    public int GetItemCount(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= _sections.Count)
        {
            return 0;
        }

        var section = _sections[sectionIndex] ?? throw new InvalidOperationException($"Section at index {sectionIndex} is null. All sections must be non-null.");
        var items = _sectionItemsGetter(section) ?? throw new InvalidOperationException($"The sectionItemsGetter returned null for section at index {sectionIndex}. The function must return a valid items collection for each section.");
        return items.Count;
    }

    /// <inheritdoc/>
    public object? GetSection(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= _sections.Count)
        {
            return null;
        }

        return _sections[sectionIndex];
    }

    /// <inheritdoc/>
    public object? GetItem(int sectionIndex, int itemIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= _sections.Count)
        {
            return null;
        }

        var section = _sections[sectionIndex] ?? throw new InvalidOperationException($"Section at index {sectionIndex} is null. All sections must be non-null.");
        var items = _sectionItemsGetter(section) ?? throw new InvalidOperationException($"The sectionItemsGetter returned null for section at index {sectionIndex}. The function must return a valid items collection for each section.");
        
        if (itemIndex < 0 || itemIndex >= items.Count)
        {
            return null;
        }

        return items[itemIndex];
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback) => new GroupedObservableCollectionAdapterSubscription(_sections, _sectionItemsGetter, changeCallback, ShouldIgnoreCollectionChanges);
    
    /// <summary>
    /// Tells if the adapter should ignore collection changes, therefore not notifying subscribers.
    /// </summary>
    /// <returns></returns>
    protected virtual bool ShouldIgnoreCollectionChanges() => false;
    
    private sealed class GroupedObservableCollectionAdapterSubscription : IDisposable
    {
        private readonly TSectionCollection _sections;
        private readonly Func<object, TItemCollection> _sectionItemsGetter;
        private readonly Action<VirtualScrollChangeSet> _changeCallback;
        private readonly Func<bool> _isDragging;
        private readonly List<ItemCollectionSubscription> _itemCollectionSubscriptions = [];
        private bool _disposed;

        public GroupedObservableCollectionAdapterSubscription(
            TSectionCollection sections,
            Func<object, TItemCollection> sectionItemsGetter,
            Action<VirtualScrollChangeSet> changeCallback,
            Func<bool> isDragging
        )
        {
            _sections = sections;
            _sectionItemsGetter = sectionItemsGetter;
            _changeCallback = changeCallback;
            _isDragging = isDragging;
            _sections.CollectionChanged += OnSectionsCollectionChanged;
            
            // Subscribe to all existing sections' item collections
            for (var i = 0; i < _sections.Count; i++)
            {
                _itemCollectionSubscriptions.Add(CreateSubscriptionForSection(i));
            }
        }

        private ItemCollectionSubscription CreateSubscriptionForSection(int sectionIndex)
        {
            var section = _sections[sectionIndex] ?? throw new InvalidOperationException($"Section at index {sectionIndex} is null. All sections must be non-null.");
            var items = _sectionItemsGetter(section) ?? throw new InvalidOperationException($"The sectionItemsGetter returned null for section at index {sectionIndex}. The function must return a valid items collection for each section.");
            return new ItemCollectionSubscription(sectionIndex, items, OnItemCollectionChanged);
        }

        private void UpdateSectionIndicesFrom(int startIndex)
        {
            for (var i = startIndex; i < _itemCollectionSubscriptions.Count; i++)
            {
                _itemCollectionSubscriptions[i].SectionIndex = i;
            }
        }

        private void OnSectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_disposed || _isDragging())
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

                    var addedStartIndex = e.NewStartingIndex;
                    var addedCount = e.NewItems.Count;

                    // Insert new subscriptions - list handles shifting automatically
                    for (var i = 0; i < addedCount; i++)
                    {
                        _itemCollectionSubscriptions.Insert(addedStartIndex + i, CreateSubscriptionForSection(addedStartIndex + i));
                    }

                    // Update section indices for shifted subscriptions
                    UpdateSectionIndicesFrom(addedStartIndex + addedCount);

                    if (addedCount == 1)
                    {
                        changes.Add(VirtualScrollChangeFactory.InsertSection(addedStartIndex));
                    }
                    else
                    {
                        var endIndex = addedStartIndex + addedCount - 1;
                        changes.Add(VirtualScrollChangeFactory.InsertSectionRange(addedStartIndex, endIndex));
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is null || e.OldItems.Count == 0)
                    {
                        break;
                    }

                    var removedStartIndex = e.OldStartingIndex;
                    var removedCount = e.OldItems.Count;

                    // Dispose and remove subscriptions - list handles shifting automatically
                    for (var i = removedCount - 1; i >= 0; i--)
                    {
                        _itemCollectionSubscriptions[removedStartIndex + i].Dispose();
                        _itemCollectionSubscriptions.RemoveAt(removedStartIndex + i);
                    }

                    // Update section indices for shifted subscriptions
                    UpdateSectionIndicesFrom(removedStartIndex);

                    if (removedCount == 1)
                    {
                        changes.Add(VirtualScrollChangeFactory.RemoveSection(removedStartIndex));
                    }
                    else
                    {
                        var endIndex = removedStartIndex + removedCount - 1;
                        changes.Add(VirtualScrollChangeFactory.RemoveSectionRange(removedStartIndex, endIndex));
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.NewItems is null || e.OldItems is null || e.NewItems.Count == 0 || e.OldItems.Count == 0)
                    {
                        break;
                    }

                    var replaceStartIndex = e.NewStartingIndex;
                    var replaceCount = Math.Min(e.NewItems.Count, e.OldItems.Count);

                    // Replace subscriptions (dispose old, create new)
                    for (var i = 0; i < replaceCount; i++)
                    {
                        _itemCollectionSubscriptions[replaceStartIndex + i].Dispose();
                        _itemCollectionSubscriptions[replaceStartIndex + i] = CreateSubscriptionForSection(replaceStartIndex + i);
                    }

                    // Handle remaining new sections if there are more new than old
                    if (e.NewItems.Count > e.OldItems.Count)
                    {
                        var remainingNewCount = e.NewItems.Count - e.OldItems.Count;
                        var insertStartIndex = replaceStartIndex + replaceCount;

                        for (var i = 0; i < remainingNewCount; i++)
                        {
                            _itemCollectionSubscriptions.Insert(insertStartIndex + i, CreateSubscriptionForSection(insertStartIndex + i));
                        }

                        UpdateSectionIndicesFrom(insertStartIndex + remainingNewCount);

                        if (replaceCount == 1 && remainingNewCount == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.ReplaceSection(replaceStartIndex));
                            changes.Add(VirtualScrollChangeFactory.InsertSection(insertStartIndex));
                        }
                        else
                        {
                            var replaceEndIndex = replaceStartIndex + replaceCount - 1;
                            changes.Add(VirtualScrollChangeFactory.ReplaceSectionRange(replaceStartIndex, replaceEndIndex));
                            var insertEndIndex = insertStartIndex + remainingNewCount - 1;
                            changes.Add(VirtualScrollChangeFactory.InsertSectionRange(insertStartIndex, insertEndIndex));
                        }
                    }
                    // Handle remaining old sections if there are more old than new
                    else if (e.OldItems.Count > e.NewItems.Count)
                    {
                        var remainingOldCount = e.OldItems.Count - e.NewItems.Count;
                        var removeStartIndex = replaceStartIndex + replaceCount;

                        for (var i = remainingOldCount - 1; i >= 0; i--)
                        {
                            _itemCollectionSubscriptions[removeStartIndex + i].Dispose();
                            _itemCollectionSubscriptions.RemoveAt(removeStartIndex + i);
                        }

                        UpdateSectionIndicesFrom(removeStartIndex);

                        if (replaceCount == 1 && remainingOldCount == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.ReplaceSection(replaceStartIndex));
                            changes.Add(VirtualScrollChangeFactory.RemoveSection(removeStartIndex));
                        }
                        else
                        {
                            var replaceEndIndex = replaceStartIndex + replaceCount - 1;
                            changes.Add(VirtualScrollChangeFactory.ReplaceSectionRange(replaceStartIndex, replaceEndIndex));
                            var removeEndIndex = removeStartIndex + remainingOldCount - 1;
                            changes.Add(VirtualScrollChangeFactory.RemoveSectionRange(removeStartIndex, removeEndIndex));
                        }
                    }
                    else
                    {
                        // Same count, just replace
                        if (replaceCount == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.ReplaceSection(replaceStartIndex));
                        }
                        else
                        {
                            var replaceEndIndex = replaceStartIndex + replaceCount - 1;
                            changes.Add(VirtualScrollChangeFactory.ReplaceSectionRange(replaceStartIndex, replaceEndIndex));
                        }
                    }
                    break;

                case NotifyCollectionChangedAction.Move:
                    if (e.OldItems is null || e.OldItems.Count == 0)
                    {
                        break;
                    }

                    var fromIndex = e.OldStartingIndex;
                    var toIndex = e.NewStartingIndex;
                    var moveCount = e.OldItems.Count;

                    // Extract subscriptions to move
                    var movedSubscriptions = new List<ItemCollectionSubscription>();
                    for (var i = moveCount - 1; i >= 0; i--)
                    {
                        movedSubscriptions.Insert(0, _itemCollectionSubscriptions[fromIndex + i]);
                        _itemCollectionSubscriptions.RemoveAt(fromIndex + i);
                    }

                    // Insert at new position
                    for (var i = 0; i < moveCount; i++)
                    {
                        _itemCollectionSubscriptions.Insert(toIndex + i, movedSubscriptions[i]);
                    }

                    // Update all affected section indices
                    var minAffectedIndex = Math.Min(fromIndex, toIndex);
                    UpdateSectionIndicesFrom(minAffectedIndex);

                    if (moveCount == 1)
                    {
                        changes.Add(VirtualScrollChangeFactory.MoveSection(fromIndex, toIndex));
                    }
                    else
                    {
                        // For multiple sections, use remove + insert
                        var removeEndIndex = fromIndex + moveCount - 1;
                        changes.Add(VirtualScrollChangeFactory.RemoveSectionRange(fromIndex, removeEndIndex));
                        var insertEndIndex = toIndex + moveCount - 1;
                        changes.Add(VirtualScrollChangeFactory.InsertSectionRange(toIndex, insertEndIndex));
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    // Dispose all subscriptions
                    foreach (var subscription in _itemCollectionSubscriptions)
                    {
                        subscription.Dispose();
                    }
                    _itemCollectionSubscriptions.Clear();

                    // Subscribe to all current sections
                    for (var i = 0; i < _sections.Count; i++)
                    {
                        _itemCollectionSubscriptions.Add(CreateSubscriptionForSection(i));
                    }

                    changes.Add(VirtualScrollChangeFactory.Reset());
                    break;
            }

            if (changes.Count > 0)
            {
                _changeCallback(new VirtualScrollChangeSet(changes));
            }
        }

        private void OnItemCollectionChanged(int sectionIndex, NotifyCollectionChangedEventArgs e)
        {
            if (_disposed || _isDragging())
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
                        changes.Add(VirtualScrollChangeFactory.InsertItem(sectionIndex, e.NewStartingIndex));
                    }
                    else
                    {
                        var endIndex = e.NewStartingIndex + e.NewItems.Count - 1;
                        changes.Add(VirtualScrollChangeFactory.InsertItemRange(sectionIndex, e.NewStartingIndex, endIndex));
                    }
                    break;

                case NotifyCollectionChangedAction.Remove:
                    if (e.OldItems is null || e.OldItems.Count == 0)
                    {
                        break;
                    }

                    if (e.OldItems.Count == 1)
                    {
                        changes.Add(VirtualScrollChangeFactory.RemoveItem(sectionIndex, e.OldStartingIndex));
                    }
                    else
                    {
                        var endIndex = e.OldStartingIndex + e.OldItems.Count - 1;
                        changes.Add(VirtualScrollChangeFactory.RemoveItemRange(sectionIndex, e.OldStartingIndex, endIndex));
                    }
                    break;

                case NotifyCollectionChangedAction.Replace:
                    if (e.NewItems is null || e.OldItems is null || e.NewItems.Count == 0 || e.OldItems.Count == 0)
                    {
                        break;
                    }

                    var replaceCount = Math.Min(e.NewItems.Count, e.OldItems.Count);
                    var startIndex = e.NewStartingIndex;

                    if (replaceCount == 1)
                    {
                        changes.Add(VirtualScrollChangeFactory.ReplaceItem(sectionIndex, startIndex));
                    }
                    else if (replaceCount > 1)
                    {
                        var replaceEndIndex = startIndex + replaceCount - 1;
                        changes.Add(VirtualScrollChangeFactory.ReplaceItemRange(sectionIndex, startIndex, replaceEndIndex));
                    }

                    // Add remaining new items if there are more new items than old items
                    if (e.NewItems.Count > e.OldItems.Count)
                    {
                        var remainingNewCount = e.NewItems.Count - e.OldItems.Count;
                        var insertStartIndex = startIndex + replaceCount;
                        if (remainingNewCount == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.InsertItem(sectionIndex, insertStartIndex));
                        }
                        else
                        {
                            var insertEndIndex = insertStartIndex + remainingNewCount - 1;
                            changes.Add(VirtualScrollChangeFactory.InsertItemRange(sectionIndex, insertStartIndex, insertEndIndex));
                        }
                    }
                    // Remove remaining old items if there are more old items than new items
                    else if (e.OldItems.Count > e.NewItems.Count)
                    {
                        var remainingOldCount = e.OldItems.Count - e.NewItems.Count;
                        var removeStartIndex = startIndex + replaceCount;
                        if (remainingOldCount == 1)
                        {
                            changes.Add(VirtualScrollChangeFactory.RemoveItem(sectionIndex, removeStartIndex));
                        }
                        else
                        {
                            var removeEndIndex = removeStartIndex + remainingOldCount - 1;
                            changes.Add(VirtualScrollChangeFactory.RemoveItemRange(sectionIndex, removeStartIndex, removeEndIndex));
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
                        changes.Add(VirtualScrollChangeFactory.MoveItem(sectionIndex, e.OldStartingIndex, e.NewStartingIndex));
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
                                changes.Add(VirtualScrollChangeFactory.MoveItem(sectionIndex, fromIndex + i, toIndex + i));
                            }
                        }
                        else
                        {
                            // If moving backward, process from start to end
                            for (var i = 0; i < itemCount; i++)
                            {
                                changes.Add(VirtualScrollChangeFactory.MoveItem(sectionIndex, fromIndex + i, toIndex + i));
                            }
                        }
#else
                        // For multiple items on non-iOS platforms, handle it as remove + insert
                        var endIndex = e.OldStartingIndex + e.OldItems.Count - 1;
                        changes.Add(VirtualScrollChangeFactory.RemoveItemRange(sectionIndex, e.OldStartingIndex, endIndex));
                        var insertEndIndex = e.NewStartingIndex + e.OldItems.Count - 1;
                        changes.Add(VirtualScrollChangeFactory.InsertItemRange(sectionIndex, e.NewStartingIndex, insertEndIndex));
#endif
                    }
                    break;

                case NotifyCollectionChangedAction.Reset:
                    changes.Add(VirtualScrollChangeFactory.ReplaceSection(sectionIndex));
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
                _sections.CollectionChanged -= OnSectionsCollectionChanged;
                
                foreach (var subscription in _itemCollectionSubscriptions)
                {
                    subscription.Dispose();
                }
                _itemCollectionSubscriptions.Clear();
                
                _disposed = true;
            }
        }
    }

    private sealed class ItemCollectionSubscription : IDisposable
    {
        private readonly TItemCollection _items;
        private readonly Action<int, NotifyCollectionChangedEventArgs> _changeCallback;
        private bool _disposed;

        public int SectionIndex { get; set; }

        public ItemCollectionSubscription(int sectionIndex, TItemCollection items, Action<int, NotifyCollectionChangedEventArgs> changeCallback)
        {
            SectionIndex = sectionIndex;
            _items = items;
            _changeCallback = changeCallback;
            _items.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_disposed)
            {
                _changeCallback(SectionIndex, e);
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _items.CollectionChanged -= OnCollectionChanged;
                _disposed = true;
            }
        }
    }
}

