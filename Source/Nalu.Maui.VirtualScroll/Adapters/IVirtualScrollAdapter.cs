namespace Nalu;

/// <summary>
/// Adapter interface for virtual scroll data sources.
/// </summary>
public interface IVirtualScrollAdapter
{
    /// <summary>
    /// Gets the section count.
    /// </summary>
    int GetSectionCount();

    /// <summary>
    /// Gets the item count for the specified section.
    /// </summary>
    int GetItemCount(int sectionIndex);
    
    /// <summary>
    /// Gets the section object for the specified section index.
    /// </summary>
    object? GetSection(int sectionIndex);

    /// <summary>
    /// Gets the item object for the specified section and item index.
    /// </summary>
    object? GetItem(int sectionIndex, int itemIndex);

    /// <summary>
    /// Subscribes to change notifications.
    /// </summary>
    /// <param name="changeCallback">The callback to invoke when changes occur.</param>
    /// <returns>A disposable that unsubscribes from change notifications when disposed.</returns>
    IDisposable Subscribe(Action<VirtualScrollChangeSet> changeCallback);
}
