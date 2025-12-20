namespace Nalu;

/// <summary>
/// Represents a range of visible items in a VirtualScroll.
/// </summary>
public readonly record struct VirtualScrollRange
{
    /// <summary>
    /// Special section index value indicating the global header.
    /// Using <see cref="int.MinValue"/> ensures it comes first in natural ordering.
    /// </summary>
    public const int GlobalHeaderSectionIndex = int.MinValue;

    /// <summary>
    /// Special section index value indicating the global footer.
    /// Using <see cref="int.MaxValue"/> ensures it comes last in natural ordering.
    /// </summary>
    public const int GlobalFooterSectionIndex = int.MaxValue;

    /// <summary>
    /// Special item index value indicating a section header.
    /// Using <see cref="int.MinValue"/> ensures it comes first within a section in natural ordering.
    /// </summary>
    public const int SectionHeaderItemIndex = int.MinValue;

    /// <summary>
    /// Special item index value indicating a section footer.
    /// Using <see cref="int.MaxValue"/> ensures it comes last within a section in natural ordering.
    /// </summary>
    public const int SectionFooterItemIndex = int.MaxValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualScrollRange"/> struct.
    /// </summary>
    /// <param name="startSectionIndex">The section index of the first visible item. Use <see cref="GlobalHeaderSectionIndex"/> for global header, <see cref="GlobalFooterSectionIndex"/> for global footer.</param>
    /// <param name="startItemIndex">The item index within the start section of the first visible item. Use <see cref="SectionHeaderItemIndex"/> for section header, <see cref="SectionFooterItemIndex"/> for section footer.</param>
    /// <param name="endSectionIndex">The section index of the last visible item. Use <see cref="GlobalHeaderSectionIndex"/> for global header, <see cref="GlobalFooterSectionIndex"/> for global footer.</param>
    /// <param name="endItemIndex">The item index within the end section of the last visible item. Use <see cref="SectionHeaderItemIndex"/> for section header, <see cref="SectionFooterItemIndex"/> for section footer.</param>
    public VirtualScrollRange(int startSectionIndex, int startItemIndex, int endSectionIndex, int endItemIndex)
    {
        StartSectionIndex = startSectionIndex;
        StartItemIndex = startItemIndex;
        EndSectionIndex = endSectionIndex;
        EndItemIndex = endItemIndex;
    }

    /// <summary>
    /// Gets the section index of the first visible item.
    /// </summary>
    public int StartSectionIndex { get; }

    /// <summary>
    /// Gets the item index within the start section of the first visible item.
    /// </summary>
    public int StartItemIndex { get; }

    /// <summary>
    /// Gets the section index of the last visible item.
    /// </summary>
    public int EndSectionIndex { get; }

    /// <summary>
    /// Gets the item index within the end section of the last visible item.
    /// </summary>
    public int EndItemIndex { get; }
}

