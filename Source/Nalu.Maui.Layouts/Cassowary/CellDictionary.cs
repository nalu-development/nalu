using System.Diagnostics;
using Nalu.Internals;

namespace Nalu.Cassowary;

internal class CellDictionary : RefDictionary<Symbol, double>
{
    public CellDictionary(CellDictionary cells)
        : base(cells)
    {
    }

    public CellDictionary()
        : base(SymbolDictionaryComparer.Instance) {
    }

    public bool SumOrRemove(Symbol key, double value)
    {
        // NOTE: this method is mirrored in Insert below.

        if (_buckets == null)
        {
            Initialize(0);
        }

        Debug.Assert(_buckets != null);

        var entries = _entries;
        Debug.Assert(entries != null, "expected entries to be non-null");

        var comparer = _comparer;
        var hashCode = (uint)comparer!.GetHashCode(key);

        uint collisionCount = 0;
        ref var bucket = ref GetBucket(hashCode);
        var last = -1;
        var i = bucket - 1; // Value in _buckets is 1-based
        
        {
            Debug.Assert(comparer is not null);

            while ((uint) i < (uint) entries.Length)
            {
                if (entries[i].HashCode == hashCode && comparer.Equals(entries[i].Key, key))
                {
                    ref var updateEntry = ref entries[i];
                    ref var updateEntryValue = ref updateEntry.Value;
                    updateEntryValue += value;
                    
                    if (Row.NearZero(updateEntryValue))
                    {
                        if (last < 0)
                        {
                            bucket = updateEntry.Next + 1; // Value in buckets is 1-based
                        }
                        else
                        {
                            entries[last].Next = updateEntry.Next;
                        }
                        // Value in buckets is 1-based
                        Debug.Assert(
                            _startOfFreeList - _freeList < 0,
                            "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646"
                        );
                        
                        entries[i].Next = _startOfFreeList - _freeList;
                        
                        _freeList = i;
                        _freeCount++;
                        
                        return false;
                    }

                    return true;
                }
                
                last = i;

                i = entries[i].Next;

                collisionCount++;

                if (collisionCount > (uint) entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    throw new InvalidOperationException("Concurrent operations not supported");
                }
            }
        }

        int index;

        if (_freeCount > 0)
        {
            index = _freeList;
            Debug.Assert(_startOfFreeList - entries[_freeList].Next >= -1, "shouldn't overflow because `next` cannot underflow");
            _freeList = _startOfFreeList - entries[_freeList].Next;
            _freeCount--;
        }
        else
        {
            var count = _count;

            if (count == entries.Length)
            {
                Resize();
                bucket = ref GetBucket(hashCode);
            }

            index = count;
            _count = count + 1;
            entries = _entries;
        }

        ref var entry = ref entries![index];
        entry.HashCode = hashCode;
        entry.Next = bucket - 1; // Value in _buckets is 1-based
        entry.Key = key;
        entry.Value = value;
        bucket = index + 1; // Value in _buckets is 1-based
        
        return true;
    }
}
