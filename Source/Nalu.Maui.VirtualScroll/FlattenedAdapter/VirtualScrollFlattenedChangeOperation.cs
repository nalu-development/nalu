namespace Nalu;

/// <summary>
/// The operation that caused a flattened (non-sectioned) change.
/// </summary>
internal enum VirtualScrollFlattenedChangeOperation
{
    // ─────────────────────────────
    // Structural / fallback
    // ─────────────────────────────

    /// <summary>
    /// Full data set reset.
    /// Avoid unless absolutely necessary.
    /// </summary>
    Reset,
    
    // ─────────────────────────────
    // Item-level operations
    // ─────────────────────────────

    /// <summary>
    /// Insert a single item at a given index.
    /// </summary>
    InsertItem,

    /// <summary>
    /// Insert a contiguous range of items.
    /// </summary>
    InsertItemRange,

    /// <summary>
    /// Remove a single item at a given index.
    /// </summary>
    RemoveItem,

    /// <summary>
    /// Remove a contiguous range of items.
    /// </summary>
    RemoveItemRange,

    /// <summary>
    /// Replace the item at a given index.
    /// Identity is preserved.
    /// </summary>
    ReplaceItem,

    /// <summary>
    /// Replace a contiguous range of items.
    /// </summary>
    ReplaceItemRange,

    /// <summary>
    /// Move a single item from one index to another.
    /// </summary>
    MoveItem,

    /// <summary>
    /// Refresh the item without changing identity or position.
    /// </summary>
    RefreshItem,
}

