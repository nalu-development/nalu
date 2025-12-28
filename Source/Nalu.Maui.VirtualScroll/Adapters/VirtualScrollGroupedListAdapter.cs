using System.Collections;

namespace Nalu;

/// <summary>
/// An adapter that wraps a grouped list for use with <see cref="VirtualScroll"/>.
/// </summary>
public class VirtualScrollGroupedListAdapter : IVirtualScrollAdapter
{
    private static readonly NoOpUnsubscriber _noOpUnsubscriber = new();

    private readonly IList _sections;
    private readonly Func<object, IList> _sectionItemsGetter;


    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollGroupedListAdapter" /> class.
    /// </summary>
    /// <param name="sections">The collection of sections.</param>
    /// <param name="sectionItemsGetter">A function that extracts the items collection from a section object.</param>
    public VirtualScrollGroupedListAdapter(IEnumerable sections, Func<object, IEnumerable> sectionItemsGetter)
    {
        _sections = (sections ?? throw new ArgumentNullException(nameof(sections))) is IList list ? list : sections.Cast<object>().ToArray();
        var getter = sectionItemsGetter ?? throw new ArgumentNullException(nameof(sectionItemsGetter));
        _sectionItemsGetter = section =>
        {
            var items = getter(section);
            return items is IList itemsList ? itemsList : items.Cast<object>().ToArray();
        };
    }

    /// <inheritdoc/>
    public int GetSectionCount() => _sections.Count;

    /// <inheritdoc/>
    public int GetItemCount(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= _sections.Count)
        {
            return 0;
        }

        var section = _sections[sectionIndex] ?? throw new InvalidOperationException($"Section at index {sectionIndex} is null. All sections must be non-null.");
        var items = _sectionItemsGetter(section) ?? throw new InvalidOperationException($"The sectionItemsGetter returned null for section at index {sectionIndex}. The function must return a valid items collection for each section.");
        return items.Count;
    }

    /// <inheritdoc/>
    public object? GetSection(int sectionIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= _sections.Count)
        {
            return null;
        }

        return _sections[sectionIndex];
    }

    /// <inheritdoc/>
    public object? GetItem(int sectionIndex, int itemIndex)
    {
        if (sectionIndex < 0 || sectionIndex >= _sections.Count)
        {
            return null;
        }

        var section = _sections[sectionIndex] ?? throw new InvalidOperationException($"Section at index {sectionIndex} is null. All sections must be non-null.");
        var items = _sectionItemsGetter(section) ?? throw new InvalidOperationException($"The sectionItemsGetter returned null for section at index {sectionIndex}. The function must return a valid items collection for each section.");
        
        if (itemIndex < 0 || itemIndex >= items.Count)
        {
            return null;
        }

        return items[itemIndex];
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback) => _noOpUnsubscriber;

    private sealed class NoOpUnsubscriber : IDisposable
    {
        public void Dispose()
        {
            // No-op: grouped list adapter doesn't have change notifications
        }
    }
}

