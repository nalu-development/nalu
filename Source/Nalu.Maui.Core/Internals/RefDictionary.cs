using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Nalu.Internals;

[DebuggerDisplay("Count = {Count}")]
internal class RefDictionary<TKey, TValue>
    where TKey : notnull
{
    private int[]? _buckets;
    private Entry[]? _entries;
    private ulong _fastModMultiplier;
    private int _count;
    private int _freeList;
    private int _freeCount;
    private readonly IEqualityComparer<TKey>? _comparer;
    private const int _startOfFreeList = -3;

    public IEnumerable<TKey> Keys
    {
        get
        {
            if (_entries != null)
            {
                for (var i = 0; i < _count; i++)
                {
                    if (_entries[i].Next >= -1)
                    {
                        yield return _entries[i].Key;
                    }
                }
            }
        }
    }

    public RefDictionary()
        : this(0, null) { }

    public RefDictionary(int capacity)
        : this(capacity, null) { }

    public RefDictionary(IEqualityComparer<TKey>? comparer)
        : this(0, comparer) { }

    public RefDictionary(int capacity, IEqualityComparer<TKey>? comparer)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (capacity > 0)
        {
            Initialize(capacity);
        }

        // For reference types, we always want to store a comparer instance, either
        // the one provided, or if one wasn't provided, the default (accessing
        // EqualityComparer<TKey>.Default with shared generics on every dictionary
        // access can add measurable overhead).  For value types, if no comparer is
        // provided, or if the default is provided, we'd prefer to use
        // EqualityComparer<TKey>.Default.Equals on every use, enabling the JIT to
        // devirtualize and possibly inline the operation.
        if (!typeof(TKey).IsValueType)
        {
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
        }
        else if (comparer is not null && // first check for null to avoid forcing default comparer instantiation unnecessarily
                 !ReferenceEquals(comparer, EqualityComparer<TKey>.Default))
        {
            _comparer = comparer;
        }
    }

    public RefDictionary(RefDictionary<TKey, TValue> dictionary)
        : this(0, dictionary._comparer)
    {
        if (dictionary._buckets == null)
        {
            return;
        }

        _buckets = new int[dictionary._buckets.Length];
        dictionary._buckets.CopyTo(_buckets, 0);

        _entries = new Entry[dictionary._entries!.Length];
        dictionary._entries.CopyTo(_entries, 0);

        _count = dictionary._count;
        _freeList = dictionary._freeList;
        _freeCount = dictionary._freeCount;
        _fastModMultiplier = dictionary._fastModMultiplier;
    }

    public IEqualityComparer<TKey> Comparer => _comparer ?? EqualityComparer<TKey>.Default;

    public int Count => _count - _freeCount;

    /// <summary>
    /// Gets the total numbers of elements the internal data structure can hold without resizing.
    /// </summary>
    public int Capacity => _entries?.Length ?? 0;

    public TValue this[TKey key]
    {
        get
        {
            ref var value = ref FindValue(key);

            if (!Unsafe.IsNullRef(ref value))
            {
                return value;
            }

            throw new KeyNotFoundException();
        }
        set
        {
            var modified = TryInsert(key, value, InsertionBehavior.OverwriteExisting);
            Debug.Assert(modified);
        }
    }

    public void Add(TKey key, TValue value)
    {
        var modified = TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
        Debug.Assert(modified); // If there was an existing key and the Add failed, an exception will already have been thrown.
    }

    public void Clear()
    {
        var count = _count;

        if (count > 0)
        {
            Debug.Assert(_buckets != null, "_buckets should be non-null");
            Debug.Assert(_entries != null, "_entries should be non-null");

            Array.Clear(_buckets);

            _count = 0;
            _freeList = -1;
            _freeCount = 0;
            Array.Clear(_entries, 0, count);
        }
    }

    public bool ContainsKey(TKey key) =>
        !Unsafe.IsNullRef(ref FindValue(key));

    public bool ContainsValue(TValue value)
    {
        var entries = _entries;

        if (value == null)
        {
            for (var i = 0; i < _count; i++)
            {
                if (entries![i].Next >= -1 && entries[i].Value == null)
                {
                    return true;
                }
            }
        }
        else if (typeof(TValue).IsValueType)
        {
            // ValueType: Devirtualize with EqualityComparer<TValue>.Default intrinsic
            for (var i = 0; i < _count; i++)
            {
                if (entries![i].Next >= -1 && EqualityComparer<TValue>.Default.Equals(entries[i].Value, value))
                {
                    return true;
                }
            }
        }
        else
        {
            // Object type: Shared Generic, EqualityComparer<TValue>.Default won't devirtualize
            // https://github.com/dotnet/runtime/issues/10050
            // So cache in a local rather than get EqualityComparer per loop iteration
            var defaultComparer = EqualityComparer<TValue>.Default;

            for (var i = 0; i < _count; i++)
            {
                if (entries![i].Next >= -1 && defaultComparer.Equals(entries[i].Value, value))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public ref TValue GetOrAddDefaultRef(TKey key, out bool exists)
    {
        // NOTE: this method is mirrored in Insert below.

        ArgumentNullException.ThrowIfNull(key);

        if (_buckets == null)
        {
            Initialize(0);
        }

        Debug.Assert(_buckets != null);

        var entries = _entries;
        Debug.Assert(entries != null, "expected entries to be non-null");

        var comparer = _comparer;
        Debug.Assert(comparer is not null || typeof(TKey).IsValueType);
        var hashCode = (uint) (typeof(TKey).IsValueType && comparer == null ? key.GetHashCode() : comparer!.GetHashCode(key));

        uint collisionCount = 0;
        ref var bucket = ref GetBucket(hashCode);
        var i = bucket - 1; // Value in _buckets is 1-based

        if (typeof(TKey).IsValueType && // comparer can only be null for value types; enable JIT to eliminate entire if block for ref types
            comparer == null)
        {
            // ValueType: Devirtualize with EqualityComparer<TKey>.Default intrinsic
            while ((uint) i < (uint) entries.Length)
            {
                if (entries[i].HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(entries[i].Key, key))
                {
                    exists = true;

                    return ref entries[i].Value;
                }

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
        else
        {
            Debug.Assert(comparer is not null);

            while ((uint) i < (uint) entries.Length)
            {
                if (entries[i].HashCode == hashCode && comparer.Equals(entries[i].Key, key))
                {
                    exists = true;

                    return ref entries[i].Value;
                }

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
        entry.Value = default!;
        bucket = index + 1; // Value in _buckets is 1-based

        exists = false;

        return ref entry.Value;
    }

    public ref TValue FindValue(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        ref var entry = ref Unsafe.NullRef<Entry>();

        if (_buckets != null)
        {
            Debug.Assert(_entries != null, "expected entries to be != null");
            var comparer = _comparer;

            if (typeof(TKey).IsValueType && // comparer can only be null for value types; enable JIT to eliminate entire if block for ref types
                comparer == null)
            {
                var hashCode = (uint) key.GetHashCode();
                var i = GetBucket(hashCode);
                var entries = _entries;
                uint collisionCount = 0;

                // ValueType: Devirtualize with EqualityComparer<TKey>.Default intrinsic
                i--; // Value in _buckets is 1-based; subtract 1 from i. We do it here so it fuses with the following conditional.

                do
                {
                    // Test in if to drop range check for following array access
                    if ((uint) i >= (uint) entries.Length)
                    {
                        goto ReturnNotFound;
                    }

                    entry = ref entries[i];

                    if (entry.HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
                    {
                        goto ReturnFound;
                    }

                    i = entry.Next;

                    collisionCount++;
                } while (collisionCount <= (uint) entries.Length);

                // The chain of entries forms a loop; which means a concurrent update has happened.
                // Break out of the loop and throw, rather than looping forever.
                goto ConcurrentOperation;
            }
            else
            {
                Debug.Assert(comparer is not null);
                var hashCode = (uint) comparer.GetHashCode(key);
                var i = GetBucket(hashCode);
                var entries = _entries;
                uint collisionCount = 0;
                i--; // Value in _buckets is 1-based; subtract 1 from i. We do it here so it fuses with the following conditional.

                do
                {
                    // Test in if to drop range check for following array access
                    if ((uint) i >= (uint) entries.Length)
                    {
                        goto ReturnNotFound;
                    }

                    entry = ref entries[i];

                    if (entry.HashCode == hashCode && comparer.Equals(entry.Key, key))
                    {
                        goto ReturnFound;
                    }

                    i = entry.Next;

                    collisionCount++;
                } while (collisionCount <= (uint) entries.Length);

                // The chain of entries forms a loop; which means a concurrent update has happened.
                // Break out of the loop and throw, rather than looping forever.
                goto ConcurrentOperation;
            }
        }

        goto ReturnNotFound;

        ConcurrentOperation:

        throw new InvalidOperationException("Concurrent operations not supported");

#pragma warning disable CS8619
        ReturnFound:
        ref var value = ref entry.Value;
        Return:

        return ref value;
        ReturnNotFound:
        value = ref Unsafe.NullRef<TValue>();
#pragma warning restore CS8619
        goto Return;
    }

    private int Initialize(int capacity)
    {
        var size = HashHelpers.GetPrime(capacity);
        var buckets = new int[size];
        var entries = new Entry[size];

        // Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
        _freeList = -1;
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint) size);
        _buckets = buckets;
        _entries = entries;

        return size;
    }

    private bool TryInsert(TKey key, TValue value, InsertionBehavior behavior)
    {
        // NOTE: this method is mirrored in CollectionsMarshal.GetValueRefOrAddDefault below.
        // If you make any changes here, make sure to keep that version in sync as well.

        ArgumentNullException.ThrowIfNull(key);

        if (_buckets == null)
        {
            Initialize(0);
        }

        Debug.Assert(_buckets != null);

        var entries = _entries;
        Debug.Assert(entries != null, "expected entries to be non-null");

        var comparer = _comparer;
        Debug.Assert(comparer is not null || typeof(TKey).IsValueType);
        var hashCode = (uint) (typeof(TKey).IsValueType && comparer == null ? key.GetHashCode() : comparer!.GetHashCode(key));

        uint collisionCount = 0;
        ref var bucket = ref GetBucket(hashCode);
        var i = bucket - 1; // Value in _buckets is 1-based

        if (typeof(TKey).IsValueType && // comparer can only be null for value types; enable JIT to eliminate entire if block for ref types
            comparer == null)
        {
            // ValueType: Devirtualize with EqualityComparer<TKey>.Default intrinsic
            while ((uint) i < (uint) entries.Length)
            {
                if (entries[i].HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(entries[i].Key, key))
                {
                    if (behavior == InsertionBehavior.OverwriteExisting)
                    {
                        entries[i].Value = value;

                        return true;
                    }

                    if (behavior == InsertionBehavior.ThrowOnExisting)
                    {
                        throw new InvalidOperationException($"Adding duplicate with key: {key}");
                    }

                    return false;
                }

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
        else
        {
            Debug.Assert(comparer is not null);

            while ((uint) i < (uint) entries.Length)
            {
                if (entries[i].HashCode == hashCode && comparer.Equals(entries[i].Key, key))
                {
                    if (behavior == InsertionBehavior.OverwriteExisting)
                    {
                        entries[i].Value = value;

                        return true;
                    }

                    if (behavior == InsertionBehavior.ThrowOnExisting)
                    {
                        throw new InvalidOperationException($"Adding duplicate with key: {key}");
                    }

                    return false;
                }

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

    private void Resize() => Resize(HashHelpers.ExpandPrime(_count));

    private void Resize(int newSize)
    {
        // Value types never rehash
        Debug.Assert(_entries != null, "_entries should be non-null");
        Debug.Assert(newSize >= _entries.Length);

        var entries = new Entry[newSize];

        var count = _count;
        Array.Copy(_entries, entries, count);

        // Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
        _buckets = new int[newSize];
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint) newSize);

        for (var i = 0; i < count; i++)
        {
            if (entries[i].Next >= -1)
            {
                ref var bucket = ref GetBucket(entries[i].HashCode);
                entries[i].Next = bucket - 1; // Value in _buckets is 1-based
                bucket = i + 1;
            }
        }

        _entries = entries;
    }

    public bool Remove(TKey key)
    {
        // The overload Remove(TKey key, out TValue value) is a copy of this method with one additional
        // statement to copy the value for entry being removed into the output parameter.
        // Code has been intentionally duplicated for performance reasons.

        ArgumentNullException.ThrowIfNull(key);

        if (_buckets != null)
        {
            Debug.Assert(_entries != null, "entries should be non-null");
            uint collisionCount = 0;

            var comparer = _comparer;
            Debug.Assert(typeof(TKey).IsValueType || comparer is not null);
            var hashCode = (uint) (typeof(TKey).IsValueType && comparer == null ? key.GetHashCode() : comparer!.GetHashCode(key));

            ref var bucket = ref GetBucket(hashCode);
            var entries = _entries;
            var last = -1;
            var i = bucket - 1; // Value in buckets is 1-based

            while (i >= 0)
            {
                ref var entry = ref entries[i];

                if (entry.HashCode == hashCode &&
                    (typeof(TKey).IsValueType && comparer == null ? EqualityComparer<TKey>.Default.Equals(entry.Key, key) : comparer!.Equals(entry.Key, key)))
                {
                    if (last < 0)
                    {
                        bucket = entry.Next + 1; // Value in buckets is 1-based
                    }
                    else
                    {
                        entries[last].Next = entry.Next;
                    }

                    Debug.Assert(
                        _startOfFreeList - _freeList < 0,
                        "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646"
                    );

                    entry.Next = _startOfFreeList - _freeList;

                    if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())
                    {
                        entry.Key = default!;
                    }

                    if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
                    {
                        entry.Value = default!;
                    }

                    _freeList = i;
                    _freeCount++;

                    return true;
                }

                last = i;
                i = entry.Next;

                collisionCount++;

                if (collisionCount > (uint) entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    throw new InvalidOperationException("Concurrent operations not supported");
                }
            }
        }

        return false;
    }

    public bool Remove(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        // This overload is a copy of the overload Remove(TKey key) with one additional
        // statement to copy the value for entry being removed into the output parameter.
        // Code has been intentionally duplicated for performance reasons.

        ArgumentNullException.ThrowIfNull(key);

        if (_buckets != null)
        {
            Debug.Assert(_entries != null, "entries should be non-null");
            uint collisionCount = 0;

            var comparer = _comparer;
            Debug.Assert(typeof(TKey).IsValueType || comparer is not null);
            var hashCode = (uint) (typeof(TKey).IsValueType && comparer == null ? key.GetHashCode() : comparer!.GetHashCode(key));

            ref var bucket = ref GetBucket(hashCode);
            var entries = _entries;
            var last = -1;
            var i = bucket - 1; // Value in buckets is 1-based

            while (i >= 0)
            {
                ref var entry = ref entries[i];

                if (entry.HashCode == hashCode &&
                    (typeof(TKey).IsValueType && comparer == null ? EqualityComparer<TKey>.Default.Equals(entry.Key, key) : comparer!.Equals(entry.Key, key)))
                {
                    if (last < 0)
                    {
                        bucket = entry.Next + 1; // Value in buckets is 1-based
                    }
                    else
                    {
                        entries[last].Next = entry.Next;
                    }

                    value = entry.Value;

                    Debug.Assert(
                        _startOfFreeList - _freeList < 0,
                        "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646"
                    );

                    entry.Next = _startOfFreeList - _freeList;

                    if (RuntimeHelpers.IsReferenceOrContainsReferences<TKey>())
                    {
                        entry.Key = default!;
                    }

                    if (RuntimeHelpers.IsReferenceOrContainsReferences<TValue>())
                    {
                        entry.Value = default!;
                    }

                    _freeList = i;
                    _freeCount++;

                    return true;
                }

                last = i;
                i = entry.Next;

                collisionCount++;

                if (collisionCount > (uint) entries.Length)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    throw new InvalidOperationException("Concurrent operations not supported");
                }
            }
        }

        value = default;

        return false;
    }

    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        ref var valRef = ref FindValue(key);

        if (!Unsafe.IsNullRef(ref valRef))
        {
            value = valRef;

            return true;
        }

        value = default;

        return false;
    }

    public bool TryAdd(TKey key, TValue value) =>
        TryInsert(key, value, InsertionBehavior.None);

    /// <summary>
    /// Ensures that the dictionary can hold up to 'capacity' entries without any further expansion of its backing storage
    /// </summary>
    public int EnsureCapacity(int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        var currentCapacity = _entries?.Length ?? 0;

        if (currentCapacity >= capacity)
        {
            return currentCapacity;
        }

        if (_buckets == null)
        {
            return Initialize(capacity);
        }

        var newSize = HashHelpers.GetPrime(capacity);
        Resize(newSize);

        return newSize;
    }

    /// <summary>
    /// Sets the capacity of this dictionary to what it would be if it had been originally initialized with all its entries
    /// </summary>
    /// <remarks>
    /// This method can be used to minimize the memory overhead
    /// once it is known that no new elements will be added.
    /// To allocate minimum size storage array, execute the following statements:
    /// dictionary.Clear();
    /// dictionary.TrimExcess();
    /// </remarks>
    public void TrimExcess() => TrimExcess(Count);

    /// <summary>
    /// Sets the capacity of this dictionary to hold up 'capacity' entries without any further expansion of its backing storage
    /// </summary>
    /// <remarks>
    /// This method can be used to minimize the memory overhead
    /// once it is known that no new elements will be added.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Passed capacity is lower than entries count.</exception>
    public void TrimExcess(int capacity)
    {
        if (capacity < Count)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        var newSize = HashHelpers.GetPrime(capacity);
        var oldEntries = _entries;
        var currentCapacity = oldEntries?.Length ?? 0;

        if (newSize >= currentCapacity)
        {
            return;
        }

        var oldCount = _count;
        Initialize(newSize);

        Debug.Assert(oldEntries is not null);

        CopyEntries(oldEntries, oldCount);
    }

    private void CopyEntries(Entry[] entries, int count)
    {
        Debug.Assert(_entries is not null);

        var newEntries = _entries;
        var newCount = 0;

        for (var i = 0; i < count; i++)
        {
            var hashCode = entries[i].HashCode;

            if (entries[i].Next >= -1)
            {
                ref var entry = ref newEntries[newCount];
                entry = entries[i];
                ref var bucket = ref GetBucket(hashCode);
                entry.Next = bucket - 1; // Value in _buckets is 1-based
                bucket = newCount + 1;
                newCount++;
            }
        }

        _count = newCount;
        _freeCount = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref int GetBucket(uint hashCode)
    {
        var buckets = _buckets!;

        return ref buckets[HashHelpers.FastMod(hashCode, (uint) buckets.Length, _fastModMultiplier)];
    }

    public ref struct EntryEnumerator
    {
        private Entry[] _entries;
        private int _index;
        private readonly int _count;

        public EntryEnumerator(Entry[] entries, int count)
        {
            _entries = entries;
            _index = -1;
            _count = count;
        }

        public bool MoveNext()
        {
            while (++_index < _count)
            {
                if (_entries[_index].Next >= -1)
                {
                    return true;
                }
            }

            return false;
        }

        public readonly ref Entry Current => ref _entries[_index];
    }

    public EntryEnumerator GetEnumerator() => new(_entries ?? [], _count);

    public struct Entry
    {
        public uint HashCode;

        /// <summary>
        /// 0-based index of next entry in chain: -1 means end of chain
        /// also encodes whether this entry _itself_ is part of the free list by changing sign and subtracting 3,
        /// so -2 means end of free list, -3 means index 0 but on free list, -4 means index 1 but on free list, etc.
        /// </summary>
        public int Next;

        public TKey Key; // Key of entry
        public TValue Value; // Value of entry
    }
}

internal static class HashHelpers
{
    public const uint HashCollisionThreshold = 100;

    // This is the maximum prime smaller than Array.MaxLength.
    public const int MaxPrimeArrayLength = 0x7FFFFFC3;

    public const int HashPrime = 101;

    // Table of prime numbers to use as hash table sizes.
    // A typical resize algorithm would pick the smallest prime number in this array
    // that is larger than twice the previous capacity.
    // Suppose our Hashtable currently has capacity x and enough elements are added
    // such that a resize needs to occur. Resizing first computes 2x then finds the
    // first prime in the table greater than 2x, i.e. if primes are ordered
    // p_1, p_2, ..., p_i, ..., it finds p_n such that p_n-1 < 2x < p_n.
    // Doubling is important for preserving the asymptotic complexity of the
    // hashtable operations such as add.  Having a prime guarantees that double
    // hashing does not lead to infinite loops.  IE, your hash function will be
    // h1(key) + i*h2(key), 0 <= i < size.  h2 and the size must be relatively prime.
    // We prefer the low computation costs of higher prime numbers over the increased
    // memory allocation of a fixed prime number i.e. when right sizing a HashSet.
    public static ReadOnlySpan<int> Primes =>
    [
        3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
        1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
        17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
        187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
        1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
    ];

    public static bool IsPrime(int candidate)
    {
        if ((candidate & 1) != 0)
        {
            var limit = (int) Math.Sqrt(candidate);

            for (var divisor = 3; divisor <= limit; divisor += 2)
            {
                if (candidate % divisor == 0)
                {
                    return false;
                }
            }

            return true;
        }

        return candidate == 2;
    }

    public static int GetPrime(int min)
    {
        if (min < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(min));
        }

        foreach (var prime in Primes)
        {
            if (prime >= min)
            {
                return prime;
            }
        }

        // Outside of our predefined table. Compute the hard way.
        for (var i = min | 1; i < int.MaxValue; i += 2)
        {
            if (IsPrime(i) && (i - 1) % HashPrime != 0)
            {
                return i;
            }
        }

        return min;
    }

    // Returns size of hashtable to grow to.
    public static int ExpandPrime(int oldSize)
    {
        var newSize = 2 * oldSize;

        // Allow the hashtables to grow to maximum possible size (~2G elements) before encountering capacity overflow.
        // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
        if ((uint) newSize > MaxPrimeArrayLength && MaxPrimeArrayLength > oldSize)
        {
            Debug.Assert(MaxPrimeArrayLength == GetPrime(MaxPrimeArrayLength), "Invalid MaxPrimeArrayLength");

            return MaxPrimeArrayLength;
        }

        return GetPrime(newSize);
    }

    /// <summary>Returns approximate reciprocal of the divisor: ceil(2**64 / divisor).</summary>
    /// <remarks>This should only be used on 64-bit.</remarks>
    public static ulong GetFastModMultiplier(uint divisor) =>
        (ulong.MaxValue / divisor) + 1;

    /// <summary>Performs a mod operation using the multiplier pre-computed with <see cref="GetFastModMultiplier" />.</summary>
    /// <remarks>This should only be used on 64-bit.</remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint FastMod(uint value, uint divisor, ulong multiplier)
    {
        // We use modified Daniel Lemire's fastmod algorithm (https://github.com/dotnet/runtime/pull/406),
        // which allows to avoid the long multiplication if the divisor is less than 2**31.
        Debug.Assert(divisor <= int.MaxValue);

        // This is equivalent of (uint)Math.BigMul(multiplier * value, divisor, out _). This version
        // is faster than BigMul currently because we only need the high bits.
        var highbits = (uint) (((((multiplier * value) >> 32) + 1) * divisor) >> 32);

        Debug.Assert(highbits == value % divisor);

        return highbits;
    }
}

internal enum InsertionBehavior : byte
{
    /// <summary>
    /// The default insertion behavior.
    /// </summary>
    None = 0,

    /// <summary>
    /// Specifies that an existing entry with the same key should be overwritten if encountered.
    /// </summary>
    OverwriteExisting = 1,

    /// <summary>
    /// Specifies that if an existing entry with the same key is encountered, an exception should be thrown.
    /// </summary>
    ThrowOnExisting = 2
}
