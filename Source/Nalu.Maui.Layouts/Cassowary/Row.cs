using System.Diagnostics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using Nalu.Internals;

namespace Nalu.Cassowary;

/// <summary>
/// An internal row class used by the solver.
/// This implementation uses a hybrid AoS/SoA approach:
/// - Entry struct (hashcode, next, key) for fast lookups
/// - Separate values array for SIMD vectorization with TensorPrimitives
/// </summary>
internal class Row
{
    private struct Entry
    {
        public uint HashCode;
        public int Next;
        public Symbol Key;
    }

    private double _constant;

    // Hybrid AoS/SoA - entries for lookup, separate values for SIMD
    private int[]? _buckets;
    private Entry[]? _entries;
    private double[]? _values;

    private ulong _fastModMultiplier;
    private int _count;
    private int _freeList;
    private int _freeCount;
    private int _dummyCount;

    private const int _startOfFreeList = -3;

    /// <summary>
    /// The variable associated with this row, if any.
    /// </summary>
    public Variable? Variable { get; set; }

    /// <summary>
    /// Returns the number of cells (symbols) in the row.
    /// </summary>
    public int CellCount => _count - _freeCount;

    /// <summary>
    /// Construct a new Row3.
    /// </summary>
    internal Row(double constant = 0.0)
    {
        _constant = constant;
        _freeList = -1;
    }

    /// <summary>
    /// Construct a new Row3 as a copy of another.
    /// </summary>
    private Row(Row other)
    {
        _constant = other._constant;
        Variable = other.Variable;

        if (other._buckets == null)
        {
            _freeList = -1;
            return;
        }

        var length = other._buckets.Length;

        _buckets = new int[length];
        other._buckets.CopyTo(_buckets, 0);

        _entries = new Entry[length];
        other._entries!.CopyTo(_entries, 0);

        _values = new double[length];
        other._values!.CopyTo(_values, 0);

        _count = other._count;
        _freeList = other._freeList;
        _freeCount = other._freeCount;
        _fastModMultiplier = other._fastModMultiplier;
        _dummyCount = other._dummyCount;
    }

    /// <summary>
    /// Returns the constant for the row.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Constant() => _constant;

    /// <summary>
    /// Returns true if the row is a constant value (no symbols).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsConstant() => CellCount == 0;

    /// <summary>
    /// Returns true if the Row has all dummy symbols.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool AllDummies() => _dummyCount == CellCount;

    /// <summary>
    /// Create a copy of the row.
    /// </summary>
    public Row Copy() => new(this);

    /// <summary>
    /// Add a constant value to the row constant.
    /// Returns the new value of the constant.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double Add(double value) => _constant += value;

    /// <summary>
    /// Insert the symbol into the row with the given coefficient.
    /// If the symbol already exists in the row, the coefficient
    /// will be added to the existing coefficient. If the resulting
    /// coefficient is zero, the symbol will be removed from the row.
    /// </summary>
    public void InsertSymbol(Symbol symbol, double coefficient = 1.0)
        => SumOrRemove(symbol, coefficient);

    /// <summary>
    /// Insert a row into this row with a given coefficient.
    /// The constant and the cells of the other row will be
    /// multiplied by the coefficient and added to this row. Any
    /// cell with a resulting coefficient of zero will be removed
    /// from the row.
    /// </summary>
    public void InsertRow(Row other, double coefficient = 1.0)
    {
        _constant += other._constant * coefficient;

        if (other._entries == null)
        {
            return;
        }

        var thisCount = CellCount;
        var otherCount = other.CellCount;

        // If other is significantly larger, swap approach
        if (ShouldSwapInsertRow(thisCount, otherCount))
        {
            InsertRowSwapped(other, coefficient);
            return;
        }

        // Standard approach: iterate over other
        var otherEntries = other._entries;
        var otherValues = other._values!;
        var otherTotalCount = other._count;

        for (var i = 0; i < otherTotalCount; i++)
        {
            if (otherEntries[i].Next >= -1)
            {
                SumOrRemove(otherEntries[i].Key, otherValues[i] * coefficient);
            }
        }
    }

    /// <summary>
    /// Optimized InsertRow when other is significantly larger than this.
    /// </summary>
    private void InsertRowSwapped(Row other, double coefficient)
    {
        // Save current state
        var oldEntries = _entries;
        var oldValues = _values;
        var oldCount = _count;

        // Copy other's structure
        var length = other._buckets!.Length;

        _buckets = new int[length];
        other._buckets.CopyTo(_buckets, 0);

        _entries = new Entry[length];
        other._entries!.CopyTo(_entries, 0);

        _values = new double[length];
        other._values!.CopyTo(_values, 0);

        _count = other._count;
        _freeList = other._freeList;
        _freeCount = other._freeCount;
        _fastModMultiplier = other._fastModMultiplier;
        _dummyCount = other._dummyCount;

        // Multiply all values by coefficient using TensorPrimitives (SIMD)
        var valuesSpan = _values.AsSpan(0, _count);
        TensorPrimitives.Multiply(valuesSpan, coefficient, valuesSpan);

        // Now insert old entries (the smaller set)
        if (oldEntries != null)
        {
            for (var i = 0; i < oldCount; i++)
            {
                if (oldEntries[i].Next >= -1)
                {
                    SumOrRemove(oldEntries[i].Key, oldValues![i]);
                }
            }
        }
    }

    /// <summary>
    /// Remove a symbol from the row.
    /// </summary>
    public void RemoveSymbol(Symbol symbol) => Remove(symbol);

    /// <summary>
    /// Reverse the sign of the constant and cells in the row.
    /// Uses TensorPrimitives for SIMD-accelerated negation.
    /// </summary>
    public void ReverseSign()
    {
        _constant = -_constant;

        if (_values == null)
        {
            return;
        }

        // Use TensorPrimitives.Multiply by -1 for SIMD acceleration
        var valuesSpan = _values.AsSpan(0, _count);
        TensorPrimitives.Multiply(valuesSpan, -1.0, valuesSpan);
    }

    /// <summary>
    /// Solve the row for the given symbol.
    /// Uses TensorPrimitives for SIMD-accelerated multiplication.
    /// </summary>
    public void SolveFor(Symbol symbol)
    {
        if (!Remove(symbol, out var coefficient))
        {
            throw new InvalidOperationException($"Symbol {symbol} not found in row for solving.");
        }

        var inverseCoeff = -1.0 / coefficient;
        _constant *= inverseCoeff;

        if (_values == null)
        {
            return;
        }

        // Use TensorPrimitives for SIMD acceleration
        var valuesSpan = _values.AsSpan(0, _count);
        TensorPrimitives.Multiply(valuesSpan, inverseCoeff, valuesSpan);
    }

    /// <summary>
    /// Solve the row for the given symbols.
    /// </summary>
    public void SolveForEx(Symbol lhs, Symbol rhs)
    {
        InsertSymbol(lhs, -1.0);
        SolveFor(rhs);
    }

    /// <summary>
    /// Returns the coefficient for the given symbol.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double CoefficientFor(Symbol symbol) => TryGetValue(symbol, out var coefficient) ? coefficient : 0.0;

    /// <summary>
    /// Substitute a symbol with the data from another row.
    /// </summary>
    public void Substitute(Symbol symbol, Row row)
    {
        if (Remove(symbol, out var coefficient))
        {
            InsertRow(row, coefficient);
        }
    }

    /// <summary>
    /// Update the variable (if any) with the current value of the row constant.
    /// </summary>
    public void UpdateVariable() => Variable?.CurrentValue = _constant;

    #region Solver-specific query methods

    /// <summary>
    /// Compute the entering variable for a pivot operation.
    /// This method will return first symbol in the objective function which
    /// is non-dummy and has a coefficient less than zero. If no symbol meets
    /// the criteria, it means the objective function is at a minimum, and an
    /// invalid symbol is returned.
    /// </summary>
    public Symbol GetEnteringSymbol()
    {
        if (_entries == null)
        {
            return Symbol.InvalidSymbol;
        }

        var entries = _entries;
        var values = _values!;
        var count = _count;

        for (var i = 0; i < count; i++)
        {
            ref var entry = ref entries[i];

            if (entry.Next >= -1 && values[i] < 0.0 && entry.Key.Type != SymbolType.Dummy)
            {
                return entry.Key;
            }
        }

        return Symbol.InvalidSymbol;
    }

    /// <summary>
    /// Compute the entering symbol for the dual optimize operation.
    /// This method will return the symbol in the row which has a positive
    /// coefficient and yields the minimum ratio for its respective symbol
    /// in the objective function. The provided row *must* be infeasible.
    /// If no symbol is found which meats the criteria, an invalid symbol
    /// is returned.
    /// </summary>
    public Symbol GetDualEnteringSymbol(Row objective)
    {
        if (_entries == null)
        {
            return Symbol.InvalidSymbol;
        }

        var entries = _entries;
        var values = _values!;
        var count = _count;
        var ratio = double.MaxValue;
        var entering = Symbol.InvalidSymbol;

        for (var i = 0; i < count; i++)
        {
            ref var entry = ref entries[i];

            if (entry.Next >= -1)
            {
                var c = values[i];
                if (c > 0.0 && entry.Key.Type != SymbolType.Dummy)
                {
                    var coeff = objective.CoefficientFor(entry.Key);
                    var r = coeff / c;

                    if (r < ratio)
                    {
                        ratio = r;
                        entering = entry.Key;
                    }
                }
            }
        }

        return entering;
    }

    /// <summary>
    /// Returns the first external symbol in the row.
    /// </summary>
    public Symbol GetFirstExternalSymbol()
    {
        if (_entries == null)
        {
            return Symbol.InvalidSymbol;
        }

        var entries = _entries;
        var count = _count;

        for (var i = 0; i < count; i++)
        {
            ref var entry = ref entries[i];
            if (entry is { Next: >= -1, Key.Type: SymbolType.External })
            {
                return entry.Key;
            }
        }

        return Symbol.InvalidSymbol;
    }

    /// <summary>
    /// Get the first Slack or Error symbol in the row.
    /// If no such symbol is present, an invalid symbol will be returned.
    /// </summary>
    public Symbol GetAnyPivotableSymbol()
    {
        if (_entries == null)
        {
            return Symbol.InvalidSymbol;
        }

        var entries = _entries;
        var count = _count;

        for (var i = 0; i < count; i++)
        {
            ref var entry = ref entries[i];

            if (entry is { Next: >= -1, Key.Type: SymbolType.Slack or SymbolType.Error })
            {
                return entry.Key;
            }
        }

        return Symbol.InvalidSymbol;
    }

    #endregion

    #region Embedded dictionary operations (Hybrid AoS/SoA)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ShouldSwapInsertRow(int thisCount, int otherCount)
        => otherCount - thisCount >= 8 && otherCount - thisCount >= (thisCount >> 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint GetHashCode(Symbol symbol) => (uint)symbol.Id;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool SymbolEquals(Symbol x, Symbol y) => x.Id == y.Id;

    private int Initialize(int capacity)
    {
        var size = HashHelpers.GetPrime(capacity);

        _buckets = new int[size];
        _entries = new Entry[size];
        _values = new double[size];
        _freeList = -1;
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)size);

        return size;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private ref int GetBucket(uint hashCode)
    {
        var buckets = _buckets!;
        return ref buckets[HashHelpers.FastMod(hashCode, (uint)buckets.Length, _fastModMultiplier)];
    }

    private void Resize()
    {
        var newSize = HashHelpers.ExpandPrime(_count);
        Debug.Assert(_entries != null, "_entries should be non-null");
        Debug.Assert(newSize >= _entries.Length);

        var count = _count;

        var newEntries = new Entry[newSize];
        Array.Copy(_entries, newEntries, count);

        var newValues = new double[newSize];
        Array.Copy(_values!, newValues, count);

        _buckets = new int[newSize];
        _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)newSize);

        for (var i = 0; i < count; i++)
        {
            if (newEntries[i].Next >= -1)
            {
                ref var bucket = ref GetBucket(newEntries[i].HashCode);
                newEntries[i].Next = bucket - 1;
                bucket = i + 1;
            }
        }

        _entries = newEntries;
        _values = newValues;
    }

    private void SumOrRemove(Symbol key, double value)
    {
        if (_buckets == null)
        {
            Initialize(0);
        }

        Debug.Assert(_buckets != null);

        var entries = _entries!;
        var values = _values!;

        var hashCode = GetHashCode(key);

        uint collisionCount = 0;
        ref var bucket = ref GetBucket(hashCode);
        var last = -1;
        var i = bucket - 1;

        while ((uint)i < (uint)entries.Length)
        {
            ref var entry = ref entries[i];
            if (entry.HashCode == hashCode && SymbolEquals(entry.Key, key))
            {
                values[i] += value;

                if (Solver.NearZero(values[i]))
                {
                    // Remove the entry
                    if (last < 0)
                    {
                        bucket = entry.Next + 1;
                    }
                    else
                    {
                        entries[last].Next = entry.Next;
                    }

                    Debug.Assert(_startOfFreeList - _freeList < 0);

                    entry.Next = _startOfFreeList - _freeList;
                    _freeList = i;
                    _freeCount++;

                    if (key.Type == SymbolType.Dummy)
                    {
                        _dummyCount--;
                    }

                    return;
                }

                return;
            }

            last = i;
            i = entry.Next;
            collisionCount++;

            if (collisionCount > (uint)entries.Length)
            {
                throw new InvalidOperationException("Concurrent operations not supported");
            }
        }

        // Not found, add new entry
        int index;

        if (_freeCount > 0)
        {
            index = _freeList;
            Debug.Assert(_startOfFreeList - entries[_freeList].Next >= -1);
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
                entries = _entries!;
                values = _values!;
            }

            index = count;
            _count = count + 1;
        }

        ref var newEntry = ref entries[index];
        newEntry.HashCode = hashCode;
        newEntry.Next = bucket - 1;
        newEntry.Key = key;
        values[index] = value;
        bucket = index + 1;

        if (key.Type == SymbolType.Dummy)
        {
            _dummyCount++;
        }
    }

    private bool TryGetValue(Symbol key, out double value)
    {
        if (_buckets == null)
        {
            value = default;
            return false;
        }

        var entries = _entries!;
        var values = _values!;

        var hashCode = GetHashCode(key);
        var i = GetBucket(hashCode) - 1;
        uint collisionCount = 0;

        while ((uint)i < (uint)entries.Length)
        {
            ref var entry = ref entries[i];
            if (entry.HashCode == hashCode && SymbolEquals(entry.Key, key))
            {
                value = values[i];
                return true;
            }

            i = entry.Next;
            collisionCount++;

            if (collisionCount > (uint)entries.Length)
            {
                throw new InvalidOperationException("Concurrent operations not supported");
            }
        }

        value = default;
        return false;
    }

    private bool Remove(Symbol key)
    {
        if (_buckets == null)
        {
            return false;
        }

        var entries = _entries!;

        uint collisionCount = 0;
        var hashCode = GetHashCode(key);

        ref var bucket = ref GetBucket(hashCode);
        var last = -1;
        var i = bucket - 1;

        while (i >= 0)
        {
            ref var entry = ref entries[i];
            if (entry.HashCode == hashCode && SymbolEquals(entry.Key, key))
            {
                if (last < 0)
                {
                    bucket = entry.Next + 1;
                }
                else
                {
                    entries[last].Next = entry.Next;
                }

                Debug.Assert(_startOfFreeList - _freeList < 0);

                entry.Next = _startOfFreeList - _freeList;
                _freeList = i;
                _freeCount++;

                if (key.Type == SymbolType.Dummy)
                {
                    _dummyCount--;
                }

                return true;
            }

            last = i;
            i = entry.Next;
            collisionCount++;

            if (collisionCount > (uint)entries.Length)
            {
                throw new InvalidOperationException("Concurrent operations not supported");
            }
        }

        return false;
    }

    private bool Remove(Symbol key, out double value)
    {
        if (_buckets == null)
        {
            value = default;
            return false;
        }

        var entries = _entries!;
        var values = _values!;

        uint collisionCount = 0;
        var hashCode = GetHashCode(key);

        ref var bucket = ref GetBucket(hashCode);
        var last = -1;
        var i = bucket - 1;

        while (i >= 0)
        {
            ref var entry = ref entries[i];
            if (entry.HashCode == hashCode && SymbolEquals(entry.Key, key))
            {
                if (last < 0)
                {
                    bucket = entry.Next + 1;
                }
                else
                {
                    entries[last].Next = entry.Next;
                }

                value = values[i];

                Debug.Assert(_startOfFreeList - _freeList < 0);

                entry.Next = _startOfFreeList - _freeList;
                _freeList = i;
                _freeCount++;

                if (key.Type == SymbolType.Dummy)
                {
                    _dummyCount--;
                }

                return true;
            }

            last = i;
            i = entry.Next;
            collisionCount++;

            if (collisionCount > (uint)entries.Length)
            {
                throw new InvalidOperationException("Concurrent operations not supported");
            }
        }

        value = default;
        return false;
    }

    #endregion
}
