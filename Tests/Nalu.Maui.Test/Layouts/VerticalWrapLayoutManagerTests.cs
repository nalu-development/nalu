using Microsoft.Maui.Layouts;

namespace Nalu.Maui.Test.Layouts;

public class VerticalWrapLayoutManagerTests
{
    /// <summary>
    /// A test view that returns a specific size when measured.
    /// </summary>
    private class TestView : View
    {
        private readonly double _width;
        private readonly double _height;

        public TestView(double width, double height)
        {
            _width = width;
            _height = height;
        }

        protected override Size MeasureOverride(double widthConstraint, double heightConstraint)
        {
            var finalWidth = Math.Min(_width, widthConstraint);
            var finalHeight = Math.Min(_height, heightConstraint);
            return new Size(finalWidth, finalHeight);
        }
    }

    /// <summary>
    /// A test wrap layout that implements IWrapLayout for testing purposes.
    /// </summary>
    private class TestWrapLayout : Layout, IWrapLayout
    {
        private readonly Dictionary<IView, double> _expandRatios = new();

        public WrapLayoutExpandMode ExpandMode { get; set; } = WrapLayoutExpandMode.Distribute;
        public double HorizontalSpacing { get; set; }
        public double VerticalSpacing { get; set; }
        public WrapLayoutItemsAlignment ItemsAlignment { get; set; } = WrapLayoutItemsAlignment.Start;

        public double GetExpandRatio(IView view) => _expandRatios.GetValueOrDefault(view, 0);

        public void SetExpandRatio(IView view, double ratio) => _expandRatios[view] = ratio;

        protected override ILayoutManager CreateLayoutManager() => new VerticalWrapLayoutManager(this);
    }

    private static VerticalWrapLayoutManager CreateLayoutManager(TestWrapLayout layout) => new VerticalWrapLayoutManager(layout);

    #region Measure Tests - Basic Scenarios

    [Fact(DisplayName = "Measure should return zero size for empty layout")]
    public void Measure_EmptyLayout_ReturnsZeroSize()
    {
        // Arrange
        var layout = new TestWrapLayout();
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 300);

        // Assert
        result.Width.Should().Be(0);
        result.Height.Should().Be(0);
    }

    [Fact(DisplayName = "Measure should return child size for single child")]
    public void Measure_SingleChild_ReturnsChildSize()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(50, 100));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 300);

        // Assert
        result.Width.Should().Be(50);
        result.Height.Should().Be(100);
    }

    [Fact(DisplayName = "Measure should include padding")]
    public void Measure_WithPadding_IncludesPadding()
    {
        // Arrange
        var layout = new TestWrapLayout { Padding = new Thickness(10, 20) };
        layout.Add(new TestView(50, 100));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 300);

        // Assert
        result.Width.Should().Be(70); // 50 + 10 + 10
        result.Height.Should().Be(140); // 100 + 20 + 20
    }

    [Fact(DisplayName = "Measure should skip collapsed children")]
    public void Measure_CollapsedChild_IsSkipped()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(50, 100));
        var collapsedView = new TestView(100, 200) { IsVisible = false };
        layout.Add(collapsedView);
        layout.Add(new TestView(40, 80));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 500);

        // Assert
        // Should only include visible children: 100 + 80 = 180
        result.Width.Should().Be(50); // Max width
        result.Height.Should().Be(180);
    }

    #endregion

    #region Measure Tests - Vertical Spacing

    [Fact(DisplayName = "Measure should include vertical spacing between children")]
    public void Measure_MultipleChildren_IncludesVerticalSpacing()
    {
        // Arrange
        var layout = new TestWrapLayout { VerticalSpacing = 10 };
        layout.Add(new TestView(50, 100));
        layout.Add(new TestView(50, 100));
        layout.Add(new TestView(50, 100));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 500);

        // Assert
        result.Width.Should().Be(50);
        result.Height.Should().Be(320); // 100 + 10 + 100 + 10 + 100
    }

    #endregion

    #region Measure Tests - Wrapping

    [Fact(DisplayName = "Measure should wrap children to next column when height exceeds constraint")]
    public void Measure_ChildrenExceedHeight_WrapsToNextColumn()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(50, 100));
        layout.Add(new TestView(50, 100));
        layout.Add(new TestView(50, 100));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 250); // Only 2 items fit per column

        // Assert
        result.Width.Should().Be(100); // 50 + 50 (two columns)
        result.Height.Should().Be(200); // Max column height: 100 + 100
    }

    [Fact(DisplayName = "Measure should include horizontal spacing between columns")]
    public void Measure_WrappedColumns_IncludesHorizontalSpacing()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 10 };
        layout.Add(new TestView(50, 100));
        layout.Add(new TestView(50, 100));
        layout.Add(new TestView(50, 100));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 250);

        // Assert
        result.Width.Should().Be(110); // 50 + 10 + 50
        result.Height.Should().Be(200);
    }

    [Fact(DisplayName = "Measure should wrap with vertical spacing considered")]
    public void Measure_WrapWithVerticalSpacing_WrapsCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout { VerticalSpacing = 20 };
        layout.Add(new TestView(50, 100));
        layout.Add(new TestView(50, 100));
        layout.Add(new TestView(50, 100));
        var manager = CreateLayoutManager(layout);

        // Act
        // Available: 230, Item 1: 100, Item 2: 100+20=120, Total: 220 (fits)
        // Item 3: 100+20=120, Total would be 340 (doesn't fit)
        var result = manager.Measure(500, 230);

        // Assert
        result.Width.Should().Be(100); // Two columns: 50 + 50
        result.Height.Should().Be(220); // 100 + 20 + 100
    }

    [Fact(DisplayName = "Measure should handle different width children in same column")]
    public void Measure_DifferentWidthChildren_UsesMaxWidth()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(30, 100));
        layout.Add(new TestView(60, 100));
        layout.Add(new TestView(40, 100));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 500);

        // Assert
        result.Width.Should().Be(60); // Max width in column
        result.Height.Should().Be(300);
    }

    [Fact(DisplayName = "Measure should handle different width columns")]
    public void Measure_DifferentWidthColumns_SumsColumnWidths()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 5 };
        layout.Add(new TestView(30, 100)); // Column 1
        layout.Add(new TestView(60, 100)); // Column 1 - max width 60
        layout.Add(new TestView(40, 100)); // Column 2 - width 40
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 250);

        // Assert
        result.Width.Should().Be(105); // 60 + 5 + 40
        result.Height.Should().Be(200);
    }

    #endregion

    #region Measure Tests - Infinite Height

    [Fact(DisplayName = "Measure with infinite height should not wrap")]
    public void Measure_InfiniteHeight_NoWrapping()
    {
        // Arrange
        var layout = new TestWrapLayout { VerticalSpacing = 10 };
        layout.Add(new TestView(50, 100));
        layout.Add(new TestView(60, 200));
        layout.Add(new TestView(40, 150));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, double.PositiveInfinity);

        // Assert
        result.Width.Should().Be(60); // Max width
        result.Height.Should().Be(470); // 100 + 10 + 200 + 10 + 150
    }

    #endregion

    #region ArrangeChildren Tests - Basic

    [Fact(DisplayName = "ArrangeChildren should position single child correctly")]
    public void ArrangeChildren_SingleChild_PositionedCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout();
        var child = new TestView(50, 100);
        layout.Add(child);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert
        child.Frame.X.Should().Be(0);
        child.Frame.Y.Should().Be(0);
        child.Frame.Width.Should().Be(50);
        child.Frame.Height.Should().Be(100);
    }

    [Fact(DisplayName = "ArrangeChildren should respect padding")]
    public void ArrangeChildren_WithPadding_PositionsCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout { Padding = new Thickness(15, 25) };
        var child = new TestView(50, 100);
        layout.Add(child);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert
        child.Frame.X.Should().Be(15);
        child.Frame.Y.Should().Be(25);
    }

    [Fact(DisplayName = "ArrangeChildren should position multiple children in a column")]
    public void ArrangeChildren_MultipleChildren_PositionedInColumn()
    {
        // Arrange
        var layout = new TestWrapLayout { VerticalSpacing = 10 };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 80);
        var child3 = new TestView(50, 60);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 500));

        // Assert
        child1.Frame.Y.Should().Be(0);
        child2.Frame.Y.Should().Be(110); // 100 + 10
        child3.Frame.Y.Should().Be(200); // 110 + 80 + 10
    }

    [Fact(DisplayName = "ArrangeChildren should use column width for all children in column")]
    public void ArrangeChildren_DifferentWidths_UsesColumnWidth()
    {
        // Arrange
        var layout = new TestWrapLayout();
        var child1 = new TestView(30, 100);
        var child2 = new TestView(60, 100);
        var child3 = new TestView(40, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 500));

        // Assert - All children should have the column's max width
        child1.Frame.Width.Should().Be(60);
        child2.Frame.Width.Should().Be(60);
        child3.Frame.Width.Should().Be(60);
    }

    #endregion

    #region ArrangeChildren Tests - Wrapping

    [Fact(DisplayName = "ArrangeChildren should wrap children to next column")]
    public void ArrangeChildren_WrappedChildren_PositionedCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 5, VerticalSpacing = 10 };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        var child3 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 250); // Only 2 items fit per column

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 250));

        // Assert
        child1.Frame.X.Should().Be(0);
        child1.Frame.Y.Should().Be(0);
        child2.Frame.X.Should().Be(0);
        child2.Frame.Y.Should().Be(110); // 100 + 10
        child3.Frame.X.Should().Be(55); // Wrapped to next column (50 + 5)
        child3.Frame.Y.Should().Be(0);
    }

    [Fact(DisplayName = "ArrangeChildren should handle multiple wrapped columns")]
    public void ArrangeChildren_MultipleColumns_PositionedCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 10 };
        var children = new List<TestView>();
        for (var i = 0; i < 6; i++)
        {
            var child = new TestView(50, 100);
            children.Add(child);
            layout.Add(child);
        }
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 250); // 2 items per column

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 250));

        // Assert
        // Column 1: items 0, 1
        children[0].Frame.X.Should().Be(0);
        children[1].Frame.X.Should().Be(0);
        // Column 2: items 2, 3
        children[2].Frame.X.Should().Be(60); // 50 + 10
        children[3].Frame.X.Should().Be(60);
        // Column 3: items 4, 5
        children[4].Frame.X.Should().Be(120); // 60 + 50 + 10
        children[5].Frame.X.Should().Be(120);
    }

    [Fact(DisplayName = "ArrangeChildren should wrap tall items to their own columns")]
    public void ArrangeChildren_TallItems_EachOnOwnColumn()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 10 };
        // Each item is 75% of available height (150 out of 200), so each wraps to its own column
        var child1 = new TestView(40, 150);
        var child2 = new TestView(50, 150);
        var child3 = new TestView(30, 150);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 200);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 200));

        // Assert - Each item should be on its own column
        child1.Frame.X.Should().Be(0);
        child1.Frame.Y.Should().Be(0);
        child1.Frame.Width.Should().Be(40);
        child1.Frame.Height.Should().Be(150);

        child2.Frame.X.Should().Be(50); // 40 + 10 (horizontal spacing)
        child2.Frame.Y.Should().Be(0);
        child2.Frame.Width.Should().Be(50);
        child2.Frame.Height.Should().Be(150);

        child3.Frame.X.Should().Be(110); // 50 + 50 + 10
        child3.Frame.Y.Should().Be(0);
        child3.Frame.Width.Should().Be(30);
        child3.Frame.Height.Should().Be(150);
    }

    [Fact(DisplayName = "Measure should calculate correct size when tall items wrap to their own columns")]
    public void Measure_TallItems_CalculatesCorrectSize()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 10 };
        // Each item is 75% of available height (150 out of 200), so each wraps to its own column
        layout.Add(new TestView(40, 150));
        layout.Add(new TestView(50, 150));
        layout.Add(new TestView(30, 150));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 200);

        // Assert
        result.Width.Should().Be(140); // 40 + 10 + 50 + 10 + 30
        result.Height.Should().Be(150); // Max column height
    }

    [Fact(DisplayName = "ArrangeChildren should expand tall items on their own columns")]
    public void ArrangeChildren_TallItemsWithExpand_ExpandsToFullHeight()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            HorizontalSpacing = 10,
            ExpandMode = WrapLayoutExpandMode.Distribute
        };
        var child1 = new TestView(40, 150);
        var child2 = new TestView(50, 150);
        var child3 = new TestView(30, 150);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 200);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 200));

        // Assert - Each item should expand to full height since they're alone on their column
        child1.Frame.Height.Should().Be(200);
        child2.Frame.Height.Should().Be(200);
        child3.Frame.Height.Should().Be(200);
    }

    #endregion

    #region ArrangeChildren Tests - Expand Mode: Distribute

    [Fact(DisplayName = "ArrangeChildren Distribute mode should distribute space by expand ratio")]
    public void ArrangeChildren_DistributeMode_DistributesSpace()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Distribute,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        // Available: 300, Used: 200, Remaining: 100
        // Each gets 50 extra (equal ratio)
        manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert
        child1.Frame.Height.Should().Be(150); // 100 + 50
        child2.Frame.Height.Should().Be(150); // 100 + 50
        child2.Frame.Y.Should().Be(150);
    }

    [Fact(DisplayName = "ArrangeChildren Distribute mode should respect unequal ratios")]
    public void ArrangeChildren_DistributeMode_RespectsUnequalRatios()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Distribute,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        // Available: 300, Used: 200, Remaining: 100
        // Ratio total: 4, child1 gets 25, child2 gets 75
        manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert
        child1.Frame.Height.Should().Be(125); // 100 + 25
        child2.Frame.Height.Should().Be(175); // 100 + 75
    }

    [Fact(DisplayName = "ArrangeChildren Distribute mode should skip items with zero expand ratio")]
    public void ArrangeChildren_DistributeMode_SkipsZeroRatio()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Distribute,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 0); // No expand
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert
        child1.Frame.Height.Should().Be(100); // No extra
        child2.Frame.Height.Should().Be(200); // Gets all 100 extra
    }

    #endregion

    #region ArrangeChildren Tests - Expand Mode: DistributeProportionally

    [Fact(DisplayName = "ArrangeChildren DistributeProportionally mode should distribute based on size and ratio")]
    public void ArrangeChildren_DistributeProportionally_UsesWeightedDistribution()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.DistributeProportionally,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 50);   // Smaller
        var child2 = new TestView(50, 150);  // Larger
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        // Available: 300, Used: 200, Remaining: 100
        // Weights: child1 = 50*1=50, child2 = 150*1=150, total=200
        // child1 gets 100 * (50/200) = 25
        // child2 gets 100 * (150/200) = 75
        manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert
        child1.Frame.Height.Should().Be(75);  // 50 + 25
        child2.Frame.Height.Should().Be(225); // 150 + 75
    }

    [Fact(DisplayName = "ArrangeChildren DistributeProportionally mode with different ratios")]
    public void ArrangeChildren_DistributeProportionally_WithDifferentRatios()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.DistributeProportionally,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 400);

        // Act
        // Available: 400, Used: 200, Remaining: 200
        // Weights: child1 = 100*1=100, child2 = 100*2=200, total=300
        // child1 gets 200 * (100/300) = 66.67
        // child2 gets 200 * (200/300) = 133.33
        manager.ArrangeChildren(new Rect(0, 0, 500, 400));

        // Assert
        child1.Frame.Height.Should().BeApproximately(166.67, 0.01);
        child2.Frame.Height.Should().BeApproximately(233.33, 0.01);
    }

    #endregion

    #region ArrangeChildren Tests - Expand Mode: Divide

    [Fact(DisplayName = "ArrangeChildren Divide mode should divide space based on ratio")]
    public void ArrangeChildren_DivideMode_DividesSpaceByRatio()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert
        child1.Frame.Height.Should().Be(150);
        child2.Frame.Height.Should().Be(150);
    }

    [Fact(DisplayName = "ArrangeChildren Divide mode should work with only one expanding item")]
    public void ArrangeChildren_DivideMode_SingleExpandingItem()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            VerticalSpacing = 10
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 0);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        // Available: 300, Used with spacing: 100 + 10 + 50 = 160, Remaining: 140
        manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert
        child1.Frame.Height.Should().Be(100);
        child2.Frame.Height.Should().Be(190); // 50 + 140
    }

    [Fact(DisplayName = "ArrangeChildren Divide mode should not shrink items below desired size")]
    public void ArrangeChildren_DivideMode_DoesNotShrinkItems()
    {
        // Arrange: Two items with desired heights 60 and 100, equal expand ratio
        // Available space 180 for both. Equal split would be 90 each, but 100 should not shrink.
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 60);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 180);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 180));

        // Assert: child2 keeps 100 (no shrink), child1 gets remaining 80
        child1.Frame.Height.Should().Be(80);
        child2.Frame.Height.Should().Be(100);
    }

    [Fact(DisplayName = "ArrangeChildren Divide mode with multiple items some would shrink")]
    public void ArrangeChildren_DivideMode_MultipleItemsSomeWouldShrink()
    {
        // Arrange: Three items with different sizes and ratios
        // Item1: 40, ratio 1 | Item2: 100, ratio 1 | Item3: 60, ratio 1
        // Available: 220, spacing: 10 each = 20 total
        // Space for expanding: 220 - 20 = 200
        // Equal split: 200/3 ≈ 66.67 each
        // Item2 (100) would shrink → lock at 100
        // Remaining: 200 - 100 = 100 for Item1 and Item3
        // Equal split: 50 each
        // Item3 (60) would shrink → lock at 60
        // Remaining: 100 - 60 = 40 for Item1
        // Item1 (40) would shrink → lock at 40
        // All items locked at desired sizes
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            VerticalSpacing = 10
        };
        var child1 = new TestView(50, 40);
        var child2 = new TestView(50, 100);
        var child3 = new TestView(50, 60);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 220);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 220));

        // Assert: All items keep their desired sizes (no shrinking)
        child1.Frame.Height.Should().Be(40);
        child2.Frame.Height.Should().Be(100);
        child3.Frame.Height.Should().Be(60);
    }

    [Fact(DisplayName = "ArrangeChildren Divide mode with enough space expands all items")]
    public void ArrangeChildren_DivideMode_WithEnoughSpaceExpandsAll()
    {
        // Arrange: Two items with sizes 60 and 100, equal ratio
        // Available: 300, spacing: 0
        // Space for expanding: 300
        // Equal split: 150 each - both can grow
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 60);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert: Both items get equal share
        child1.Frame.Height.Should().Be(150);
        child2.Frame.Height.Should().Be(150);
    }

    [Fact(DisplayName = "ArrangeChildren Divide mode respects different ratios when no shrink needed")]
    public void ArrangeChildren_DivideMode_RespectsRatiosNoShrink()
    {
        // Arrange: Two items, ratio 1:2, enough space
        // Available: 300, Item1 height 50, Item2 height 50
        // Item1 gets 100, Item2 gets 200
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 50);
        var child2 = new TestView(50, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert
        child1.Frame.Height.Should().Be(100);
        child2.Frame.Height.Should().Be(200);
    }

    [Fact(DisplayName = "ArrangeChildren Divide mode with 4 children mixed expanding and non-expanding")]
    public void ArrangeChildren_DivideMode_FourChildrenMixed()
    {
        // Arrange: 4 children - 2 non-expanding (fixed), 2 expanding with different ratios
        // Child1: 80 (no expand) | Child2: 40 (ratio 1) | Child3: 60 (no expand) | Child4: 100 (ratio 2)
        // Available: 500, spacing: 10 each = 30 total
        // Non-expanding total: 80 + 60 = 140
        // Space for expanding: 500 - 140 - 30 = 330
        // Ratio split: Child2 gets 330 * 1/3 = 110, Child4 gets 330 * 2/3 = 220
        // Child2 (40) → 110 ✓ (grows)
        // Child4 (100) → 220 ✓ (grows)
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            VerticalSpacing = 10
        };
        var child1 = new TestView(50, 80);
        var child2 = new TestView(50, 40);
        var child3 = new TestView(50, 60);
        var child4 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.Add(child4);
        layout.SetExpandRatio(child1, 0);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 0);
        layout.SetExpandRatio(child4, 2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 500));

        // Assert
        child1.Frame.Height.Should().Be(80);   // Non-expanding, keeps desired
        child2.Frame.Height.Should().Be(110);  // 330 * 1/3
        child3.Frame.Height.Should().Be(60);   // Non-expanding, keeps desired
        child4.Frame.Height.Should().Be(220);  // 330 * 2/3
        
        // Verify positions
        child1.Frame.Y.Should().Be(0);
        child2.Frame.Y.Should().Be(90);   // 80 + 10
        child3.Frame.Y.Should().Be(210);  // 90 + 110 + 10
        child4.Frame.Y.Should().Be(280);  // 210 + 60 + 10
    }

    [Fact(DisplayName = "ArrangeChildren Divide mode with 4 children some would shrink")]
    public void ArrangeChildren_DivideMode_FourChildrenSomeWouldShrink()
    {
        // Arrange: 4 expanding children with different sizes
        // Child1: 30 (ratio 1) | Child2: 120 (ratio 1) | Child3: 50 (ratio 1) | Child4: 80 (ratio 1)
        // Available: 320, spacing: 10 each = 30 total
        // Space for expanding: 320 - 30 = 290
        // Equal split: 290 / 4 = 72.5 each
        // Child2 (120) would shrink → lock at 120
        // Remaining: 290 - 120 = 170 for 3 items
        // Equal split: 170 / 3 ≈ 56.67 each
        // Child4 (80) would shrink → lock at 80
        // Remaining: 170 - 80 = 90 for 2 items
        // Equal split: 90 / 2 = 45 each
        // Child3 (50) would shrink → lock at 50
        // Remaining: 90 - 50 = 40 for Child1
        // Child1 (30) gets 40 ✓ (grows)
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            VerticalSpacing = 10
        };
        var child1 = new TestView(50, 30);
        var child2 = new TestView(50, 120);
        var child3 = new TestView(50, 50);
        var child4 = new TestView(50, 80);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.Add(child4);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 1);
        layout.SetExpandRatio(child4, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 320);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 320));

        // Assert
        child1.Frame.Height.Should().Be(40);   // Gets remaining space
        child2.Frame.Height.Should().Be(120);  // Locked at desired (would shrink)
        child3.Frame.Height.Should().Be(50);   // Locked at desired (would shrink)
        child4.Frame.Height.Should().Be(80);   // Locked at desired (would shrink)
    }

    #endregion

    #region ArrangeChildren Tests - Expand Per Column

    [Fact(DisplayName = "ArrangeChildren should apply expand within each column independently")]
    public void ArrangeChildren_Expand_AppliesPerColumn()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Distribute,
            HorizontalSpacing = 0,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 80);  // Column 1
        var child2 = new TestView(50, 80);  // Column 1
        var child3 = new TestView(50, 100); // Column 2 (wrapped)
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 200);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 200));

        // Assert
        // Column 1: 200 - 160 = 40 remaining, each gets 20
        child1.Frame.Height.Should().Be(100);
        child2.Frame.Height.Should().Be(100);
        // Column 2: 200 - 100 = 100 remaining, child3 gets all
        child3.Frame.Height.Should().Be(200);
    }

    #endregion

    #region ArrangeChildren Tests - Collapsed Items

    [Fact(DisplayName = "ArrangeChildren should skip collapsed items")]
    public void ArrangeChildren_CollapsedItems_AreSkipped()
    {
        // Arrange
        var layout = new TestWrapLayout { VerticalSpacing = 10 };
        var child1 = new TestView(50, 100);
        var collapsedChild = new TestView(50, 100) { IsVisible = false };
        var child3 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(collapsedChild);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 500));

        // Assert
        child1.Frame.Y.Should().Be(0);
        child3.Frame.Y.Should().Be(110); // 100 + 10, no space for collapsed
    }

    #endregion

    #region ArrangeChildren Tests - Bounds Offset

    [Fact(DisplayName = "ArrangeChildren should respect bounds offset")]
    public void ArrangeChildren_WithBoundsOffset_PositionsCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout();
        var child = new TestView(50, 100);
        layout.Add(child);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        manager.ArrangeChildren(new Rect(20, 30, 500, 300));

        // Assert
        child.Frame.X.Should().Be(20);
        child.Frame.Y.Should().Be(30);
    }

    #endregion

    #region Edge Cases

    [Fact(DisplayName = "Measure should handle single item taller than constraint")]
    public void Measure_SingleItemTallerThanConstraint_ReturnsConstrainedSize()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(50, 500));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 300);

        // Assert
        // Item height gets constrained by available height during measurement
        result.Width.Should().Be(50);
        result.Height.Should().Be(300);
    }

    [Fact(DisplayName = "ArrangeChildren should handle no expandable items")]
    public void ArrangeChildren_NoExpandableItems_KeepsOriginalSize()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Distribute,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        // No expand ratios set (default 0)
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 400);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 400));

        // Assert
        child1.Frame.Height.Should().Be(100);
        child2.Frame.Height.Should().Be(100);
    }

    [Fact(DisplayName = "Measure should handle all collapsed children")]
    public void Measure_AllCollapsed_ReturnsZero()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(50, 100) { IsVisible = false });
        layout.Add(new TestView(50, 100) { IsVisible = false });
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 300);

        // Assert
        result.Width.Should().Be(0);
        result.Height.Should().Be(0);
    }

    [Fact(DisplayName = "ArrangeChildren returns bounds size")]
    public void ArrangeChildren_ReturnsSpecifiedBoundsSize()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(50, 100));
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 300);

        // Act
        var result = manager.ArrangeChildren(new Rect(0, 0, 500, 300));

        // Assert
        result.Width.Should().Be(500);
        result.Height.Should().Be(300);
    }

    #endregion

    #region Complex Scenarios

    [Fact(DisplayName = "Complex layout with padding, spacing, wrapping and expand")]
    public void ComplexLayout_WithAllFeatures()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            Padding = new Thickness(10),
            HorizontalSpacing = 10,
            VerticalSpacing = 5,
            ExpandMode = WrapLayoutExpandMode.Distribute
        };
        var child1 = new TestView(40, 80);
        var child2 = new TestView(50, 80);
        var child3 = new TestView(30, 80);
        var child4 = new TestView(45, 80);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.Add(child4);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 1);
        layout.SetExpandRatio(child4, 1);
        var manager = CreateLayoutManager(layout);

        // Available height: 200 - 20 (padding) = 180
        // Column 1: 80 + 5 + 80 = 165 (fits)
        // Column 2: 80 + 5 + 80 = 165 (fits)
        manager.Measure(500, 200);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 200));

        // Assert positions
        child1.Frame.X.Should().Be(10); // Left padding
        child1.Frame.Y.Should().Be(10); // Top padding
        child2.Frame.X.Should().Be(10); // Same column

        child3.Frame.X.Should().Be(70); // 10 + 50 + 10 (column width + horizontal spacing)
        child4.Frame.X.Should().Be(70);

        // Column widths
        child1.Frame.Width.Should().Be(50); // Max width in column 1
        child2.Frame.Width.Should().Be(50);
        child3.Frame.Width.Should().Be(45); // Max width in column 2
        child4.Frame.Width.Should().Be(45);

        // Expand: remaining = 180 - 165 = 15, split = 7.5 each
        child1.Frame.Height.Should().Be(87.5);
        child2.Frame.Height.Should().Be(87.5);
    }

    #endregion

    #region ArrangeChildren Tests - Items Alignment

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment.Start should position items at start")]
    public void ArrangeChildren_ItemsAlignmentStart_PositionsAtStart()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.Start,
            VerticalSpacing = 10
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 400);

        // Act
        // Available: 400, Used: 100 + 10 + 100 = 210, Remaining: 190
        manager.ArrangeChildren(new Rect(0, 0, 500, 400));

        // Assert - items should start at Y=0
        child1.Frame.Y.Should().Be(0);
        child2.Frame.Y.Should().Be(110);
    }

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment.Center should center items")]
    public void ArrangeChildren_ItemsAlignmentCenter_CentersItems()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.Center,
            VerticalSpacing = 10
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 400);

        // Act
        // Available: 400, Used: 100 + 10 + 100 = 210, Remaining: 190, Offset: 95
        manager.ArrangeChildren(new Rect(0, 0, 500, 400));

        // Assert - items should be centered
        child1.Frame.Y.Should().Be(95);
        child2.Frame.Y.Should().Be(205);
    }

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment.End should position items at end")]
    public void ArrangeChildren_ItemsAlignmentEnd_PositionsAtEnd()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.End,
            VerticalSpacing = 10
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 400);

        // Act
        // Available: 400, Used: 100 + 10 + 100 = 210, Remaining: 190, Offset: 190
        manager.ArrangeChildren(new Rect(0, 0, 500, 400));

        // Assert - items should be at end
        child1.Frame.Y.Should().Be(190);
        child2.Frame.Y.Should().Be(300);
    }

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment should not affect columns with expand")]
    public void ArrangeChildren_ItemsAlignmentWithExpand_ExpandTakesPrecedence()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.End,
            ExpandMode = WrapLayoutExpandMode.Distribute,
            VerticalSpacing = 0
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 400);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 400));

        // Assert - expand takes all space, so alignment has no effect
        child1.Frame.Y.Should().Be(0);
        child1.Frame.Height.Should().Be(200);
        child2.Frame.Y.Should().Be(200);
        child2.Frame.Height.Should().Be(200);
    }

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment.Center and padding")]
    public void ArrangeChildren_ItemsAlignmentCenterWithPadding_CentersCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.Center,
            Padding = new Thickness(20),
            VerticalSpacing = 10
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        layout.Add(child1);
        layout.Add(child2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 400);

        // Act
        // Available: 400 - 40 = 360, Used: 210, Remaining: 150, Offset: 75
        manager.ArrangeChildren(new Rect(0, 0, 500, 400));

        // Assert - items should be centered within padding
        child1.Frame.Y.Should().Be(95); // 20 (padding) + 75 (offset)
        child2.Frame.Y.Should().Be(205);
    }

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment should apply per column")]
    public void ArrangeChildren_ItemsAlignmentCenter_AppliesPerColumn()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.Center,
            HorizontalSpacing = 10,
            VerticalSpacing = 10
        };
        var child1 = new TestView(50, 100);
        var child2 = new TestView(50, 100);
        var child3 = new TestView(50, 50); // Smaller item in column 2
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        // Height constraint causes wrap after child2
        manager.Measure(500, 250);

        // Act
        // Column 1: Available: 250, Used: 210, Remaining: 40, Offset: 20
        // Column 2: Available: 250, Used: 50, Remaining: 200, Offset: 100
        manager.ArrangeChildren(new Rect(0, 0, 500, 250));

        // Assert
        child1.Frame.Y.Should().Be(20);
        child2.Frame.Y.Should().Be(130);
        child3.Frame.Y.Should().Be(100);
    }

    #endregion
}

