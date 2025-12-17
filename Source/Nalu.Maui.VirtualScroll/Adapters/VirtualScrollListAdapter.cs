using System.Collections;

namespace Nalu;

/// <summary>
/// An adapter that wraps an <see cref="IEnumerable"/> for use with <see cref="VirtualScroll"/>.
/// </summary>
public class VirtualScrollListAdapter : IVirtualScrollAdapter
{
    private readonly IList _list;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollListAdapter" /> class based on the specified enumerable.
    /// </summary>
    public VirtualScrollListAdapter(IEnumerable enumerable)
    {
        _list = enumerable is IList list ? list : enumerable.Cast<object>().ToArray();
    }

    /// <inheritdoc/>
    public int GetSectionCount() => _list.Count > 0 ? 1 : 0;

    /// <inheritdoc/>
    public int GetItemCount(int sectionIndex) => _list.Count;

    /// <inheritdoc/>
    public object? GetSection(int sectionIndex) => null;

    /// <inheritdoc/>
    public object? GetItem(int sectionIndex, int itemIndex) => _list[itemIndex];

    /// <inheritdoc/>
    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback) => new NoOpUnsubscriber();

    private sealed class NoOpUnsubscriber : IDisposable
    {
        public void Dispose()
        {
            // No-op: list adapter doesn't have change notifications
        }
    }
}
