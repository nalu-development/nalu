namespace Nalu;

/// <summary>
/// Adapter interface for virtual scroll data sources.
/// </summary>
internal interface IVirtualScrollFlattenedAdapter
{
    /// <summary>
    /// Gets the total item count.
    /// </summary>
    int GetItemCount();
    
    /// <summary>
    /// Gets the item info for the specified flattened index.
    /// </summary>
    VirtualScrollFlattenedItem GetItem(int flattenedIndex);

    /// <summary>
    /// Subscribes to change notifications.
    /// </summary>
    /// <param name="changeCallback">The callback to invoke when changes occur.</param>
    /// <returns>A disposable that unsubscribes from change notifications when disposed.</returns>
    IDisposable Subscribe(Action<VirtualScrollFlattenedChangeSet> changeCallback);
}
