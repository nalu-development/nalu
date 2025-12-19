namespace Nalu;

/// <summary>
/// The operation that caused the change.
/// Extends <see cref="VirtualScrollFlattenedChangeOperation"/> with section-level operations.
/// </summary>
public enum VirtualScrollChangeOperation
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

    // ─────────────────────────────
    // Section-level operations
    // ─────────────────────────────

    /// <summary>
    /// Insert a section (header + items).
    /// </summary>
    InsertSection,

    /// <summary>
    /// Insert multiple contiguous sections.
    /// </summary>
    InsertSectionRange,

    /// <summary>
    /// Remove a section (header + items).
    /// </summary>
    RemoveSection,

    /// <summary>
    /// Remove multiple contiguous sections.
    /// </summary>
    RemoveSectionRange,

    /// <summary>
    /// Replace all items in a section.
    /// Section identity is preserved.
    /// </summary>
    ReplaceSection,

    /// <summary>
    /// Replace multiple contiguous sections.
    /// </summary>
    ReplaceSectionRange,

    /// <summary>
    /// Move a section from one index to another.
    /// </summary>
    MoveSection,

    /// <summary>
    /// Refresh a section without changing its structure.
    /// </summary>
    RefreshSection,
}
