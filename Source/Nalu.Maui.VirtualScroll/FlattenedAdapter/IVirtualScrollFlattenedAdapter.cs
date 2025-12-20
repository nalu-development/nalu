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
    /// Converts a flattened index to section and item indices.
    /// </summary>
    /// <param name="flattenedIndex">The flattened index to convert.</param>
    /// <param name="sectionIndex">When this method returns, contains the section index, or -1 if the flattened index is invalid or points to a header/footer.</param>
    /// <param name="itemIndex">When this method returns, contains the item index within the section, or -1 if the flattened index is invalid or points to a header/footer.</param>
    /// <returns>True if the flattened index corresponds to an item (not a header/footer), false otherwise.</returns>
    bool TryGetSectionAndItemIndex(int flattenedIndex, out int sectionIndex, out int itemIndex);
    
    /// <summary>
    /// Gets the position type and section index for a flattened index.
    /// </summary>
    /// <param name="flattenedIndex">The flattened index.</param>
    /// <param name="positionType">When this method returns, contains the position type.</param>
    /// <param name="sectionIndex">When this method returns, contains the section index. For global header/footer, this will be -1 or -2. For section header/footer, this will be the actual section index. For items, this will be the section index.</param>
    /// <returns>True if the flattened index is valid, false otherwise.</returns>
    bool TryGetPositionInfo(int flattenedIndex, out VirtualScrollFlattenedPositionType positionType, out int sectionIndex);

    /// <summary>
    /// Subscribes to change notifications.
    /// </summary>
    /// <param name="changeCallback">The callback to invoke when changes occur.</param>
    /// <returns>A disposable that unsubscribes from change notifications when disposed.</returns>
    IDisposable Subscribe(Action<VirtualScrollFlattenedChangeSet> changeCallback);
}
