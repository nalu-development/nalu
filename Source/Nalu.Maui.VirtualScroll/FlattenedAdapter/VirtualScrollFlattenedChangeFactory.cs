namespace Nalu;

/// <summary>
/// Factory class for creating flattened (non-sectioned) virtual scroll changes.
/// </summary>
internal static class VirtualScrollFlattenedChangeFactory
{
    private static readonly VirtualScrollFlattenedChange _resetChange = new(VirtualScrollFlattenedChangeOperation.Reset, -1, -1);
    // ─────────────────────────────
    // Item-level factory methods
    // ─────────────────────────────

    /// <summary>
    /// Creates a change for inserting a single item at a given index.
    /// </summary>
    /// <param name="itemIndex">The item index where the item will be inserted.</param>
    /// <returns>A new <see cref="VirtualScrollFlattenedChange"/> instance.</returns>
    public static VirtualScrollFlattenedChange InsertItem(int itemIndex) => new(VirtualScrollFlattenedChangeOperation.InsertItem, itemIndex, itemIndex);

    /// <summary>
    /// Creates a change for inserting a contiguous range of items.
    /// </summary>
    /// <param name="startItemIndex">The starting item index where the items will be inserted.</param>
    /// <param name="endItemIndex">The ending item index where the items will be inserted (inclusive).</param>
    /// <returns>A new <see cref="VirtualScrollFlattenedChange"/> instance.</returns>
    public static VirtualScrollFlattenedChange InsertItemRange(int startItemIndex, int endItemIndex) => new(VirtualScrollFlattenedChangeOperation.InsertItemRange, startItemIndex, endItemIndex);

    /// <summary>
    /// Creates a change for removing a single item at a given index.
    /// </summary>
    /// <param name="itemIndex">The item index where the item will be removed.</param>
    /// <returns>A new <see cref="VirtualScrollFlattenedChange"/> instance.</returns>
    public static VirtualScrollFlattenedChange RemoveItem(int itemIndex) => new(VirtualScrollFlattenedChangeOperation.RemoveItem, itemIndex, itemIndex);

    /// <summary>
    /// Creates a change for removing a contiguous range of items.
    /// </summary>
    /// <param name="startItemIndex">The starting item index where the items will be removed.</param>
    /// <param name="endItemIndex">The ending item index where the items will be removed (inclusive).</param>
    /// <returns>A new <see cref="VirtualScrollFlattenedChange"/> instance.</returns>
    public static VirtualScrollFlattenedChange RemoveItemRange(int startItemIndex, int endItemIndex) => new(VirtualScrollFlattenedChangeOperation.RemoveItemRange, startItemIndex, endItemIndex);

    /// <summary>
    /// Creates a change for replacing the item at a given index.
    /// Identity is preserved.
    /// </summary>
    /// <param name="itemIndex">The item index where the item will be replaced.</param>
    /// <returns>A new <see cref="VirtualScrollFlattenedChange"/> instance.</returns>
    public static VirtualScrollFlattenedChange ReplaceItem(int itemIndex) => new(VirtualScrollFlattenedChangeOperation.ReplaceItem, itemIndex, itemIndex);

    /// <summary>
    /// Creates a change for replacing a contiguous range of items.
    /// </summary>
    /// <param name="startItemIndex">The starting item index where the items will be replaced.</param>
    /// <param name="endItemIndex">The ending item index where the items will be replaced (inclusive).</param>
    /// <returns>A new <see cref="VirtualScrollFlattenedChange"/> instance.</returns>
    public static VirtualScrollFlattenedChange ReplaceItemRange(int startItemIndex, int endItemIndex) => new(VirtualScrollFlattenedChangeOperation.ReplaceItemRange, startItemIndex, endItemIndex);

    /// <summary>
    /// Creates a change for moving a single item from one index to another.
    /// </summary>
    /// <param name="fromItemIndex">The source item index.</param>
    /// <param name="toItemIndex">The destination item index.</param>
    /// <returns>A new <see cref="VirtualScrollFlattenedChange"/> instance.</returns>
    public static VirtualScrollFlattenedChange MoveItem(int fromItemIndex, int toItemIndex) => new(VirtualScrollFlattenedChangeOperation.MoveItem, fromItemIndex, toItemIndex);

    /// <summary>
    /// Creates a change for refreshing the item without changing identity or position.
    /// </summary>
    /// <param name="itemIndex">The item index to refresh.</param>
    /// <returns>A new <see cref="VirtualScrollFlattenedChange"/> instance.</returns>
    public static VirtualScrollFlattenedChange RefreshItem(int itemIndex) => new(VirtualScrollFlattenedChangeOperation.RefreshItem, itemIndex, itemIndex);

    // ─────────────────────────────
    // Structural / fallback factory methods
    // ─────────────────────────────

    /// <summary>
    /// Creates a change for a full data set reset.
    /// Avoid unless absolutely necessary.
    /// </summary>
    /// <returns>A cached <see cref="VirtualScrollFlattenedChange"/> instance.</returns>
    public static VirtualScrollFlattenedChange Reset() => _resetChange;
}

