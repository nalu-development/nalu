using System.Collections;

namespace Nalu.Maui.Navigation.SourceGenerators;

/// <summary>
/// An immutable array with value (sequence) equality, safe to embed in incremental-pipeline
/// records: the pipeline caches by Equals, and reference-equatable collections would defeat it.
/// </summary>
internal readonly struct EquatableArray<T>(T[] array) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    public static readonly EquatableArray<T> Empty = new([]);

    private readonly T[]? _array = array;

    public int Count => _array?.Length ?? 0;

    public T this[int index] => (_array ?? throw new IndexOutOfRangeException())[index];

    public bool Equals(EquatableArray<T> other)
    {
        var a = _array ?? [];
        var b = other._array ?? [];

        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a.Length != b.Length)
        {
            return false;
        }

        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }

        return true;
    }

    public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        if (_array is not { } array)
        {
            return 0;
        }

        unchecked
        {
            var hash = 17;

            foreach (var item in array)
            {
                hash = (hash * 31) + item.GetHashCode();
            }

            return hash;
        }
    }

    public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>) (_array ?? [])).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
