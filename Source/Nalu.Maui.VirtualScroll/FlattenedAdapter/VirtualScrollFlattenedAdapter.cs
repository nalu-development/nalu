using System.Runtime.CompilerServices;

namespace Nalu;

/// <summary>
/// Implementation of VirtualScrollFlattenedAdapter that uses cached section offsets
/// instead of storing a full flattened array. This reduces memory usage but GetItem is O(log n) instead of O(1).
/// </summary>
internal class VirtualScrollFlattenedAdapter : IVirtualScrollFlattenedAdapter, IDisposable
{
    private readonly IVirtualScrollAdapter _virtualScrollAdapter;
    private readonly IDisposable _subscription;
    private IVirtualScrollLayoutInfo _layoutInfo;
    private int _flattenedLength;
    private int[] _sectionOffsets; // Cumulative offsets for O(1) section start lookup
    private int _sectionCount; // Actual number of sections (array may be larger due to capacity)
    private bool _hasGlobalHeader; // Cached layout flags
    private bool _hasGlobalFooter;
    private bool _hasSectionHeader;
    private bool _hasSectionFooter;
    private int _globalFooterHeaderSize;
    private int _sectionFooterHeaderSize;
    private readonly List<Action<VirtualScrollFlattenedChangeSet>> _subscribers = [];

    public VirtualScrollFlattenedAdapter(IVirtualScrollAdapter virtualScrollAdapter, IVirtualScrollLayoutInfo layoutInfo)
    {
        _virtualScrollAdapter = virtualScrollAdapter;
        _layoutInfo = layoutInfo;
        _subscription = virtualScrollAdapter.Subscribe(OnAdapterChanged);
        _sectionOffsets = Array.Empty<int>();
        _flattenedLength = 0;
        CacheLayoutFlags();
        RebuildOffsets();
    }

    /// <summary>
    /// Sets the layout header / footer information.
    /// </summary>
    public void ChangeLayoutInfo(IVirtualScrollLayoutInfo layoutInfo)
    {
        if (_layoutInfo.Equals(layoutInfo))
        {
            return;
        }

        var oldLength = _flattenedLength;
        _layoutInfo = layoutInfo;
        CacheLayoutFlags();
        RebuildOffsets();

        if (oldLength != _flattenedLength)
        {
            NotifySubscribers(new VirtualScrollFlattenedChangeSet(new[] { VirtualScrollFlattenedChangeFactory.Reset() }));
        }
    }

    private void CacheLayoutFlags()
    {
        _hasGlobalHeader = _layoutInfo.HasGlobalHeader;
        _hasGlobalFooter = _layoutInfo.HasGlobalFooter;
        _hasSectionHeader = _layoutInfo.HasSectionHeader;
        _hasSectionFooter = _layoutInfo.HasSectionFooter;
        _globalFooterHeaderSize = (_hasGlobalHeader ? 1 : 0) + (_hasGlobalFooter ? 1 : 0);
        _sectionFooterHeaderSize = (_hasSectionHeader ? 1 : 0) + (_hasSectionFooter ? 1 : 0);
    }

    public int GetItemCount() => _flattenedLength;

    public VirtualScrollFlattenedItem GetItem(int flattenedIndex)
    {
        if (flattenedIndex < 0 || flattenedIndex >= _flattenedLength)
        {
            throw new ArgumentOutOfRangeException(nameof(flattenedIndex));
        }

        // Handle global header
        if (_hasGlobalHeader)
        {
            if (flattenedIndex == 0)
            {
                return new VirtualScrollFlattenedItem(VirtualScrollFlattenedPositionType.GlobalHeader, null);
            }
            flattenedIndex--; // Adjust for global header
        }

        // Find which section this flattened index belongs to using binary search
        var sectionIndex = FindSectionForFlattenedIndex(flattenedIndex);
        if (sectionIndex < 0)
        {
            // Must be global footer
            return new VirtualScrollFlattenedItem(VirtualScrollFlattenedPositionType.GlobalFooter, null);
        }

        var sectionStartOffset = _sectionOffsets[sectionIndex];
        var relativeIndex = flattenedIndex - sectionStartOffset;

        // Check if it's section header
        if (_hasSectionHeader)
        {
            if (relativeIndex == 0)
            {
                return new VirtualScrollFlattenedItem(VirtualScrollFlattenedPositionType.SectionHeader, _virtualScrollAdapter.GetSection(sectionIndex));
            }
            relativeIndex--;
        }

        var itemCount = _virtualScrollAdapter.GetItemCount(sectionIndex);
        
        // Check if it's within items
        if (relativeIndex < itemCount)
        {
            return new VirtualScrollFlattenedItem(VirtualScrollFlattenedPositionType.Item, _virtualScrollAdapter.GetItem(sectionIndex, relativeIndex));
        }

        // Must be section footer
        return new VirtualScrollFlattenedItem(VirtualScrollFlattenedPositionType.SectionFooter, _virtualScrollAdapter.GetSection(sectionIndex));
    }

    public IDisposable Subscribe(Action<VirtualScrollFlattenedChangeSet> changeCallback)
    {
        _subscribers.Add(changeCallback);
        return new Subscription(this, changeCallback);
    }

    public void Dispose() => _subscription.Dispose();
    
    private void OnAdapterChanged(VirtualScrollChangeSet changeSet)
    {
        var flattenedChanges = new List<VirtualScrollFlattenedChange>();

        foreach (var change in changeSet.Changes)
        {
            flattenedChanges.AddRange(ConvertChange(change));
        }

        if (flattenedChanges.Count > 0)
        {
            NotifySubscribers(new VirtualScrollFlattenedChangeSet(flattenedChanges));
        }
    }

    private IEnumerable<VirtualScrollFlattenedChange> ConvertChange(VirtualScrollChange change)
    {
        if (change.Operation == VirtualScrollChangeOperation.Reset)
        {
            RebuildOffsets();
            return new[] { VirtualScrollFlattenedChangeFactory.Reset() };
        }

        if (change.IsSectionChange)
        {
            return ConvertSectionChange(change);
        }

        return ConvertItemChange(change);
    }

    private IEnumerable<VirtualScrollFlattenedChange> ConvertSectionChange(VirtualScrollChange change)
    {
        var flattenedChanges = new List<VirtualScrollFlattenedChange>();

        switch (change.Operation)
        {
            case VirtualScrollChangeOperation.InsertSection:
            case VirtualScrollChangeOperation.InsertSectionRange:
                {
                    var startFlattenedIndex = GetFlattenedIndexForSectionStart(change.StartSectionIndex);
                    var sectionCount = change.EndSectionIndex - change.StartSectionIndex + 1;
                    var itemsToInsert = CalculateItemsForSections(change.StartSectionIndex, sectionCount);
                    UpdateOffsetsAfterSectionInsert(change.StartSectionIndex, sectionCount);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.InsertItemRange(startFlattenedIndex, startFlattenedIndex + itemsToInsert - 1));
                    break;
                }

            case VirtualScrollChangeOperation.RemoveSection:
            case VirtualScrollChangeOperation.RemoveSectionRange:
                {
                    var startFlattenedIndex = GetFlattenedIndexForSectionStart(change.StartSectionIndex);
                    var sectionCount = change.EndSectionIndex - change.StartSectionIndex + 1;
                    var itemsToRemove = CalculateItemsForSections(change.StartSectionIndex, sectionCount);
                    UpdateOffsetsAfterSectionRemove(change.StartSectionIndex, sectionCount);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.RemoveItemRange(startFlattenedIndex, startFlattenedIndex + itemsToRemove - 1));
                    break;
                }

            case VirtualScrollChangeOperation.ReplaceSection:
            case VirtualScrollChangeOperation.ReplaceSectionRange:
                {
                    // ReplaceSection(Range) means content changed but structure is the same
                    // Emit ReplaceItemRange for the affected items
                    var startFlattenedIndex = GetFlattenedIndexForSectionStart(change.StartSectionIndex);
                    var sectionCount = change.EndSectionIndex - change.StartSectionIndex + 1;
                    var itemsToReplace = CalculateItemsForSections(change.StartSectionIndex, sectionCount);
                    
                    if (itemsToReplace == 1)
                    {
                        flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.ReplaceItem(startFlattenedIndex));
                    }
                    else
                    {
                        flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.ReplaceItemRange(startFlattenedIndex, startFlattenedIndex + itemsToReplace - 1));
                    }
                    break;
                }

            case VirtualScrollChangeOperation.MoveSection:
                {
                    // MoveSection: remove from old position, insert at new position
                    var fromSectionIndex = change.StartSectionIndex;
                    var toSectionIndex = change.EndSectionIndex;
                    
                    // Calculate positions BEFORE any changes
                    var fromFlattenedIndex = GetFlattenedIndexForSectionStart(fromSectionIndex);
                    var sectionSize = CalculateItemsForSections(fromSectionIndex, 1);
                    
                    // Calculate destination index
                    // If moving forward, destination shifts back after removal
                    // If moving backward, destination stays the same
                    int toFlattenedIndex;
                    if (fromSectionIndex < toSectionIndex)
                    {
                        // Moving forward: get position of section AFTER the target
                        // After removal, target position shifts back by sectionSize
                        toFlattenedIndex = GetFlattenedIndexForSectionStart(toSectionIndex + 1) - sectionSize;
                    }
                    else
                    {
                        // Moving backward: destination is the start of the target section
                        toFlattenedIndex = GetFlattenedIndexForSectionStart(toSectionIndex);
                    }
                    
                    // Update offsets to reflect the move
                    RebuildOffsets();
                    
                    // Emit remove and insert changes
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.RemoveItemRange(fromFlattenedIndex, fromFlattenedIndex + sectionSize - 1));
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.InsertItemRange(toFlattenedIndex, toFlattenedIndex + sectionSize - 1));
                    break;
                }

            case VirtualScrollChangeOperation.RefreshSection:
                {
                    var startFlattenedIndex = GetFlattenedIndexForSectionStart(change.StartSectionIndex);
                    var itemsToRefresh = CalculateItemsForSections(change.StartSectionIndex, 1);
                    
                    if (itemsToRefresh == 1)
                    {
                        flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.RefreshItem(startFlattenedIndex));
                    }
                    else
                    {
                        flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.ReplaceItemRange(startFlattenedIndex, startFlattenedIndex + itemsToRefresh - 1));
                    }
                    break;
                }
        }

        return flattenedChanges;
    }

    private IEnumerable<VirtualScrollFlattenedChange> ConvertItemChange(VirtualScrollChange change)
    {
        var flattenedChanges = new List<VirtualScrollFlattenedChange>();

        switch (change.Operation)
        {
            case VirtualScrollChangeOperation.InsertItem:
                {
                    var flattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    UpdateOffsetsAfterItemInsert(change.StartSectionIndex);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.InsertItem(flattenedIndex));
                    break;
                }

            case VirtualScrollChangeOperation.InsertItemRange:
                {
                    var startFlattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    var count = change.EndItemIndex - change.StartItemIndex + 1;
                    UpdateOffsetsAfterItemInsert(change.StartSectionIndex, count);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.InsertItemRange(startFlattenedIndex, startFlattenedIndex + count - 1));
                    break;
                }

            case VirtualScrollChangeOperation.RemoveItem:
                {
                    var flattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    UpdateOffsetsAfterItemRemove(change.StartSectionIndex);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.RemoveItem(flattenedIndex));
                    break;
                }

            case VirtualScrollChangeOperation.RemoveItemRange:
                {
                    var startFlattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    var count = change.EndItemIndex - change.StartItemIndex + 1;
                    UpdateOffsetsAfterItemRemove(change.StartSectionIndex, count);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.RemoveItemRange(startFlattenedIndex, startFlattenedIndex + count - 1));
                    break;
                }

            case VirtualScrollChangeOperation.ReplaceItem:
                {
                    var flattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.ReplaceItem(flattenedIndex));
                    break;
                }

            case VirtualScrollChangeOperation.ReplaceItemRange:
                {
                    var startFlattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    var count = change.EndItemIndex - change.StartItemIndex + 1;
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.ReplaceItemRange(startFlattenedIndex, startFlattenedIndex + count - 1));
                    break;
                }

            case VirtualScrollChangeOperation.MoveItem:
                {
                    var fromFlattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    var toFlattenedIndex = GetFlattenedIndexForItem(change.EndSectionIndex, change.EndItemIndex);
                    // Note: MoveItem doesn't change offsets if within same section, but we need to rebuild offsets if cross-section
                    if (change.StartSectionIndex != change.EndSectionIndex)
                    {
                        UpdateOffsetsAfterItemRemove(change.StartSectionIndex);
                        UpdateOffsetsAfterItemInsert(change.EndSectionIndex);
                    }
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.MoveItem(fromFlattenedIndex, toFlattenedIndex));
                    break;
                }

            case VirtualScrollChangeOperation.RefreshItem:
                {
                    var flattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.RefreshItem(flattenedIndex));
                    break;
                }
        }

        return flattenedChanges;
    }

    private void RebuildOffsets()
    {
        _sectionCount = _virtualScrollAdapter.GetSectionCount();
        
        // Ensure arrays are large enough (double capacity for growth)
        if (_sectionOffsets.Length < _sectionCount)
        {
            _sectionOffsets = new int[Math.Max(_sectionCount * 2, 4)];
        }

        _flattenedLength = 0;

        // Global header
        var globalHeaderOffset = 0;
        if (_hasGlobalHeader)
        {
            globalHeaderOffset = 1;
            _flattenedLength++;
        }

        // Build section offsets
        // Offsets are stored relative to after global header (for use in GetItem after adjusting for global header)
        for (var sectionIndex = 0; sectionIndex < _sectionCount; sectionIndex++)
        {
            // Store offset relative to after global header
            _sectionOffsets[sectionIndex] = _flattenedLength - globalHeaderOffset;
            
            var itemCount = _virtualScrollAdapter.GetItemCount(sectionIndex);
            
            // Section header & footer
            _flattenedLength += itemCount + _sectionFooterHeaderSize;
        }

        // Global footer
        if (_hasGlobalFooter)
        {
            _flattenedLength++;
        }
    }

    /// <inheritdoc/>
    public int GetFlattenedIndexForSectionStart(int sectionIndex)
    {
        if (sectionIndex < 0)
        {
            return -1;
        }

        var sectionCount = _virtualScrollAdapter.GetSectionCount();
        if (sectionIndex >= sectionCount)
        {
            return -1;
        }

        // Use cached offsets for O(1) lookup
        // Offsets are stored relative to after global header
        // Returns the index OF the section start (i.e., the section header position, or first item if no header)
        if (sectionIndex < _sectionCount)
        {
            var offset = _sectionOffsets[sectionIndex];
            return _hasGlobalHeader ? offset + 1 : offset;
        }

        // Fallback calculation for sections beyond current count (e.g., during insert)
        var flattenedIndex = _hasGlobalHeader ? 1 : 0;
        var sectionsToIterate = Math.Min(sectionIndex, sectionCount);
        
        for (var i = 0; i < sectionsToIterate; i++)
        {
            flattenedIndex += _virtualScrollAdapter.GetItemCount(i) + _sectionFooterHeaderSize;
        }
        return flattenedIndex;
    }

    /// <inheritdoc/>
    public int GetFlattenedIndexForItem(int sectionIndex, int itemIndex)
    {
        if (sectionIndex < 0)
        {
            return -1;
        }

        var sectionCount = _virtualScrollAdapter.GetSectionCount();
        if (sectionIndex >= sectionCount)
        {
            return -1;
        }

        // If itemIndex is -1, return the section header index
        if (itemIndex == -1)
        {
            return GetFlattenedIndexForSectionStart(sectionIndex);
        }

        if (itemIndex < 0)
        {
            return -1;
        }

        var itemCount = _virtualScrollAdapter.GetItemCount(sectionIndex);
        if (itemIndex >= itemCount)
        {
            return -1;
        }

        var flattenedIndex = GetFlattenedIndexForSectionStart(sectionIndex);
        // GetFlattenedIndexForSectionStart returns the section start (header position),
        // so we need to add the section header offset to get to items
        if (_hasSectionHeader)
        {
            flattenedIndex++;
        }
        return flattenedIndex + itemIndex;
    }

    private int CalculateItemsForSections(int startSectionIndex, int sectionCount)
    {
        var totalItems = 0;
        for (var i = 0; i < sectionCount; i++)
        {
            var sectionIndex = startSectionIndex + i;
            totalItems += _virtualScrollAdapter.GetItemCount(sectionIndex) + _sectionFooterHeaderSize;
        }
        return totalItems;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindSectionForFlattenedIndex(int flattenedIndex)
    {
        // Binary search for the section containing this flattened index
        var left = 0;
        var right = _sectionCount - 1;

        while (left <= right)
        {
            var sectionIndex = (left + right) / 2;
            var sectionStart = _sectionOffsets[sectionIndex];
            var sectionSize = GetCurrentSectionSize(sectionIndex, sectionStart);
            var sectionEnd = sectionStart + sectionSize - 1;

            if (flattenedIndex >= sectionStart && flattenedIndex <= sectionEnd)
            {
                return sectionIndex;
            }

            if (flattenedIndex < sectionStart)
            {
                right = sectionIndex - 1;
            }
            else
            {
                left = sectionIndex + 1;
            }
        }

        return -1; // Not found (must be global footer)
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetCurrentSectionSize(int mid, int sectionStart)
        => mid < _sectionCount - 1
            ? _sectionOffsets[mid + 1] - sectionStart
            : _flattenedLength - sectionStart - _globalFooterHeaderSize;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CalculateSectionSize(int sectionIndex)
    {
         var size = _virtualScrollAdapter.GetItemCount(sectionIndex) + _sectionFooterHeaderSize;
         return size;
    }

    private void UpdateOffsetsAfterSectionInsert(int startSectionIndex, int insertedSectionCount)
    {
        var newSectionTotal = _virtualScrollAdapter.GetSectionCount();
        
        // Ensure arrays are large enough
        if (_sectionOffsets.Length < newSectionTotal)
        {
            var newOffsets = new int[Math.Max(newSectionTotal * 2, _sectionOffsets.Length * 2)];
            Array.Copy(_sectionOffsets, 0, newOffsets, 0, startSectionIndex);
            _sectionOffsets = newOffsets;
        }

        // Calculate offset for first inserted section (relative to after global header)
        var insertOffset = 0;
        if (startSectionIndex > 0)
        {
            insertOffset = _sectionOffsets[startSectionIndex - 1] + CalculateSectionSize(startSectionIndex - 1);
        }
        
        // Shift existing offsets
        var oldSectionsToShift = _sectionCount - startSectionIndex;
        if (oldSectionsToShift > 0)
        {
            Array.Copy(_sectionOffsets, startSectionIndex, _sectionOffsets, startSectionIndex + insertedSectionCount, oldSectionsToShift);
        }

        // Insert new offsets
        var currentOffset = insertOffset;
        for (var i = 0; i < insertedSectionCount; i++)
        {
            var sectionIndex = startSectionIndex + i;
            _sectionOffsets[sectionIndex] = currentOffset;
            currentOffset += CalculateSectionSize(sectionIndex);
        }

        // Update offsets for subsequent sections
        for (var i = startSectionIndex + insertedSectionCount; i < newSectionTotal; i++)
        {
            _sectionOffsets[i] = currentOffset;
            currentOffset += CalculateSectionSize(i);
        }

        // Update section count
        _sectionCount = newSectionTotal;

        // Recalculate total length (add global header offset back)
        _flattenedLength = currentOffset + (_hasGlobalHeader ? 1 : 0);
        if (_hasGlobalFooter)
        {
            _flattenedLength++;
        }
    }

    private void UpdateOffsetsAfterSectionRemove(int startSectionIndex, int removedSectionCount)
    {
        var newSectionTotal = _virtualScrollAdapter.GetSectionCount();

        // Shift offsets backward
        var sectionsAfterRemoval = _sectionCount - startSectionIndex - removedSectionCount;
        if (sectionsAfterRemoval > 0)
        {
            Array.Copy(_sectionOffsets, startSectionIndex + removedSectionCount, _sectionOffsets, startSectionIndex, sectionsAfterRemoval);
        }

        // Update section count
        _sectionCount = newSectionTotal;

        // Recalculate offsets for remaining sections (relative to after global header)
        var currentOffset = 0;
        for (var i = 0; i < _sectionCount; i++)
        {
            _sectionOffsets[i] = currentOffset;
            currentOffset += CalculateSectionSize(i);
        }

        // Recalculate total length (add global header offset back)
        _flattenedLength = currentOffset + (_hasGlobalHeader ? 1 : 0);
        if (_hasGlobalFooter)
        {
            _flattenedLength++;
        }
    }

    private void UpdateOffsetsAfterItemInsert(int sectionIndex, int count = 1)
    {
        // Update offsets for subsequent sections
        for (var i = sectionIndex + 1; i < _sectionCount; i++)
        {
            _sectionOffsets[i] += count;
        }

        // Update total length
        _flattenedLength += count;
    }

    private void UpdateOffsetsAfterItemRemove(int sectionIndex, int count = 1)
    {
        // Update offsets for subsequent sections
        for (var i = sectionIndex + 1; i < _sectionCount; i++)
        {
            _sectionOffsets[i] -= count;
        }

        // Update total length
        _flattenedLength -= count;
    }

    private void NotifySubscribers(VirtualScrollFlattenedChangeSet changeSet)
    {
        foreach (var subscriber in _subscribers)
        {
            subscriber(changeSet);
        }
    }

    private void Unsubscribe(Action<VirtualScrollFlattenedChangeSet> callback) => _subscribers.Remove(callback);

    private sealed class Subscription : IDisposable
    {
        private readonly VirtualScrollFlattenedAdapter _adapter;
        private readonly Action<VirtualScrollFlattenedChangeSet> _callback;
        private bool _disposed;

        public Subscription(VirtualScrollFlattenedAdapter adapter, Action<VirtualScrollFlattenedChangeSet> callback)
        {
            _adapter = adapter;
            _callback = callback;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _adapter.Unsubscribe(_callback);
                _disposed = true;
            }
        }
    }
}

