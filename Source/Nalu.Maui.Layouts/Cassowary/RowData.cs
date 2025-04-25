using System.Collections;
using System.Runtime.InteropServices;
using Nalu.Cassowary.Extensions;

namespace Nalu.Cassowary;

internal class RowData : IEnumerable<KeyValuePair<Symbol, double>>
{
    private static readonly int _dictionaryStorageThreshold = ComputeDictionaryStorageThreshold();

    private readonly List<double> _values;
    private List<Symbol>? _keys;
    private Dictionary<Symbol, int>? _indexBySymbol;

    public int Count { get; private set; }

    public IEnumerable<Symbol> Keys => _keys?.Where(s => s != Symbol.Invalid) ?? (IEnumerable<Symbol>)_indexBySymbol!.Keys;

    public RowData()
    {
        _keys = [];
        _values = [];
    }

    public RowData(int capacity)
    {
        if (capacity > _dictionaryStorageThreshold)
        {
            _indexBySymbol = new Dictionary<Symbol, int>(capacity);
        }
        else
        {
            _keys = new List<Symbol>(capacity);
        }

        _values = new List<double>(capacity);
    }

    public RowData(RowData other)
    {
        _values = [..other._values];

        if (other._keys is not null)
        {
            _keys = [..other._keys];
        }
        else
        {
            _indexBySymbol = new Dictionary<Symbol, int>(other._indexBySymbol!);
        }

        Count = other.Count;
    }

    public void Multiply(double value)
    {
        for (var i = 0; i < _values.Count; i++)
        {
            _values[i] *= value;
        }
    }

    public double GetValueOrDefault(Symbol symbol, double defaultValue = 0)
    {
        var index = IndexOf(symbol);
        return index < 0 ? defaultValue : _values[index];
    }

    public void Add(Symbol symbol, double value)
    {
        if (value == 0)
        {
            return;
        }

        var index = IndexOf(symbol);
        if (index >= 0)
        {
            value += _values[index];

            if (value.IsNearZero())
            {
                Remove(symbol);
            }
            else
            {
                _values[index] = value;
            }

            return;
        }

        if (value.IsNearZero())
        {
            return;
        }

        index = _values.IndexOf(double.NaN);

        // If we're about to exceed the threshold, we need to switch to a dictionary
        var valuesCount = _values.Count;
        if (index < 0)
        {
            EnsureProperKeyStorage(valuesCount + 1);
        }

        if (index < 0)
        {
            index = valuesCount;
            _values.Add(value);
            _indexBySymbol?.Add(symbol, index);
            _keys?.Add(symbol);
        }
        else
        {
            _values[index] = value;
            _indexBySymbol?.Add(symbol, index);
            if (_keys is not null)
            {
                _keys[index] = symbol;
            }
        }

        ++Count;
    }

    private void EnsureProperKeyStorage(int capacity)
    {
        if (capacity > _dictionaryStorageThreshold && _keys is not null && _indexBySymbol is null)
        {
            var keysCount = _keys.Count;
            _indexBySymbol = new Dictionary<Symbol, int>(keysCount + 1);
            for (var i = 0; i < keysCount; i++)
            {
                _indexBySymbol[_keys[i]] = i;
            }

            _keys = null;
        }
    }

    public bool Remove(Symbol symbol, out double value)
    {
        var index = IndexOf(symbol);
        if (index >= 0)
        {
            value = _values[index];
            _values[index] = double.NaN;
            _indexBySymbol?.Remove(symbol);
            if (_keys is not null)
            {
                _keys[index] = Symbol.Invalid;
            }

            --Count;
            return true;
        }

        value = 0;
        return false;
    }

    public void Remove(Symbol symbol)
    {
        var index = IndexOf(symbol);
        if (index >= 0)
        {
            _values[index] = double.NaN;
            _indexBySymbol?.Remove(symbol);
            if (_keys is not null)
            {
                _keys[index] = Symbol.Invalid;
            }

            --Count;
        }
    }

    private int IndexOf(Symbol symbol) => _indexBySymbol?.GetValueOrDefault(symbol, -1) ?? _keys!.IndexOf(symbol);

    public IEnumerator<KeyValuePair<Symbol, double>> GetEnumerator()
    {
        if (_keys is not null)
        {
            var keysCount = _keys.Count;
            for (var i = 0; i < keysCount; i++)
            {
                var key = _keys[i];
                if (key != Symbol.Invalid)
                {
                    yield return new KeyValuePair<Symbol, double>(key, _values[i]);
                }
            }
        }
        else
        {
            foreach (var (key, i) in _indexBySymbol!)
            {
                yield return new KeyValuePair<Symbol, double>(key, _values[i]);
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static int ComputeDictionaryStorageThreshold()
    {
        var logicalCores = Environment.ProcessorCount;

        // Conservative base
        var baseThreshold = 8;

        // Scale up on powerful machines
        if (logicalCores >= 8)
        {
            baseThreshold += 4;
        }

        // Tweak based on architecture
        if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
        {
            baseThreshold -= 2; // ARM cache behavior differs
        }

        // Zero-based index
        return baseThreshold - 1;
    }
}
