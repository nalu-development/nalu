namespace Nalu;

/// <summary>
/// Adapter interface for virtual scroll data sources.
/// </summary>
internal interface IVirtualScrollFlattenedAdapter : IDisposable
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
    /// Gets the flattened index for the specified section and item index.
    /// </summary>
    /// <param name="sectionIndex">The index of the section.</param>
    /// <param name="itemIndex">The index of the item within the section. Use -1 to get the section header index.</param>
    /// <returns>The flattened index, or -1 if the indices are invalid.</returns>
    int GetFlattenedIndexForItem(int sectionIndex, int itemIndex);
    
    /// <summary>
    /// Gets the flattened index for the start of the specified section (section header position).
    /// </summary>
    /// <param name="sectionIndex">The index of the section.</param>
    /// <returns>The flattened index for the section start, or -1 if the section index is invalid.</returns>
    int GetFlattenedIndexForSectionStart(int sectionIndex);

    /// <summary>
    /// Subscribes to change notifications.
    /// </summary>
    /// <param name="changeCallback">The callback to invoke when changes occur.</param>
    /// <returns>A disposable that unsubscribes from change notifications when disposed.</returns>
    IDisposable Subscribe(Action<VirtualScrollFlattenedChangeSet> changeCallback);
}
