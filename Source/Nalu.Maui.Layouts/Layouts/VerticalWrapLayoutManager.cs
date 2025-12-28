using System.Runtime.InteropServices;
using Microsoft.Maui.Layouts;

namespace Nalu;

/// <summary>
/// Provides layout logic for an <see cref="IWrapLayout"/> that arranges its children vertically in sequential position from top to bottom and left to right,
/// wrapping to the next column as necessary.
/// </summary>
public class VerticalWrapLayoutManager : LayoutManager
{
    /// <summary>
    /// Gets the <see cref="IWrapLayout"/> associated with this layout manager.
    /// </summary>
    public new IWrapLayout Layout { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="VerticalWrapLayoutManager"/> class.
    /// </summary>
    /// <param name="layout">The wrap layout to manage.</param>
    public VerticalWrapLayoutManager(IWrapLayout layout) : base(layout)
    {
        Layout = layout;
    }

    /// <inheritdoc />
    public override Size Measure(double widthConstraint, double heightConstraint)
    {
        var padding = Layout.Padding;
        var horizontalSpacing = Layout.HorizontalSpacing;
        var verticalSpacing = Layout.VerticalSpacing;

        var paddingHorizontalThickness = padding.HorizontalThickness;
        var paddingVerticalThickness = padding.VerticalThickness;

        var availableHeight = heightConstraint - paddingVerticalThickness;

        var childWidthConstraint = widthConstraint - paddingHorizontalThickness;

        double totalWidth = 0;
        double maxColumnHeight = 0;
        double currentColumnHeight = 0;
        double currentColumnWidth = 0;
        var isFirstInColumn = true;

        var childrenCount = Layout.Count;

        for (var n = 0; n < childrenCount; n++)
        {
            var child = Layout[n];

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            // Measure the child with the available height constraint and available width
            var measure = child.Measure(childWidthConstraint, availableHeight);

            var childWidth = measure.Width;
            var childHeight = measure.Height;

            // Check if we need to wrap to the next column
            var heightWithSpacing = isFirstInColumn ? childHeight : childHeight + verticalSpacing;

            if (!double.IsPositiveInfinity(availableHeight) && !isFirstInColumn && currentColumnHeight + heightWithSpacing > availableHeight)
            {
                // Wrap to next column
                totalWidth += currentColumnWidth + horizontalSpacing;
                maxColumnHeight = Math.Max(maxColumnHeight, currentColumnHeight);
                currentColumnHeight = childHeight;
                currentColumnWidth = childWidth;
                isFirstInColumn = false;
            }
            else
            {
                // Add to current column
                currentColumnHeight += heightWithSpacing;
                currentColumnWidth = Math.Max(currentColumnWidth, childWidth);
                isFirstInColumn = false;
            }
        }

        // Account for the last column
        if (currentColumnWidth > 0)
        {
            totalWidth += currentColumnWidth;
            maxColumnHeight = Math.Max(maxColumnHeight, currentColumnHeight);
        }

        var measuredWidth = totalWidth + paddingHorizontalThickness;
        var measuredHeight = maxColumnHeight + paddingVerticalThickness;

        IView layoutView = Layout;
        var finalHeight = ResolveConstraints(heightConstraint, layoutView.Height, measuredHeight, layoutView.MinimumHeight, layoutView.MaximumHeight);
        var finalWidth = ResolveConstraints(widthConstraint, layoutView.Width, measuredWidth, layoutView.MinimumWidth, layoutView.MaximumWidth);

        return new Size(finalWidth, finalHeight);
    }

    /// <inheritdoc />
    public override Size ArrangeChildren(Rect bounds)
    {
        var padding = Layout.Padding;
        var horizontalSpacing = Layout.HorizontalSpacing;
        var verticalSpacing = Layout.VerticalSpacing;
        var expandMode = Layout.ExpandMode;
        var itemsAlignment = Layout.ItemsAlignment;

        var left = bounds.Left + padding.Left;
        var top = bounds.Top + padding.Top;
        var availableHeight = bounds.Height - padding.VerticalThickness;

        // First pass: organize children into columns and measure them
        var columns = OrganizeIntoColumns(availableHeight, verticalSpacing);

        // Second pass: arrange children column by column
        var xPosition = left;

        foreach (var column in columns)
        {
            if (column.Children.Count == 0)
            {
                continue;
            }

            // Calculate remaining space and apply expand logic
            // Note: column.TotalHeight already includes spacing between items
            var remainingHeight = availableHeight - column.TotalHeight;

            if (remainingHeight > 0 && column.TotalExpandRatio > 0)
            {
                ApplyExpand(column, remainingHeight, expandMode);
                remainingHeight = 0;
            }

            // Calculate starting Y position based on alignment
            var yPosition = top + GetAlignmentOffset(remainingHeight, itemsAlignment);

            foreach (var childInfo in column.Children)
            {
                var destination = new Rect(xPosition, yPosition, column.Width, childInfo.ArrangeHeight);
                childInfo.Child.Arrange(destination);

                yPosition += childInfo.ArrangeHeight + verticalSpacing;
            }

            xPosition += column.Width + horizontalSpacing;
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

    private List<ColumnInfo> OrganizeIntoColumns(double availableHeight, double verticalSpacing)
    {
        var columns = new List<ColumnInfo>();
        var currentColumn = new ColumnInfo();
        var isFirstInColumn = true;

        var childrenCount = Layout.Count;

        for (var n = 0; n < childrenCount; n++)
        {
            var child = Layout[n];

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var desiredWidth = child.DesiredSize.Width;
            var desiredHeight = child.DesiredSize.Height;
            var expandRatio = Layout.GetExpandRatio(child);

            var heightWithSpacing = isFirstInColumn ? desiredHeight : desiredHeight + verticalSpacing;

            // Check if we need to wrap to the next column
            if (!double.IsPositiveInfinity(availableHeight) && !isFirstInColumn && currentColumn.TotalHeight + heightWithSpacing > availableHeight)
            {
                // Finalize current column and start new one
                columns.Add(currentColumn);
                currentColumn = new ColumnInfo();
                isFirstInColumn = true;
            }

            var childInfo = new ChildInfo
            {
                Child = child,
                DesiredHeight = desiredHeight,
                ArrangeHeight = desiredHeight,
                ExpandRatio = expandRatio
            };

            currentColumn.Children.Add(childInfo);
            currentColumn.TotalHeight += isFirstInColumn ? desiredHeight : desiredHeight + verticalSpacing;
            currentColumn.Width = Math.Max(currentColumn.Width, desiredWidth);
            currentColumn.TotalExpandRatio += expandRatio;
            isFirstInColumn = false;
        }

        // Add the last column if it has children
        if (currentColumn.Children.Count > 0)
        {
            columns.Add(currentColumn);
        }

        return columns;
    }

    private static void ApplyExpand(ColumnInfo column, double remainingHeight, WrapLayoutExpandMode expandMode)
    {
        switch (expandMode)
        {
            case WrapLayoutExpandMode.Distribute:
                DistributeRemainingSpace(column, remainingHeight);
                break;

            case WrapLayoutExpandMode.DistributeProportionally:
                DistributeProportionally(column, remainingHeight);
                break;

            case WrapLayoutExpandMode.Divide:
                DivideRemainingSpace(column, remainingHeight);
                break;
        }
    }

    private static void DistributeRemainingSpace(ColumnInfo column, double remainingHeight)
    {
        // Distribute space based on expand ratio
        var childrenSpan = CollectionsMarshal.AsSpan(column.Children);
        for (var i = 0; i < childrenSpan.Length; i++)
        {
            ref var childInfo = ref childrenSpan[i];
            if (childInfo.ExpandRatio > 0)
            {
                var extraHeight = remainingHeight * (childInfo.ExpandRatio / column.TotalExpandRatio);
                childInfo.ArrangeHeight += extraHeight;
            }
        }
    }

    private static void DistributeProportionally(ColumnInfo column, double remainingHeight)
    {
        // Calculate weighted total (size * expand ratio)
        var weightedTotal = 0.0;
        var childrenSpan = CollectionsMarshal.AsSpan(column.Children);

        foreach (var childInfo in childrenSpan)
        {
            if (childInfo.ExpandRatio > 0)
            {
                weightedTotal += childInfo.DesiredHeight * childInfo.ExpandRatio;
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
                var weight = childInfo.DesiredHeight * childInfo.ExpandRatio;
                var extraHeight = remainingHeight * (weight / weightedTotal);
                childInfo.ArrangeHeight += extraHeight;
            }
        }
    }

    private static void DivideRemainingSpace(ColumnInfo column, double remainingHeight)
    {
        // Count items with expand ratio > 0
        var expandingCount = 0;
        var childrenSpan = CollectionsMarshal.AsSpan(column.Children);

        foreach (var childInfo in childrenSpan)
        {
            if (childInfo.ExpandRatio > 0)
            {
                expandingCount++;
            }
        }

        if (expandingCount == 0)
        {
            return;
        }

        // Divide space equally among expanding items, weighted by expand ratio
        for (var i = 0; i < childrenSpan.Length; i++)
        {
            ref var childInfo = ref childrenSpan[i];
            if (childInfo.ExpandRatio > 0)
            {
                var extraHeight = remainingHeight * (childInfo.ExpandRatio / column.TotalExpandRatio);
                childInfo.ArrangeHeight += extraHeight;
            }
        }
    }

    private class ColumnInfo
    {
        public List<ChildInfo> Children { get; } = [];
        public double TotalHeight { get; set; }
        public double Width { get; set; }
        public double TotalExpandRatio { get; set; }
    }

    private struct ChildInfo
    {
        public IView Child { get; init; }
        public double DesiredHeight { get; init; }
        public double ArrangeHeight { get; set; }
        public double ExpandRatio { get; init; }
    }
}

