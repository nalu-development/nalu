using System.Collections;

namespace Nalu.Internals;

static file class EnumKeys<TEnum>
    where TEnum : struct, Enum
{
    public static readonly TEnum[] Values = Enum.GetValues<TEnum>();
    // ReSharper disable once StaticMemberInGenericType
    public static readonly int Length = Convert.ToInt32(Values.Max()) + 1;
}

internal class SealedEnumDictionary<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>
    where TKey : struct, Enum
{
    private struct Entry {
        public TKey Key { get; set; }
        public TValue Value { get; set; }
    }

    private readonly Entry[] _values = new Entry[EnumKeys<TKey>.Length];

    public SealedEnumDictionary()
    {
        var keys = EnumKeys<TKey>.Values;
        var keysLength = keys.Length;
        for (var index = 0; index < keysLength; index++)
        {
            var key = keys[index];
            ref var entry = ref _values[index];
            entry.Key = key;
        }
    }

    public SealedEnumDictionary(Func<TKey, TValue> initializer)
    {
        var keys = EnumKeys<TKey>.Values;
        var keysLength = keys.Length;
        for (var index = 0; index < keysLength; index++)
        {
            var key = keys[index];
            ref var entry = ref _values[index];
            entry.Key = key;
            entry.Value = initializer(key);
        }
    }

    public TValue this[TKey key]
    {
        get => _values[Convert.ToInt32(key)].Value;
        set
        {
            ref var entry = ref _values[Convert.ToInt32(key)];
            entry.Value = value;
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        var values = _values;
        var valuesLength = values.Length;
        for (var i = 0; i < valuesLength; ++i)
        {
            ref var entry = ref _values[i];
        
            yield return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
