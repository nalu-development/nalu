using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Nalu.Cassowary;

/// <summary>
/// A specialized dictionary implementation optimized for Symbol keys and double values
/// </summary>
internal sealed class SymbolDoubleMap : IEnumerable<KeyValuePair<Symbol, double>>
{
    // Constants and static fields
    private const int _initialCapacity = 4;
    private readonly Symbol _nullKey = new(SymbolType.Invalid, 0);

    // Instance fields
    private int[] _buckets;         // The bucket array (contains indices into _entries)
    private Entry[] _entries;       // The entry array
    private double[] _values;       // The values array (parallel to _entries)
    private int _count;             // Number of items in the dictionary
    private int _freeList;          // Index of first entry in free list (-1 if empty)
    private int _freeCount;         // Number of entries in free list

    // Entry structure for the entry array
    private struct Entry
    {
        public Symbol Key;             // The Symbol.Id value (NullKey if unused)
        public int Next;            // Index of next entry, -1 if last
    }

    /// <summary>
    /// Initializes a new instance of SymbolDoubleMap with default capacity
    /// </summary>
    public SymbolDoubleMap() : this(_initialCapacity) { }

    /// <summary>
    /// Initializes a new instance of SymbolDoubleMap with specified capacity
    /// </summary>
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public SymbolDoubleMap(int capacity)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        if (capacity == 0)
        {
            _buckets = [];
            _entries = [];
            _values = [];
        }
        else
        {
            Initialize(capacity);
        }
    }
    
    /// <summary>
    /// Initializes a new instance of SymbolDoubleMap as a copy of another instance
    /// </summary>
    /// <param name="other">The SymbolDoubleMap to copy from</param>
    public SymbolDoubleMap(SymbolDoubleMap other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        if (other._count == 0)
        {
            // If source is empty, initialize with minimal capacity
            _buckets = [];
            _entries = [];
            _values = [];
        }
        else
        {
            // Copy buckets array
            var bucketsLength = other._buckets.Length;
            _buckets = new int[bucketsLength];
            Array.Copy(other._buckets, _buckets, bucketsLength);
            
            // Copy entries array
            var entriesLength = other._entries.Length;
            _entries = new Entry[entriesLength];
            Array.Copy(other._entries, _entries, entriesLength);
            
            // Copy values array
            var valuesLength = other._values.Length;
            _values = new double[valuesLength];
            Array.Copy(other._values, _values, valuesLength);
            
            // Copy other state
            _count = other._count;
            _freeList = other._freeList;
            _freeCount = other._freeCount;
        }
    }

    /// <summary>
    /// Gets or sets the value associated with the specified key
    /// </summary>
    public double this[Symbol key]
    {
        get
        {
            var i = FindEntry(key);
            if (i >= 0)
            {
                return _values[i];
            }

            throw new KeyNotFoundException($"The key {key} was not found.");
        }
        set => Insert(key, value, false);
    }

    /// <summary>
    /// Gets the number of key/value pairs contained in the SymbolDoubleMap
    /// </summary>
    public int Count => _count - _freeCount;

    /// <summary>
    /// Gets an enumerable collection containing the values in the SymbolDoubleMap
    /// </summary>
    public IEnumerable<double> Values
    {
        get
        {
            for (var i = 0; i < _count; i++)
            {
                if (_entries[i].Key != _nullKey) // Not a free entry
                {
                    yield return _values[i];
                }
            }
        }
    }
    
    /// <summary>
    /// Gets an enumerable collection containing the keys in the SymbolDoubleMap
    /// </summary>
    public IEnumerable<Symbol> Keys
    {
        get
        {
            for (var i = 0; i < _count; i++)
            {
                if (_entries[i].Key != _nullKey) // Not a free entry
                {
                    yield return _entries[i].Key;
                }
            }
        }
    }
    
    /// <summary>
    /// Gets a span that provides direct access to the underlying values array
    /// This span includes all values, including those that might be in free slots.
    /// </summary>
    /// <remarks>
    /// This method is unsafe and should be used with caution.
    /// The span is valid until the SymbolDoubleMap is modified.
    /// </remarks>
    public Span<double> GetUnsafeValuesSpan() => new(_values, 0, _count);
    
    /// <summary>
    /// Returns an enumerator that iterates through the SymbolDoubleMap
    /// </summary>
    public IEnumerator<KeyValuePair<Symbol, double>> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
        {
            if (_entries[i].Key != _nullKey) // Not a free entry
            {
                yield return new KeyValuePair<Symbol, double>(_entries[i].Key, _values[i]);
            }
        }
    }
    
    /// <summary>
    /// Returns an enumerator that iterates through the SymbolDoubleMap
    /// </summary>
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Tries to get the value associated with the specified key
    /// </summary>
    /// <returns>true if the key was found; otherwise, false</returns>
    public bool TryGetValue(Symbol key, out double value)
    {
        var i = FindEntry(key);
        if (i >= 0)
        {
            value = _values[i];
            return true;
        }
        value = 0;
        return false;
    }

    /// <summary>
    /// Determines whether the SymbolDoubleMap contains the specified key
    /// </summary>
    public bool ContainsKey(Symbol key) => FindEntry(key) >= 0;

    /// <summary>
    /// Adds a key/value pair to the SymbolDoubleMap
    /// </summary>
    public void Add(Symbol key, double value) => Insert(key, value, true);

    /// <summary>
    /// Removes the value with the specified key from the SymbolDoubleMap
    /// </summary>
    /// <returns>true if the element was removed; otherwise, false</returns>
    public bool Remove(Symbol key)
    {
        if (_buckets.Length > 0)
        {
            // Since Symbol.Id is always != 0, we can directly use it as the hash
            var hashCode = GetBucketIndex(key, _buckets.Length);
            var last = -1;
            var i = _buckets[hashCode] - 1; // Adjust for 1-based indexing

            while (i >= 0)
            {
                ref var entry = ref _entries[i];
                
                if (entry.Key == key)
                {
                    if (last < 0)
                    {
                        _buckets[hashCode] = entry.Next + 1; // Adjust for 1-based indexing
                    }
                    else
                    {
                        _entries[last].Next = entry.Next;
                    }

                    entry.Key = _nullKey;
                    entry.Next = _freeList;
                    
                    // Clear the value
                    _values[i] = 0;
                    
                    _freeList = i;
                    _freeCount++;
                    return true;
                }

                last = i;
                i = entry.Next;
            }
        }
        return false;
    }

    /// <summary>
    /// Removes all keys and values from the SymbolDoubleMap
    /// </summary>
    public void Clear()
    {
        if (_count > 0)
        {
            Array.Clear(_buckets, 0, _buckets.Length);
            Array.Clear(_entries, 0, _count);
            Array.Clear(_values, 0, _count);
            _freeList = -1;
            _freeCount = 0;
            _count = 0;
        }
    }

    /// <summary>
    /// Ensures that the SymbolDoubleMap can hold up to capacity entries without resizing
    /// </summary>
    public void EnsureCapacity(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        var currentCapacity = _buckets.Length;
        if (currentCapacity >= capacity)
        {
            return;
        }

        if (_buckets.Length == 0)
        {
            Initialize(capacity);
            return;
        }

        var newSize = HashHelpers.GetPrime(capacity);
        Resize(newSize);
    }

    /// <summary>
    /// Initializes the SymbolDoubleMap with the specified capacity
    /// </summary>
    private void Initialize(int capacity)
    {
        var size = HashHelpers.GetPrime(capacity);
        _buckets = new int[size];
        _entries = new Entry[size];
        _values = new double[size];
        _freeList = -1;
    }

    /// <summary>
    /// Finds the entry index for the specified key
    /// </summary>
    /// <returns>The index of the entry if found; otherwise, -1</returns>
    private int FindEntry(Symbol key)
    {
        if (_buckets.Length > 0)
        {
            // Since Symbol.Id is always > 0, we can directly use it as the hash
            var hashCode = GetBucketIndex(key, _buckets.Length);
            
            for (var i = _buckets[hashCode] - 1; i >= 0; i = _entries[i].Next)
            {
                if (_entries[i].Key == key)
                {
                    return i;
                }
            }
        }
        return -1;
    }

    /// <summary>
    /// Gets the bucket index for the specified id
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetBucketIndex(Symbol key, int bucketSize)
        // For Symbol.Id, we can use a simpler hashing function since it's already a positive integer
        => key.Id % bucketSize;

    /// <summary>
    /// Inserts a key/value pair into the SymbolDoubleMap
    /// </summary>
    private void Insert(Symbol key, double value, bool add)
    {
        if (_buckets.Length == 0)
        {
            Initialize(_initialCapacity);
        }

        // Since Symbol.Id is always > 0, we can directly use it as the hash
        var hashCode = GetBucketIndex(key, _buckets.Length);
        
        // Search if the key already exists
        var i = _buckets[hashCode] - 1; // Adjust for 1-based indexing
        while (i >= 0)
        {
            ref var entry = ref _entries[i];
            if (entry.Key == key)
            {
                if (add)
                {
                    throw new ArgumentException("An item with the same key has already been added", nameof(key));
                }

                _values[i] = value;
                return;
            }
            i = entry.Next;
        }

        int index;
        if (_freeCount > 0)
        {
            // Use an entry from the free list
            index = _freeList;
            _freeList = _entries[index].Next;
            _freeCount--;
        }
        else
        {
            // Need to add a new entry
            if (_count == _entries.Length)
            {
                Resize(HashHelpers.ExpandPrime(_count));
                hashCode = GetBucketIndex(key, _buckets.Length);
            }
            index = _count;
            _count++;
        }

        ref var newEntry = ref _entries[index];
        newEntry.Key = key;
        newEntry.Next = _buckets[hashCode] - 1; // Adjust for 1-based indexing
        _buckets[hashCode] = index + 1; // Adjust for 1-based indexing
        _values[index] = value;
    }

    /// <summary>
    /// Resizes the SymbolDoubleMap to the specified new size
    /// </summary>
    private void Resize(int newSize)
    {
        Debug.Assert(_entries.Length > 0);
        Debug.Assert(newSize >= _entries.Length);

        var newBuckets = new int[newSize];
        var newEntries = new Entry[newSize];
        var newValues = new double[newSize];

        // Copy the existing entries to the new arrays
        Array.Copy(_entries, newEntries, _count);
        Array.Copy(_values, newValues, _count);

        // Recompute the bucket indices for all entries
        for (var i = 0; i < _count; i++)
        {
            if (newEntries[i].Key != _nullKey)
            {
                var bucket = GetBucketIndex(newEntries[i].Key, newSize);
                newEntries[i].Next = newBuckets[bucket] - 1; // Adjust for 1-based indexing
                newBuckets[bucket] = i + 1; // Adjust for 1-based indexing
            }
        }

        _buckets = newBuckets;
        _entries = newEntries;
        _values = newValues;
    }
}

/// <summary>
/// HashHelpers provides utility methods for hash-based collections
/// </summary>
internal static class HashHelpers
{
    // Prime numbers for hash table sizes
    private static readonly int[] _primes =
    [
        3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
        1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
        17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
        187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
        1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
    ];

    /// <summary>
    /// Returns a prime number that is greater than or equal to the specified minimum
    /// </summary>
    public static int GetPrime(int min)
    {
        if (min < 0)
        {
            throw new ArgumentException("The minimum value must be non-negative", nameof(min));
        }

        foreach (var prime in _primes)
        {
            if (prime >= min)
            {
                return prime;
            }
        }

        // If we get here, min is greater than the largest prime in our table
        // Just return min if it's prime, or the next prime after min
        for (var i = (min | 1); i < int.MaxValue; i += 2)
        {
            if (IsPrime(i))
            {
                return i;
            }
        }

        return min;
    }

    /// <summary>
    /// Expands a prime number to the next larger prime
    /// </summary>
    public static int ExpandPrime(int oldSize)
    {
        var newSize = 2 * oldSize;
        if ((uint)newSize > 0x7FEFFFFF) // Max array length
        {
            return 0x7FEFFFFF;
        }

        return GetPrime(newSize);
    }

    /// <summary>
    /// Determines whether the specified value is prime
    /// </summary>
    private static bool IsPrime(int candidate)
    {
        if ((candidate & 1) == 0)
        {
            return candidate == 2;
        }

        var limit = (int)Math.Sqrt(candidate);
        for (var divisor = 3; divisor <= limit; divisor += 2)
        {
            if ((candidate % divisor) == 0)
            {
                return false;
            }
        }

        return true;
    }
}
