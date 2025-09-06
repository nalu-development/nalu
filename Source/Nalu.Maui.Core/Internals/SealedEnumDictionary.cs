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
    private readonly TValue[] _values = new TValue[EnumKeys<TKey>.Length];

    public SealedEnumDictionary() { }

    public SealedEnumDictionary(Func<TKey, TValue> initializer)
    {
        var keys = EnumKeys<TKey>.Values;
        var keysLength = keys.Length;
        for (var index = 0; index < keysLength; index++)
        {
            var key = keys[index];
            _values[Convert.ToInt32(key)] = initializer(key);
        }
    }

    public TValue this[TKey key]
    {
        get => _values[Convert.ToInt32(key)];
        set => _values[Convert.ToInt32(key)] = value;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        var values = EnumKeys<TKey>.Values;
        var valuesLength = values.Length;
        for (var i = 0; i < valuesLength; ++i)
        {
            var key = values[i];
            var index = Convert.ToInt32(key);
        
            yield return new KeyValuePair<TKey, TValue>(key, _values[index]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
