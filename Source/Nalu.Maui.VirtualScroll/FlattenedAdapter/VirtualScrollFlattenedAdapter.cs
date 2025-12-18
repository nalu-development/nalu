namespace Nalu;


internal class VirtualScrollFlattenedAdapter : IVirtualScrollFlattenedAdapter, IDisposable
{
    private readonly IVirtualScrollAdapter _virtualScrollAdapter;
    private readonly IDisposable _subscription;
    private IVirtualScrollLayoutInfo _layoutInfo;
    private VirtualScrollFlattenedPositionInfo[] _flattenedArray;
    private int _flattenedLength;
    private int[] _sectionOffsets; // Cumulative offsets for O(1) section start lookup
    private readonly List<Action<VirtualScrollFlattenedChangeSet>> _subscribers = [];

    public VirtualScrollFlattenedAdapter(IVirtualScrollAdapter virtualScrollAdapter, IVirtualScrollLayoutInfo layoutInfo)
    {
        _virtualScrollAdapter = virtualScrollAdapter;
        _layoutInfo = layoutInfo;
        _subscription = virtualScrollAdapter.Subscribe(OnAdapterChanged);
        _flattenedArray = Array.Empty<VirtualScrollFlattenedPositionInfo>();
        _flattenedLength = 0;
        _sectionOffsets = Array.Empty<int>();
        RebuildFlattenedArray();
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
        RebuildFlattenedArray();

        if (oldLength != _flattenedLength)
        {
            NotifySubscribers(new VirtualScrollFlattenedChangeSet(new[] { VirtualScrollFlattenedChangeFactory.Reset() }));
        }
    }

    public int GetItemCount() => _flattenedLength;

    public VirtualScrollFlattenedItem GetItem(int flattenedIndex)
    {
        if (flattenedIndex < 0 || flattenedIndex >= _flattenedLength)
        {
            throw new ArgumentOutOfRangeException(nameof(flattenedIndex));
        }

        var positionInfo = _flattenedArray[flattenedIndex];
        var value = positionInfo.Type switch
        {
            VirtualScrollFlattenedPositionType.GlobalHeader => null,
            VirtualScrollFlattenedPositionType.GlobalFooter => null,
            VirtualScrollFlattenedPositionType.SectionHeader => _virtualScrollAdapter.GetSection(positionInfo.SectionIndex),
            VirtualScrollFlattenedPositionType.SectionFooter => _virtualScrollAdapter.GetSection(positionInfo.SectionIndex),
            VirtualScrollFlattenedPositionType.Item => _virtualScrollAdapter.GetItem(positionInfo.SectionIndex, positionInfo.ItemIndex),
            _ => throw new InvalidOperationException($"Unknown position type: {positionInfo.Type}")
        };

        return new VirtualScrollFlattenedItem(positionInfo.Type, value);
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
            RebuildFlattenedArray();
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
                    InsertIntoFlattenedArray(startFlattenedIndex, change.StartSectionIndex, sectionCount);
                    UpdateSectionIndicesAfterInsert(change.StartSectionIndex + sectionCount);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.InsertItemRange(startFlattenedIndex, startFlattenedIndex + itemsToInsert - 1));
                    break;
                }

            case VirtualScrollChangeOperation.RemoveSection:
            case VirtualScrollChangeOperation.RemoveSectionRange:
                {
                    var startFlattenedIndex = GetFlattenedIndexForSectionStart(change.StartSectionIndex);
                    var sectionCount = change.EndSectionIndex - change.StartSectionIndex + 1;
                    var itemsToRemove = CalculateItemsForSections(change.StartSectionIndex, sectionCount);
                    RemoveFromFlattenedArray(startFlattenedIndex, itemsToRemove);
                    RemoveSectionOffsets(change.StartSectionIndex, sectionCount);
                    UpdateSectionIndicesAfterRemove(change.StartSectionIndex);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.RemoveItemRange(startFlattenedIndex, startFlattenedIndex + itemsToRemove - 1));
                    break;
                }

            case VirtualScrollChangeOperation.ReplaceSection:
            case VirtualScrollChangeOperation.ReplaceSectionRange:
                {
                    // Replace operations may change item counts, so we rebuild to be safe
                    // This ensures correctness even if the section structure changed
                    RebuildFlattenedArray();
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.Reset());
                    break;
                }

            case VirtualScrollChangeOperation.MoveSection:
                {
                    // MoveSection is complex due to index shifting - rebuild for correctness
                    // This is a relatively rare operation, so the performance impact is acceptable
                    RebuildFlattenedArray();
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.Reset());
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
                    InsertItemIntoFlattenedArray(flattenedIndex, change.StartSectionIndex, change.StartItemIndex);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.InsertItem(flattenedIndex));
                    break;
                }

            case VirtualScrollChangeOperation.InsertItemRange:
                {
                    var startFlattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    var count = change.EndItemIndex - change.StartItemIndex + 1;
                    InsertItemRangeIntoFlattenedArray(startFlattenedIndex, change.StartSectionIndex, change.StartItemIndex, count);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.InsertItemRange(startFlattenedIndex, startFlattenedIndex + count - 1));
                    break;
                }

            case VirtualScrollChangeOperation.RemoveItem:
                {
                    var flattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    RemoveItemFromFlattenedArray(flattenedIndex);
                    flattenedChanges.Add(VirtualScrollFlattenedChangeFactory.RemoveItem(flattenedIndex));
                    break;
                }

            case VirtualScrollChangeOperation.RemoveItemRange:
                {
                    var startFlattenedIndex = GetFlattenedIndexForItem(change.StartSectionIndex, change.StartItemIndex);
                    var count = change.EndItemIndex - change.StartItemIndex + 1;
                    RemoveItemRangeFromFlattenedArray(startFlattenedIndex, count);
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
                    MoveItemInFlattenedArray(fromFlattenedIndex, toFlattenedIndex);
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

    private void RebuildFlattenedArray()
    {
        var sectionCount = _virtualScrollAdapter.GetSectionCount();
        var estimatedCapacity = EstimateCapacity(sectionCount);
        
        if (_flattenedArray.Length < estimatedCapacity)
        {
            var newArray = new VirtualScrollFlattenedPositionInfo[estimatedCapacity];
            if (_flattenedLength > 0)
            {
                Array.Copy(_flattenedArray, 0, newArray, 0, _flattenedLength);
            }
            _flattenedArray = newArray;
        }

        // Rebuild section offsets array
        if (_sectionOffsets.Length < sectionCount)
        {
            _sectionOffsets = new int[Math.Max(sectionCount, sectionCount * 2)];
        }

        _flattenedLength = 0;

        // Global header
        if (_layoutInfo.HasGlobalHeader)
        {
            EnsureCapacity(1);
            _flattenedArray[_flattenedLength++] = new VirtualScrollFlattenedPositionInfo
            {
                Type = VirtualScrollFlattenedPositionType.GlobalHeader,
                SectionIndex = -1,
                ItemIndex = -1
            };
        }

        // Sections
        for (var sectionIndex = 0; sectionIndex < sectionCount; sectionIndex++)
        {
            // Store the offset for this section start
            _sectionOffsets[sectionIndex] = _flattenedLength;

            // Section header
            if (_layoutInfo.HasSectionHeader)
            {
                EnsureCapacity(1);
                _flattenedArray[_flattenedLength++] = new VirtualScrollFlattenedPositionInfo
                {
                    Type = VirtualScrollFlattenedPositionType.SectionHeader,
                    SectionIndex = sectionIndex,
                    ItemIndex = -1
                };
            }

            // Section items
            var itemCount = _virtualScrollAdapter.GetItemCount(sectionIndex);
            EnsureCapacity(itemCount);
            for (var itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                _flattenedArray[_flattenedLength++] = new VirtualScrollFlattenedPositionInfo
                {
                    Type = VirtualScrollFlattenedPositionType.Item,
                    SectionIndex = sectionIndex,
                    ItemIndex = itemIndex
                };
            }

            // Section footer
            if (_layoutInfo.HasSectionFooter)
            {
                EnsureCapacity(1);
                _flattenedArray[_flattenedLength++] = new VirtualScrollFlattenedPositionInfo
                {
                    Type = VirtualScrollFlattenedPositionType.SectionFooter,
                    SectionIndex = sectionIndex,
                    ItemIndex = -1
                };
            }
        }

        // Global footer
        if (_layoutInfo.HasGlobalFooter)
        {
            EnsureCapacity(1);
            _flattenedArray[_flattenedLength++] = new VirtualScrollFlattenedPositionInfo
            {
                Type = VirtualScrollFlattenedPositionType.GlobalFooter,
                SectionIndex = -1,
                ItemIndex = -1
            };
        }
    }

    private int EstimateCapacity(int sectionCount)
    {
        var itemsPerSection = sectionCount > 0 ? _virtualScrollAdapter.GetItemCount(0) : 0;
        var sectionOverhead = (_layoutInfo.HasSectionHeader ? 1 : 0) + (_layoutInfo.HasSectionFooter ? 1 : 0);
        var globalOverhead = (_layoutInfo.HasGlobalHeader ? 1 : 0) + (_layoutInfo.HasGlobalFooter ? 1 : 0);
        return sectionCount * (itemsPerSection + sectionOverhead) + globalOverhead;
    }

    private void EnsureCapacity(int additionalItems)
    {
        if (_flattenedLength + additionalItems > _flattenedArray.Length)
        {
            var newCapacity = Math.Max(_flattenedArray.Length * 2, _flattenedLength + additionalItems);
            var newArray = new VirtualScrollFlattenedPositionInfo[newCapacity];
            if (_flattenedLength > 0)
            {
                Array.Copy(_flattenedArray, 0, newArray, 0, _flattenedLength);
            }
            _flattenedArray = newArray;
        }
    }

    private int GetFlattenedIndexForSectionStart(int sectionIndex)
    {
        // Use cached offsets for O(1) lookup
        if (sectionIndex >= 0 && sectionIndex < _sectionOffsets.Length)
        {
            return _sectionOffsets[sectionIndex];
        }

        // Fallback to calculation if offsets not available (shouldn't happen in normal operation)
        var flattenedIndex = _layoutInfo.HasGlobalHeader ? 1 : 0;

        for (var i = 0; i < sectionIndex; i++)
        {
            if (_layoutInfo.HasSectionHeader)
            {
                flattenedIndex++;
            }
            flattenedIndex += _virtualScrollAdapter.GetItemCount(i);
            if (_layoutInfo.HasSectionFooter)
            {
                flattenedIndex++;
            }
        }

        if (_layoutInfo.HasSectionHeader)
        {
            flattenedIndex++;
        }

        return flattenedIndex;
    }

    private int GetFlattenedIndexForItem(int sectionIndex, int itemIndex)
    {
        var flattenedIndex = GetFlattenedIndexForSectionStart(sectionIndex);
        // GetFlattenedIndexForSectionStart returns the index after the section header,
        // so we can directly add the itemIndex
        return flattenedIndex + itemIndex;
    }

    private int CalculateItemsForSections(int startSectionIndex, int sectionCount)
    {
        var totalItems = 0;
        for (var i = 0; i < sectionCount; i++)
        {
            var sectionIndex = startSectionIndex + i;
            if (_layoutInfo.HasSectionHeader)
            {
                totalItems++;
            }
            totalItems += _virtualScrollAdapter.GetItemCount(sectionIndex);
            if (_layoutInfo.HasSectionFooter)
            {
                totalItems++;
            }
        }
        return totalItems;
    }

    private void InsertIntoFlattenedArray(int flattenedIndex, int startSectionIndex, int sectionCount)
    {
        var itemsToInsert = CalculateItemsForSections(startSectionIndex, sectionCount);
        EnsureCapacity(itemsToInsert);

        if (flattenedIndex < _flattenedLength)
        {
            Array.Copy(_flattenedArray, flattenedIndex, _flattenedArray, flattenedIndex + itemsToInsert, _flattenedLength - flattenedIndex);
        }

        // Update section offsets - shift existing sections forward and insert new ones
        UpdateSectionOffsetsAfterInsert(startSectionIndex, sectionCount, itemsToInsert);

        var currentIndex = flattenedIndex;
        for (var i = 0; i < sectionCount; i++)
        {
            var sectionIndex = startSectionIndex + i;
            
            // Store offset for this section
            _sectionOffsets[sectionIndex] = currentIndex;
            
            if (_layoutInfo.HasSectionHeader)
            {
                _flattenedArray[currentIndex++] = new VirtualScrollFlattenedPositionInfo
                {
                    Type = VirtualScrollFlattenedPositionType.SectionHeader,
                    SectionIndex = sectionIndex,
                    ItemIndex = -1
                };
            }

            var itemCount = _virtualScrollAdapter.GetItemCount(sectionIndex);
            for (var itemIndex = 0; itemIndex < itemCount; itemIndex++)
            {
                _flattenedArray[currentIndex++] = new VirtualScrollFlattenedPositionInfo
                {
                    Type = VirtualScrollFlattenedPositionType.Item,
                    SectionIndex = sectionIndex,
                    ItemIndex = itemIndex
                };
            }

            if (_layoutInfo.HasSectionFooter)
            {
                _flattenedArray[currentIndex++] = new VirtualScrollFlattenedPositionInfo
                {
                    Type = VirtualScrollFlattenedPositionType.SectionFooter,
                    SectionIndex = sectionIndex,
                    ItemIndex = -1
                };
            }
        }

        _flattenedLength += itemsToInsert;
    }

    private void RemoveFromFlattenedArray(int flattenedIndex, int count)
    {
        if (flattenedIndex + count < _flattenedLength)
        {
            Array.Copy(_flattenedArray, flattenedIndex + count, _flattenedArray, flattenedIndex, _flattenedLength - flattenedIndex - count);
        }
        _flattenedLength -= count;
        
        // Update section offsets - shift subsequent sections backward
        UpdateSectionOffsetsAfterRemove(flattenedIndex, count);
    }

    private void MoveInFlattenedArray(int fromIndex, int toIndex, int count)
    {
        if (fromIndex == toIndex)
        {
            return;
        }

        var temp = new VirtualScrollFlattenedPositionInfo[count];
        Array.Copy(_flattenedArray, fromIndex, temp, 0, count);

        if (fromIndex < toIndex)
        {
            Array.Copy(_flattenedArray, fromIndex + count, _flattenedArray, fromIndex, toIndex - fromIndex);
            Array.Copy(temp, 0, _flattenedArray, toIndex, count);
        }
        else
        {
            Array.Copy(_flattenedArray, toIndex, _flattenedArray, toIndex + count, fromIndex - toIndex);
            Array.Copy(temp, 0, _flattenedArray, toIndex, count);
        }
    }

    private void InsertItemIntoFlattenedArray(int flattenedIndex, int sectionIndex, int itemIndex)
    {
        EnsureCapacity(1);
        if (flattenedIndex < _flattenedLength)
        {
            Array.Copy(_flattenedArray, flattenedIndex, _flattenedArray, flattenedIndex + 1, _flattenedLength - flattenedIndex);
        }
        _flattenedArray[flattenedIndex] = new VirtualScrollFlattenedPositionInfo
        {
            Type = VirtualScrollFlattenedPositionType.Item,
            SectionIndex = sectionIndex,
            ItemIndex = itemIndex
        };
        _flattenedLength++;

        // Update section offsets for subsequent sections (shifted by 1)
        UpdateSectionOffsetsAfterItemChange(sectionIndex + 1, 1);

        // Update item indices for subsequent items in the same section
        UpdateItemIndicesAfterInsert(sectionIndex, itemIndex);
    }

    private void InsertItemRangeIntoFlattenedArray(int startFlattenedIndex, int sectionIndex, int startItemIndex, int count)
    {
        EnsureCapacity(count);
        if (startFlattenedIndex < _flattenedLength)
        {
            Array.Copy(_flattenedArray, startFlattenedIndex, _flattenedArray, startFlattenedIndex + count, _flattenedLength - startFlattenedIndex);
        }

        for (var i = 0; i < count; i++)
        {
            _flattenedArray[startFlattenedIndex + i] = new VirtualScrollFlattenedPositionInfo
            {
                Type = VirtualScrollFlattenedPositionType.Item,
                SectionIndex = sectionIndex,
                ItemIndex = startItemIndex + i
            };
        }

        _flattenedLength += count;
        
        // Update section offsets for subsequent sections (shifted by count)
        UpdateSectionOffsetsAfterItemChange(sectionIndex + 1, count);
        
        UpdateItemIndicesAfterInsert(sectionIndex, startItemIndex + count);
    }

    private void RemoveItemFromFlattenedArray(int flattenedIndex)
    {
        var positionInfo = _flattenedArray[flattenedIndex];
        if (flattenedIndex + 1 < _flattenedLength)
        {
            Array.Copy(_flattenedArray, flattenedIndex + 1, _flattenedArray, flattenedIndex, _flattenedLength - flattenedIndex - 1);
        }
        _flattenedLength--;

        // Update section offsets for subsequent sections (shifted backward by 1)
        UpdateSectionOffsetsAfterItemChange(positionInfo.SectionIndex + 1, -1);

        UpdateItemIndicesAfterRemove(positionInfo.SectionIndex, positionInfo.ItemIndex);
    }

    private void RemoveItemRangeFromFlattenedArray(int startFlattenedIndex, int count)
    {
        var firstPositionInfo = _flattenedArray[startFlattenedIndex];
        if (startFlattenedIndex + count < _flattenedLength)
        {
            Array.Copy(_flattenedArray, startFlattenedIndex + count, _flattenedArray, startFlattenedIndex, _flattenedLength - startFlattenedIndex - count);
        }
        _flattenedLength -= count;

        // Update section offsets for subsequent sections (shifted backward by count)
        UpdateSectionOffsetsAfterItemChange(firstPositionInfo.SectionIndex + 1, -count);

        UpdateItemIndicesAfterRemove(firstPositionInfo.SectionIndex, firstPositionInfo.ItemIndex + count);
    }

    private void MoveItemInFlattenedArray(int fromIndex, int toIndex)
    {
        if (fromIndex == toIndex)
        {
            return;
        }

        var temp = _flattenedArray[fromIndex];
        if (fromIndex < toIndex)
        {
            Array.Copy(_flattenedArray, fromIndex + 1, _flattenedArray, fromIndex, toIndex - fromIndex);
        }
        else
        {
            Array.Copy(_flattenedArray, toIndex, _flattenedArray, toIndex + 1, fromIndex - toIndex);
        }
        _flattenedArray[toIndex] = temp;
    }

    private void UpdateItemIndicesAfterInsert(int sectionIndex, int insertedItemIndex)
    {
        var sectionStartIndex = GetFlattenedIndexForSectionStart(sectionIndex);
        if (_layoutInfo.HasSectionHeader)
        {
            sectionStartIndex++;
        }

        var sectionItemCount = _virtualScrollAdapter.GetItemCount(sectionIndex);
        for (var i = insertedItemIndex + 1; i < sectionItemCount; i++)
        {
            var flattenedIndex = sectionStartIndex + i;
            if (flattenedIndex < _flattenedLength && _flattenedArray[flattenedIndex].Type == VirtualScrollFlattenedPositionType.Item)
            {
                var info = _flattenedArray[flattenedIndex];
                info.ItemIndex = i;
                _flattenedArray[flattenedIndex] = info;
            }
        }
    }

    private void UpdateItemIndicesAfterRemove(int sectionIndex, int removedItemIndex)
    {
        var sectionStartIndex = GetFlattenedIndexForSectionStart(sectionIndex);
        if (_layoutInfo.HasSectionHeader)
        {
            sectionStartIndex++;
        }

        var sectionItemCount = _virtualScrollAdapter.GetItemCount(sectionIndex);
        for (var i = removedItemIndex; i < sectionItemCount; i++)
        {
            var flattenedIndex = sectionStartIndex + i;
            if (flattenedIndex < _flattenedLength && _flattenedArray[flattenedIndex].Type == VirtualScrollFlattenedPositionType.Item)
            {
                var info = _flattenedArray[flattenedIndex];
                info.ItemIndex = i;
                _flattenedArray[flattenedIndex] = info;
            }
        }
    }


    private void UpdateSectionOffsetsAfterInsert(int startSectionIndex, int sectionCount, int itemsInserted)
    {
        var sectionTotal = _virtualScrollAdapter.GetSectionCount();
        
        // Ensure offsets array is large enough
        if (_sectionOffsets.Length < sectionTotal)
        {
            var newOffsets = new int[Math.Max(sectionTotal, _sectionOffsets.Length * 2)];
            Array.Copy(_sectionOffsets, 0, newOffsets, 0, startSectionIndex);
            _sectionOffsets = newOffsets;
        }
        
        // Shift offsets for sections after insertion point forward by itemsInserted
        for (var i = startSectionIndex + sectionCount; i < sectionTotal; i++)
        {
            if (i < _sectionOffsets.Length)
            {
                _sectionOffsets[i] += itemsInserted;
            }
        }
    }

    private void UpdateSectionOffsetsAfterRemove(int flattenedIndex, int itemsRemoved)
    {
        // Find which section this removal affects and update subsequent sections
        var sectionTotal = _virtualScrollAdapter.GetSectionCount();
        
        // Shift offsets for sections that come after the removed items
        for (var i = 0; i < sectionTotal; i++)
        {
            if (i < _sectionOffsets.Length && _sectionOffsets[i] > flattenedIndex)
            {
                _sectionOffsets[i] -= itemsRemoved;
            }
        }
    }

    private void RemoveSectionOffsets(int startSectionIndex, int sectionCount)
    {
        // Remove offsets for deleted sections and shift remaining ones
        var sectionTotal = _virtualScrollAdapter.GetSectionCount();
        
        // Shift offsets for sections after the removed range
        if (startSectionIndex + sectionCount < _sectionOffsets.Length)
        {
            Array.Copy(_sectionOffsets, startSectionIndex + sectionCount, _sectionOffsets, startSectionIndex, sectionTotal - startSectionIndex);
        }
    }

    private void UpdateSectionOffsetsAfterItemChange(int startSectionIndex, int delta)
    {
        // When items are inserted/removed within a section, update offsets for subsequent sections
        var sectionTotal = _virtualScrollAdapter.GetSectionCount();
        
        for (var i = startSectionIndex; i < sectionTotal; i++)
        {
            if (i < _sectionOffsets.Length)
            {
                _sectionOffsets[i] += delta;
            }
        }
    }

    private void UpdateSectionIndicesAfterInsert(int startSectionIndex)
    {
        // After inserting sections, all subsequent sections have their indices incremented
        // Scan through flattened array and update SectionIndex for items belonging to affected sections
        var currentSectionIndex = 0;
        var sectionCount = _virtualScrollAdapter.GetSectionCount();
        var skipGlobalHeader = _layoutInfo.HasGlobalHeader;

        for (var i = 0; i < _flattenedLength && currentSectionIndex < sectionCount; i++)
        {
            var info = _flattenedArray[i];
            
            // Skip global header
            if (skipGlobalHeader && info.Type == VirtualScrollFlattenedPositionType.GlobalHeader)
            {
                continue;
            }

            // Update section header
            if (info.Type == VirtualScrollFlattenedPositionType.SectionHeader)
            {
                if (currentSectionIndex >= startSectionIndex)
                {
                    info.SectionIndex = currentSectionIndex;
                    _flattenedArray[i] = info;
                }
            }
            // Update section items
            else if (info.Type == VirtualScrollFlattenedPositionType.Item)
            {
                if (currentSectionIndex >= startSectionIndex)
                {
                    info.SectionIndex = currentSectionIndex;
                    _flattenedArray[i] = info;
                }
            }
            // Update section footer and advance to next section
            else if (info.Type == VirtualScrollFlattenedPositionType.SectionFooter)
            {
                if (currentSectionIndex >= startSectionIndex)
                {
                    info.SectionIndex = currentSectionIndex;
                    _flattenedArray[i] = info;
                }
                currentSectionIndex++;
            }
        }
    }

    private void UpdateSectionIndicesAfterRemove(int startSectionIndex)
    {
        // After removing sections, all subsequent sections have their indices decremented
        var currentSectionIndex = 0;
        var sectionCount = _virtualScrollAdapter.GetSectionCount();
        var skipGlobalHeader = _layoutInfo.HasGlobalHeader;

        for (var i = 0; i < _flattenedLength && currentSectionIndex < sectionCount; i++)
        {
            var info = _flattenedArray[i];
            
            // Skip global header
            if (skipGlobalHeader && info.Type == VirtualScrollFlattenedPositionType.GlobalHeader)
            {
                continue;
            }

            // Update section header
            if (info.Type == VirtualScrollFlattenedPositionType.SectionHeader)
            {
                if (info.SectionIndex > startSectionIndex)
                {
                    info.SectionIndex = currentSectionIndex;
                    _flattenedArray[i] = info;
                }
            }
            // Update section items
            else if (info.Type == VirtualScrollFlattenedPositionType.Item)
            {
                if (info.SectionIndex > startSectionIndex)
                {
                    info.SectionIndex = currentSectionIndex;
                    _flattenedArray[i] = info;
                }
            }
            // Update section footer and advance to next section
            else if (info.Type == VirtualScrollFlattenedPositionType.SectionFooter)
            {
                if (info.SectionIndex > startSectionIndex)
                {
                    info.SectionIndex = currentSectionIndex;
                    _flattenedArray[i] = info;
                }
                currentSectionIndex++;
            }
        }
    }

    private void NotifySubscribers(VirtualScrollFlattenedChangeSet changeSet)
    {
        foreach (var subscriber in _subscribers)
        {
            subscriber(changeSet);
        }
    }

    private void Unsubscribe(Action<VirtualScrollFlattenedChangeSet> callback) => _subscribers.Remove(callback);
    
    private struct VirtualScrollFlattenedPositionInfo
    {
        public int SectionIndex { get; set; }
        public int ItemIndex { get; set; }
        public VirtualScrollFlattenedPositionType Type { get; set; }
    }

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
