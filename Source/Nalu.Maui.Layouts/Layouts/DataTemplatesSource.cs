using System.Collections;

namespace Nalu;

/// <summary>
/// A simple list of <see cref="DataTemplate"/> to be used in combination with <see cref="TemplateSourceSelector"/>.
/// </summary>
public class DataTemplatesSource : IList<DataTemplate>
{
    private readonly List<DataTemplate> _list = new(4);

    /// <inheritdoc />
    public IEnumerator<DataTemplate> GetEnumerator() => _list.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)_list).GetEnumerator();

    /// <inheritdoc />
    public void Add(DataTemplate item) => _list.Add(item);

    /// <inheritdoc />
    public void Clear() => _list.Clear();

    /// <inheritdoc />
    public bool Contains(DataTemplate item) => _list.Contains(item);

    /// <inheritdoc />
    public void CopyTo(DataTemplate[] array, int arrayIndex) => _list.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public bool Remove(DataTemplate item) => _list.Remove(item);

    /// <inheritdoc />
    public int Count => _list.Count;

    /// <inheritdoc />
    public bool IsReadOnly => ((ICollection<DataTemplate>) _list).IsReadOnly;

    /// <inheritdoc />
    public int IndexOf(DataTemplate item) => _list.IndexOf(item);

    /// <inheritdoc />
    public void Insert(int index, DataTemplate item) => _list.Insert(index, item);

    /// <inheritdoc />
    public void RemoveAt(int index) => _list.RemoveAt(index);

    /// <inheritdoc />
    public DataTemplate this[int index]
    {
        get => _list[index];
        set => _list[index] = value;
    }
}
