namespace Nalu;

/// <summary>
/// Describes a change in the virtual scroll data.
/// </summary>
public sealed class VirtualScrollChange
{
    /// <summary>
    /// Gets the operation that caused the change.
    /// </summary>
    public VirtualScrollChangeOperation Operation { get; }

    /// <summary>
    /// Gets a value indicating whether the change is at the section level.
    /// </summary>
    public bool IsSectionChange => Operation >= VirtualScrollChangeOperation.InsertSection;

    /// <summary>
    /// Gets the section index affected by the change.
    /// </summary>
    public int StartSectionIndex { get; }

    /// <summary>
    /// Gets the item index affected by the change.
    /// </summary>
    /// <remarks>
    /// -1 indicates that the change is at the section level (e.g., inserting or removing a section).
    /// </remarks>
    public int StartItemIndex { get; }
    
    /// <summary>
    /// Gets the end section index affected by the change.
    /// </summary>
    public int EndSectionIndex { get; }
    
    /// <summary>
    /// Gets the end item index affected by the change.
    /// </summary>
    /// <remarks>
    /// -1 indicates that the change is at the section level (e.g., inserting or removing a section).
    /// </remarks>
    public int EndItemIndex { get; }

    private VirtualScrollChange(VirtualScrollChangeOperation operation, int startSectionIndex, int startItemIndex, int endSectionIndex, int endItemIndex)
    {
        Operation = operation;
        StartSectionIndex = startSectionIndex;
        StartItemIndex = startItemIndex;
        EndSectionIndex = endSectionIndex;
        EndItemIndex = endItemIndex;
    }

    // ─────────────────────────────
    // Item-level factory methods
    // ─────────────────────────────

    /// <summary>
    /// Creates a change for inserting a single item at a given index.
    /// </summary>
    /// <param name="sectionIndex">The section index where the item will be inserted.</param>
    /// <param name="itemIndex">The item index where the item will be inserted.</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange InsertItem(int sectionIndex, int itemIndex) => new(VirtualScrollChangeOperation.InsertItem, sectionIndex, itemIndex, sectionIndex, itemIndex);

    /// <summary>
    /// Creates a change for inserting a contiguous range of items.
    /// </summary>
    /// <param name="sectionIndex">The section index where the items will be inserted.</param>
    /// <param name="startItemIndex">The starting item index where the items will be inserted.</param>
    /// <param name="endItemIndex">The ending item index where the items will be inserted (inclusive).</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange InsertItemRange(int sectionIndex, int startItemIndex, int endItemIndex) => new(VirtualScrollChangeOperation.InsertItemRange, sectionIndex, startItemIndex, sectionIndex, endItemIndex);

    /// <summary>
    /// Creates a change for removing a single item at a given index.
    /// </summary>
    /// <param name="sectionIndex">The section index where the item will be removed.</param>
    /// <param name="itemIndex">The item index where the item will be removed.</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange RemoveItem(int sectionIndex, int itemIndex) => new(VirtualScrollChangeOperation.RemoveItem, sectionIndex, itemIndex, sectionIndex, itemIndex);

    /// <summary>
    /// Creates a change for removing a contiguous range of items.
    /// </summary>
    /// <param name="sectionIndex">The section index where the items will be removed.</param>
    /// <param name="startItemIndex">The starting item index where the items will be removed.</param>
    /// <param name="endItemIndex">The ending item index where the items will be removed (inclusive).</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange RemoveItemRange(int sectionIndex, int startItemIndex, int endItemIndex) => new(VirtualScrollChangeOperation.RemoveItemRange, sectionIndex, startItemIndex, sectionIndex, endItemIndex);

    /// <summary>
    /// Creates a change for replacing the item at a given index.
    /// Identity is preserved.
    /// </summary>
    /// <param name="sectionIndex">The section index where the item will be replaced.</param>
    /// <param name="itemIndex">The item index where the item will be replaced.</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange ReplaceItem(int sectionIndex, int itemIndex) => new(VirtualScrollChangeOperation.ReplaceItem, sectionIndex, itemIndex, sectionIndex, itemIndex);

    /// <summary>
    /// Creates a change for replacing a contiguous range of items.
    /// </summary>
    /// <param name="sectionIndex">The section index where the items will be replaced.</param>
    /// <param name="startItemIndex">The starting item index where the items will be replaced.</param>
    /// <param name="endItemIndex">The ending item index where the items will be replaced (inclusive).</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange ReplaceItemRange(int sectionIndex, int startItemIndex, int endItemIndex) => new(VirtualScrollChangeOperation.ReplaceItemRange, sectionIndex, startItemIndex, sectionIndex, endItemIndex);

    /// <summary>
    /// Creates a change for moving a single item from one index to another.
    /// </summary>
    /// <param name="sectionIndex">The section index where the item is located.</param>
    /// <param name="fromItemIndex">The source item index.</param>
    /// <param name="toItemIndex">The destination item index.</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange MoveItem(int sectionIndex, int fromItemIndex, int toItemIndex) => new(VirtualScrollChangeOperation.MoveItem, sectionIndex, fromItemIndex, sectionIndex, toItemIndex);

    /// <summary>
    /// Creates a change for refreshing the item without changing identity or position.
    /// </summary>
    /// <param name="sectionIndex">The section index where the item is located.</param>
    /// <param name="itemIndex">The item index to refresh.</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange RefreshItem(int sectionIndex, int itemIndex) => new(VirtualScrollChangeOperation.RefreshItem, sectionIndex, itemIndex, sectionIndex, itemIndex);

    // ─────────────────────────────
    // Section-level factory methods
    // ─────────────────────────────

    /// <summary>
    /// Creates a change for inserting a section (header + items).
    /// </summary>
    /// <param name="sectionIndex">The section index where the section will be inserted.</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange InsertSection(int sectionIndex) => new(VirtualScrollChangeOperation.InsertSection, sectionIndex, -1, sectionIndex, -1);

    /// <summary>
    /// Creates a change for inserting multiple contiguous sections.
    /// </summary>
    /// <param name="startSectionIndex">The starting section index where the sections will be inserted.</param>
    /// <param name="endSectionIndex">The ending section index where the sections will be inserted (inclusive).</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange InsertSectionRange(int startSectionIndex, int endSectionIndex) => new(VirtualScrollChangeOperation.InsertSectionRange, startSectionIndex, -1, endSectionIndex, -1);

    /// <summary>
    /// Creates a change for removing a section (header + items).
    /// </summary>
    /// <param name="sectionIndex">The section index where the section will be removed.</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange RemoveSection(int sectionIndex) => new(VirtualScrollChangeOperation.RemoveSection, sectionIndex, -1, sectionIndex, -1);

    /// <summary>
    /// Creates a change for removing multiple contiguous sections.
    /// </summary>
    /// <param name="startSectionIndex">The starting section index where the sections will be removed.</param>
    /// <param name="endSectionIndex">The ending section index where the sections will be removed (inclusive).</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange RemoveSectionRange(int startSectionIndex, int endSectionIndex) => new(VirtualScrollChangeOperation.RemoveSectionRange, startSectionIndex, -1, endSectionIndex, -1);

    /// <summary>
    /// Creates a change for replacing all items in a section.
    /// Section identity is preserved.
    /// </summary>
    /// <param name="sectionIndex">The section index where the section will be replaced.</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange ReplaceSection(int sectionIndex) => new(VirtualScrollChangeOperation.ReplaceSection, sectionIndex, -1, sectionIndex, -1);

    /// <summary>
    /// Creates a change for replacing multiple contiguous sections.
    /// </summary>
    /// <param name="startSectionIndex">The starting section index where the sections will be replaced.</param>
    /// <param name="endSectionIndex">The ending section index where the sections will be replaced (inclusive).</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange ReplaceSectionRange(int startSectionIndex, int endSectionIndex) => new(VirtualScrollChangeOperation.ReplaceSectionRange, startSectionIndex, -1, endSectionIndex, -1);

    /// <summary>
    /// Creates a change for moving a section from one index to another.
    /// </summary>
    /// <param name="fromSectionIndex">The source section index.</param>
    /// <param name="toSectionIndex">The destination section index.</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange MoveSection(int fromSectionIndex, int toSectionIndex) => new(VirtualScrollChangeOperation.MoveSection, fromSectionIndex, -1, toSectionIndex, -1);

    /// <summary>
    /// Creates a change for refreshing a section without changing its structure.
    /// </summary>
    /// <param name="sectionIndex">The section index to refresh.</param>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange RefreshSection(int sectionIndex) => new(VirtualScrollChangeOperation.RefreshSection, sectionIndex, -1, sectionIndex, -1);

    // ─────────────────────────────
    // Structural / fallback factory methods
    // ─────────────────────────────

    /// <summary>
    /// Creates a change for a full data set reset.
    /// Avoid unless absolutely necessary.
    /// </summary>
    /// <returns>A new <see cref="VirtualScrollChange"/> instance.</returns>
    public static VirtualScrollChange Reset() => new(VirtualScrollChangeOperation.Reset, -1, -1, -1, -1);
}
