using ILayout = Microsoft.Maui.ILayout;

namespace Nalu;

/// <summary>
/// Defines how remaining space is distributed among items in a <see cref="IWrapLayout"/> that have an expand ratio greater than 0.
/// </summary>
public enum WrapLayoutExpandMode
{
    /// <summary>
    /// Adds remaining space among all items based on their expand ratio.
    /// </summary>
    /// <remarks>
    /// An expand ratio of 0 means the item will not receive any extra space (default).
    /// </remarks>
    Distribute,

    /// <summary>
    /// Adds the remaining space among all items proportionally to their current size and their expand ratio.
    /// </summary>
    /// <remarks>
    /// An expand ratio of 0 means the item will not receive any extra space (default).
    /// </remarks>
    DistributeProportionally,
    
    /// <summary>
    /// After all items with no expand have been measured, divides the remaining space among all items with an expand ratio greater than 0.
    /// </summary>
    /// <remarks>
    /// <para>Items are still placed in rows/columns based on their measured size. If an item's measured size exceeds the available space, it will wrap to the next row/column.</para>
    /// <para>When dividing space, items will never shrink below their measured size. If an item's share of the remaining space is less than its measured size, it keeps its measured size and the remaining space is redistributed among other expanding items.</para>
    /// </remarks>
    Divide
}

/// <summary>
/// Defines the contract for a layout that arranges its children in sequential position from left to right and top to bottom,
/// wrapping to the next line as necessary.
/// </summary>
public interface IWrapLayout : ILayout
{
    /// <summary>
    /// Gets the mode that defines how remaining space is distributed among items within the same line with an expand ratio greater than 0.
    /// </summary>
    WrapLayoutExpandMode ExpandMode { get; }

    /// <summary>
    /// Gets the spacing between items horizontally.
    /// </summary>
    double HorizontalSpacing { get; }

    /// <summary>
    /// Gets the spacing between items vertically.
    /// </summary>
    double VerticalSpacing { get; }

    /// <summary>
    /// Gets the alignment of items within each line.
    /// </summary>
    /// <remarks>
    /// This is only effective when there's remaining space in the line after all items have been arranged.
    /// </remarks>
    WrapLayoutItemsAlignment ItemsAlignment { get; }

    /// <summary>
    /// Gets the expand ratio for the specified view.
    /// </summary>
    /// <param name="view">The view to get the expand ratio from.</param>
    /// <returns>The expand ratio value.</returns>
    double GetExpandRatio(IView view);
}

/// <summary>
/// Defines how items are aligned within each line of a <see cref="IWrapLayout"/>.
/// </summary>
public enum WrapLayoutItemsAlignment
{
    /// <summary>
    /// Indicates that items will be aligned to the start of the line.
    /// </summary>
    /// <remarks>
    /// This is only effective when the <see cref="IWrapLayout"/> has remaining space in the line after all items have been arranged.
    /// </remarks>
    Start,
    /// <summary>
    /// Indicates that items will be aligned in the center of the line.
    /// </summary>
    /// <remarks>
    /// This is only effective when the <see cref="IWrapLayout"/> has remaining space in the line after all items have been arranged.
    /// </remarks>
    Center,
    /// <summary>
    /// Indicates that items will be aligned to the end of the line.
    /// </summary>
    /// <remarks>
    /// This is only effective when the <see cref="IWrapLayout"/> has remaining space in the line after all items have been arranged.
    /// </remarks>
    End
}
