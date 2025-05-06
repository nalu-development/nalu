// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Runtime.CompilerServices;

namespace Nalu.Cassowary;

/// <summary>
/// Array-based dictionary for storing key-value pairs where keys are dense integers.
/// </summary>
internal class RowData : IEnumerable<KeyValuePair<Symbol, double>>
{
    private struct Entry
    {
        public bool IsSet;
        public Symbol Key;
    }

    private Entry[] _entries;
    private double[] _values;
    private int _count;

    public RowData(int capacity = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        
        var size = HashHelpers.GetPrime(capacity);

        _entries = new Entry[size];
        _values = new double[size];
    }

    public RowData(RowData other)
    {
        _count = other.Count;
        var len = other._entries.Length;
        _entries = new Entry[len];
        _values = new double[len];
        Array.Copy(other._entries, _entries, len);
        Array.Copy(other._values, _values, len);
    }

    public int Count => _count;

    public IEnumerable<Symbol> Keys
    {
        get
        {
            var len = _entries.Length;
            for (var i = 0; i < len; i++)
            {
                ref var entry = ref _entries[i];
                if (entry.IsSet)
                {
                    yield return entry.Key;
                }
            }
        }
    }

    public IEnumerable<double> Values
    {
        get
        {
            var len = _entries.Length;
            for (var i = 0; i < len; i++)
            {
                ref var entry = ref _entries[i];
                if (entry.IsSet)
                {
                    yield return _values[i];
                }
            }
        }
    }

    public double this[Symbol key]
    {
        get
        {
            var index = key.Id;
            ref var entry = ref _entries[index];
            if (entry.IsSet)
            {
                return _values[index];
            }

            throw new KeyNotFoundException();
        }
        set => Insert(key, value, overwriteExisting: true);
    }

    public void Add(Symbol key, double value) => Insert(key, value, overwriteExisting: false);

    public ref double GetValueOrAddRef(Symbol key, out bool exists)
    {
        var index = key.Id;

        if (index >= _entries.Length)
        {
            EnsureCapacity(index + 1);
        }

        ref var entry = ref _entries[index];
        exists = entry.IsSet;

        if (!exists)
        {
            entry.IsSet = true;
            entry.Key = key;
            _values[index] = 0;
            ++_count;
        }

        return ref _values[index];
    }

    private void Insert(Symbol key, double value, bool overwriteExisting)
    {
        var keyIndex = key.Id;
        EnsureCapacity(keyIndex + 1);

        ref var entry = ref _entries[keyIndex];
        if (entry.IsSet && !overwriteExisting)
        {
            throw new ArgumentException($"Key {key} already exists.");
        }

        if (!entry.IsSet)
        {
            ++_count;
            entry.IsSet = true;
            entry.Key = key;
        }

        _values[keyIndex] = value;
    }

    private void EnsureCapacity(int capacity)
    {
        if (_entries.Length >= capacity)
        {
            return;
        }

        var newSize = HashHelpers.GetPrime(capacity);
        var newEntries = new Entry[newSize];
        var newValues = new double[newSize];
        var len = _entries.Length;
        Array.Copy(_entries, newEntries, len);
        Array.Copy(_values, newValues, len);
        _entries = newEntries;
        _values = newValues;
    }

    public bool TryGetValue(Symbol key, out double value)
    {
        var index = key.Id;
        if (index < _entries.Length && _entries[index].IsSet)
        {
            value = _values[index];
            return true;
        }

        value = 0;
        return false;
    }

    public bool ContainsKey(Symbol key)
    {
        var index = key.Id;
        return index < _entries.Length && _entries[index].IsSet;
    }

    public bool Remove(Symbol key)
    {
        var keyIndex = key.Id;
        if (keyIndex < _entries.Length)
        {
            ref var entry = ref _entries[keyIndex];
            if (entry.IsSet)
            {
                entry.IsSet = false;
                entry.Key = default!;
                _values[keyIndex] = 0;
                --_count;
                return true;
            }
        }

        return false;
    }
    
    public bool Remove(Symbol key, out double value)
    {
        var keyIndex = key.Id;
        if (keyIndex < _entries.Length)
        {
            ref var entry = ref _entries[keyIndex];
            if (entry.IsSet)
            {
                entry.IsSet = false;
                entry.Key = default!;
                value = _values[keyIndex];
                _values[keyIndex] = 0;
                --_count;
                return true;
            }
        }

        value = 0;
        return false;
    }

    public IEnumerator<KeyValuePair<Symbol, double>> GetEnumerator()
    {
        var len = _entries.Length;
        for (var i = 0; i < len; i++)
        {
            ref var entry = ref _entries[i];
            if (entry.IsSet)
            {
                yield return new KeyValuePair<Symbol, double>(entry.Key, _values[i]);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
