using System.Runtime.InteropServices;
using Microsoft.Maui.Layouts;

namespace Nalu;

/// <summary>
/// Provides layout logic for an <see cref="IWrapLayout"/> that arranges its children horizontally in sequential position from left to right and top to bottom,
/// wrapping to the next line as necessary.
/// </summary>
public class HorizontalWrapLayoutManager : LayoutManager
{
    /// <summary>
    /// Gets the <see cref="IWrapLayout"/> associated with this layout manager.
    /// </summary>
    public new IWrapLayout Layout { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HorizontalWrapLayoutManager"/> class.
    /// </summary>
    /// <param name="layout">The wrap layout to manage.</param>
    public HorizontalWrapLayoutManager(IWrapLayout layout) : base(layout)
    {
        Layout = layout;
    }

    /// <inheritdoc />
    public override Size Measure(double widthConstraint, double heightConstraint)
    {
        var layout = Layout;
        var padding = layout.Padding;
        var horizontalSpacing = layout.HorizontalSpacing;
        var verticalSpacing = layout.VerticalSpacing;

        var paddingHorizontalThickness = padding.HorizontalThickness;
        var paddingVerticalThickness = padding.VerticalThickness;

        var availableWidth = widthConstraint - paddingHorizontalThickness;
        var canWrap = !double.IsPositiveInfinity(availableWidth);

        var childHeightConstraint = heightConstraint - paddingVerticalThickness;

        double totalHeight = 0;
        double maxRowWidth = 0;
        double currentRowWidth = 0;
        double currentRowHeight = 0;
        var isFirstInRow = true;

        var childrenCount = layout.Count;

        for (var n = 0; n < childrenCount; n++)
        {
            var child = layout[n];

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            // Measure the child with the available width constraint and available height
            var measure = child.Measure(availableWidth, childHeightConstraint);

            var childWidth = measure.Width;
            var childHeight = measure.Height;

            // Check if we need to wrap to the next line
            var widthWithSpacing = isFirstInRow ? childWidth : childWidth + horizontalSpacing;

            if (canWrap && !isFirstInRow && currentRowWidth + widthWithSpacing > availableWidth)
            {
                // Wrap to next line
                totalHeight += currentRowHeight + verticalSpacing;
                maxRowWidth = Math.Max(maxRowWidth, currentRowWidth);
                currentRowWidth = childWidth;
                currentRowHeight = childHeight;
                isFirstInRow = false;
            }
            else
            {
                // Add to current row
                currentRowWidth += widthWithSpacing;
                currentRowHeight = Math.Max(currentRowHeight, childHeight);
                isFirstInRow = false;
            }
        }

        // Account for the last row
        if (currentRowHeight > 0)
        {
            totalHeight += currentRowHeight;
            maxRowWidth = Math.Max(maxRowWidth, currentRowWidth);
        }

        var measuredWidth = maxRowWidth + paddingHorizontalThickness;
        var measuredHeight = totalHeight + paddingVerticalThickness;

        IView layoutView = layout;
        var finalHeight = ResolveConstraints(heightConstraint, layoutView.Height, measuredHeight, layoutView.MinimumHeight, layoutView.MaximumHeight);
        var finalWidth = ResolveConstraints(widthConstraint, layoutView.Width, measuredWidth, layoutView.MinimumWidth, layoutView.MaximumWidth);

        return new Size(finalWidth, finalHeight);
    }

    /// <inheritdoc />
    public override Size ArrangeChildren(Rect bounds)
    {
        var layout = Layout;
        var padding = layout.Padding;
        var horizontalSpacing = layout.HorizontalSpacing;
        var verticalSpacing = layout.VerticalSpacing;
        var expandMode = layout.ExpandMode;
        var itemsAlignment = layout.ItemsAlignment;

        var left = bounds.Left + padding.Left;
        var top = bounds.Top + padding.Top;
        var availableWidth = bounds.Width - padding.HorizontalThickness;

        // First pass: organize children into rows and measure them
        var rows = OrganizeIntoRows(availableWidth, horizontalSpacing);

        // Second pass: arrange children row by row
        var yPosition = top;

        foreach (var row in rows)
        {
            if (row.Children.Count == 0)
            {
                continue;
            }

            // Calculate remaining space and apply expand logic
            // Note: row.TotalWidth already includes spacing between items
            var remainingWidth = availableWidth - row.TotalWidth;

            if (remainingWidth > 0 && row.TotalExpandRatio > 0)
            {
                ApplyExpand(row, remainingWidth, expandMode, availableWidth, horizontalSpacing);
                remainingWidth = 0;
            }

            // Calculate starting X position based on alignment
            var xPosition = left + GetAlignmentOffset(remainingWidth, itemsAlignment);

            var childrenSpan = CollectionsMarshal.AsSpan(row.Children);
            for (var i = 0; i < childrenSpan.Length; i++)
            {
                var childInfo = childrenSpan[i];
                var destination = new Rect(xPosition, yPosition, childInfo.ArrangeWidth, row.Height);
                childInfo.Child.Arrange(destination);

                xPosition += childInfo.ArrangeWidth + horizontalSpacing;
            }

            yPosition += row.Height + verticalSpacing;
        }

        return new Size(bounds.Width, bounds.Height);
    }

    private static double GetAlignmentOffset(double remainingSpace, WrapLayoutItemsAlignment alignment)
    {
        if (remainingSpace <= 0)
        {
            return 0;
        }

        return alignment switch
        {
            WrapLayoutItemsAlignment.Center => remainingSpace / 2,
            WrapLayoutItemsAlignment.End => remainingSpace,
            _ => 0
        };
    }

    private List<RowInfo> OrganizeIntoRows(double availableWidth, double horizontalSpacing)
    {
        var layout = Layout;
        var rows = new List<RowInfo>();
        var currentRow = RowInfo.Create();
        var isFirstInRow = true;
        var canWrap = !double.IsPositiveInfinity(availableWidth);

        var layoutCount = layout.Count;

        for (var n = 0; n < layoutCount; n++)
        {
            var child = layout[n];

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var desiredWidth = child.DesiredSize.Width;
            var desiredHeight = child.DesiredSize.Height;
            var expandRatio = layout.GetExpandRatio(child);

            var widthWithSpacing = isFirstInRow ? desiredWidth : desiredWidth + horizontalSpacing;

            // Check if we need to wrap to the next line
            if (canWrap && !isFirstInRow && currentRow.TotalWidth + widthWithSpacing > availableWidth)
            {
                // Finalize current row and start new one
                rows.Add(currentRow);
                currentRow = RowInfo.Create();
                isFirstInRow = true;
                // Recalculate widthWithSpacing since isFirstInRow changed
                widthWithSpacing = desiredWidth;
            }

            var childInfo = new ChildInfo
            {
                Child = child,
                DesiredWidth = desiredWidth,
                ArrangeWidth = desiredWidth,
                ExpandRatio = expandRatio
            };

            // Track non-expanding items width for Divide mode
            if (expandRatio == 0)
            {
                currentRow.NonExpandingWidth += desiredWidth;
            }

            currentRow.Children.Add(childInfo);
            currentRow.TotalWidth += widthWithSpacing;
            currentRow.Height = Math.Max(currentRow.Height, desiredHeight);
            currentRow.TotalExpandRatio += expandRatio;
            
            isFirstInRow = false;
        }

        // Add the last row if it has children
        if (currentRow.Children.Count > 0)
        {
            rows.Add(currentRow);
        }

        return rows;
    }

    private static void ApplyExpand(RowInfo row, double remainingWidth, WrapLayoutExpandMode expandMode, double availableWidth, double horizontalSpacing)
    {
        switch (expandMode)
        {
            case WrapLayoutExpandMode.Distribute:
                DistributeRemainingSpace(row, remainingWidth);
                break;

            case WrapLayoutExpandMode.DistributeProportionally:
                DistributeProportionally(row, remainingWidth);
                break;

            case WrapLayoutExpandMode.Divide:
                DivideRemainingSpace(row, availableWidth, horizontalSpacing);
                break;
        }
    }

    private static void DistributeRemainingSpace(RowInfo row, double remainingWidth)
    {
        // Distribute space based on expand ratio
        var childrenSpan = CollectionsMarshal.AsSpan(row.Children);
        for (var i = 0; i < childrenSpan.Length; i++)
        {
            ref var childInfo = ref childrenSpan[i];
            if (childInfo.ExpandRatio > 0)
            {
                var extraWidth = remainingWidth * (childInfo.ExpandRatio / row.TotalExpandRatio);
                childInfo.ArrangeWidth += extraWidth;
            }
        }
    }
    
    private static void DivideRemainingSpace(RowInfo row, double availableWidth, double horizontalSpacing)
    {
        // Calculate remaining space: available width minus non-expanding items and total spacing
        var childrenSpan = CollectionsMarshal.AsSpan(row.Children);
        var totalSpacing = childrenSpan.Length > 1 ? horizontalSpacing * (childrenSpan.Length - 1) : 0;
        var remainingSpace = availableWidth - row.NonExpandingWidth - totalSpacing;
        
        // If no remaining space or no expanding items, keep desired sizes
        if (remainingSpace <= 0 || row.TotalExpandRatio <= 0)
        {
            return;
        }
        
        // Divide remaining space among expanding items, weighted by expand ratio
        // Items should never shrink below their desired size
        var activeExpandRatio = row.TotalExpandRatio;
        var spaceToDistribute = remainingSpace;
        
        // Iteratively lock items that would shrink below desired size
        bool lockedAny;
        do
        {
            lockedAny = false;
            for (var i = 0; i < childrenSpan.Length; i++)
            {
                ref var childInfo = ref childrenSpan[i];
                // Skip non-expanding or already locked items (ArrangeWidth != DesiredWidth means locked)
                if (childInfo.ExpandRatio <= 0 || childInfo.ArrangeWidth != childInfo.DesiredWidth)
                {
                    continue;
                }
                
                var share = spaceToDistribute * (childInfo.ExpandRatio / activeExpandRatio);
                if (share < childInfo.DesiredWidth)
                {
                    // Lock at desired size
                    spaceToDistribute -= childInfo.DesiredWidth;
                    activeExpandRatio -= childInfo.ExpandRatio;
                    // Mark as locked by setting a sentinel value temporarily
                    childInfo.ArrangeWidth = -1;
                    lockedAny = true;
                }
            }
        } while (lockedAny && activeExpandRatio > 0);
        
        // Assign final widths
        for (var i = 0; i < childrenSpan.Length; i++)
        {
            ref var childInfo = ref childrenSpan[i];
            if (childInfo.ExpandRatio <= 0)
            {
                continue;
            }
            
            if (childInfo.ArrangeWidth < 0)
            {
                // Locked item - restore desired width
                childInfo.ArrangeWidth = childInfo.DesiredWidth;
            }
            else if (activeExpandRatio > 0)
            {
                // Unlocked item - gets its share
                childInfo.ArrangeWidth = spaceToDistribute * (childInfo.ExpandRatio / activeExpandRatio);
            }
        }
    }

    private static void DistributeProportionally(RowInfo row, double remainingWidth)
    {
        // Calculate weighted total (size * expand ratio)
        var weightedTotal = 0.0;
        var childrenSpan = CollectionsMarshal.AsSpan(row.Children);

        foreach (var childInfo in childrenSpan)
        {
            if (childInfo.ExpandRatio > 0)
            {
                weightedTotal += childInfo.DesiredWidth * childInfo.ExpandRatio;
            }
        }

        if (weightedTotal <= 0)
        {
            return;
        }

        // Distribute proportionally to current size and expand ratio
        for (var i = 0; i < childrenSpan.Length; i++)
        {
            ref var childInfo = ref childrenSpan[i];
            if (childInfo.ExpandRatio > 0)
            {
                var weight = childInfo.DesiredWidth * childInfo.ExpandRatio;
                var extraWidth = remainingWidth * (weight / weightedTotal);
                childInfo.ArrangeWidth += extraWidth;
            }
        }
    }


    private struct RowInfo
    {
        public List<ChildInfo> Children { get; init; }
        public double TotalWidth { get; set; }
        public double Height { get; set; }
        public double TotalExpandRatio { get; set; }
        public double NonExpandingWidth { get; set; }

        public static RowInfo Create() => new() { Children = [] };
    }

    private struct ChildInfo
    {
        public IView Child { get; init; }
        public double DesiredWidth { get; init; }
        public double ArrangeWidth { get; set; }
        public double ExpandRatio { get; init; }
    }
}
