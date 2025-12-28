using Microsoft.Maui.Layouts;

namespace Nalu.Maui.Test.Layouts;

public class HorizontalWrapLayoutManagerTests
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

        protected override ILayoutManager CreateLayoutManager() => new HorizontalWrapLayoutManager(this);
    }

    private static HorizontalWrapLayoutManager CreateLayoutManager(TestWrapLayout layout) => new HorizontalWrapLayoutManager(layout);

    #region Measure Tests - Basic Scenarios

    [Fact(DisplayName = "Measure should return zero size for empty layout")]
    public void Measure_EmptyLayout_ReturnsZeroSize()
    {
        // Arrange
        var layout = new TestWrapLayout();
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(300, 500);

        // Assert
        result.Width.Should().Be(0);
        result.Height.Should().Be(0);
    }

    [Fact(DisplayName = "Measure should return child size for single child")]
    public void Measure_SingleChild_ReturnsChildSize()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(100, 50));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(300, 500);

        // Assert
        result.Width.Should().Be(100);
        result.Height.Should().Be(50);
    }

    [Fact(DisplayName = "Measure should include padding")]
    public void Measure_WithPadding_IncludesPadding()
    {
        // Arrange
        var layout = new TestWrapLayout { Padding = new Thickness(10, 20) };
        layout.Add(new TestView(100, 50));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(300, 500);

        // Assert
        result.Width.Should().Be(120); // 100 + 10 + 10
        result.Height.Should().Be(90); // 50 + 20 + 20
    }

    [Fact(DisplayName = "Measure should skip collapsed children")]
    public void Measure_CollapsedChild_IsSkipped()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(100, 50));
        var collapsedView = new TestView(200, 100) { IsVisible = false };
        layout.Add(collapsedView);
        layout.Add(new TestView(80, 40));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 500);

        // Assert
        // Should only include visible children: 100 + 80 = 180
        result.Width.Should().Be(180);
        result.Height.Should().Be(50); // Max height
    }

    #endregion

    #region Measure Tests - Horizontal Spacing

    [Fact(DisplayName = "Measure should include horizontal spacing between children")]
    public void Measure_MultipleChildren_IncludesHorizontalSpacing()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 10 };
        layout.Add(new TestView(100, 50));
        layout.Add(new TestView(100, 50));
        layout.Add(new TestView(100, 50));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 500);

        // Assert
        result.Width.Should().Be(320); // 100 + 10 + 100 + 10 + 100
        result.Height.Should().Be(50);
    }

    #endregion

    #region Measure Tests - Wrapping

    [Fact(DisplayName = "Measure should wrap children to next row when width exceeds constraint")]
    public void Measure_ChildrenExceedWidth_WrapsToNextRow()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(100, 50));
        layout.Add(new TestView(100, 50));
        layout.Add(new TestView(100, 50));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(250, 500); // Only 2 items fit per row

        // Assert
        result.Width.Should().Be(200); // Max row width: 100 + 100
        result.Height.Should().Be(100); // 50 + 50 (two rows)
    }

    [Fact(DisplayName = "Measure should include vertical spacing between rows")]
    public void Measure_WrappedRows_IncludesVerticalSpacing()
    {
        // Arrange
        var layout = new TestWrapLayout { VerticalSpacing = 10 };
        layout.Add(new TestView(100, 50));
        layout.Add(new TestView(100, 50));
        layout.Add(new TestView(100, 50));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(250, 500);

        // Assert
        result.Width.Should().Be(200);
        result.Height.Should().Be(110); // 50 + 10 + 50
    }

    [Fact(DisplayName = "Measure should wrap with horizontal spacing considered")]
    public void Measure_WrapWithHorizontalSpacing_WrapsCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 20 };
        layout.Add(new TestView(100, 50));
        layout.Add(new TestView(100, 50));
        layout.Add(new TestView(100, 50));
        var manager = CreateLayoutManager(layout);

        // Act
        // Available: 230, Item 1: 100, Item 2: 100+20=120, Total: 220 (fits)
        // Item 3: 100+20=120, Total would be 340 (doesn't fit)
        var result = manager.Measure(230, 500);

        // Assert
        result.Width.Should().Be(220); // 100 + 20 + 100
        result.Height.Should().Be(100); // Two rows: 50 + 50
    }

    [Fact(DisplayName = "Measure should handle different height children in same row")]
    public void Measure_DifferentHeightChildren_UsesMaxHeight()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(100, 30));
        layout.Add(new TestView(100, 60));
        layout.Add(new TestView(100, 40));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(500, 500);

        // Assert
        result.Width.Should().Be(300);
        result.Height.Should().Be(60); // Max height in row
    }

    [Fact(DisplayName = "Measure should handle different height rows")]
    public void Measure_DifferentHeightRows_SumsRowHeights()
    {
        // Arrange
        var layout = new TestWrapLayout { VerticalSpacing = 5 };
        layout.Add(new TestView(100, 30)); // Row 1
        layout.Add(new TestView(100, 60)); // Row 1 - max height 60
        layout.Add(new TestView(100, 40)); // Row 2 - height 40
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(250, 500);

        // Assert
        result.Width.Should().Be(200);
        result.Height.Should().Be(105); // 60 + 5 + 40
    }

    #endregion

    #region Measure Tests - Infinite Width

    [Fact(DisplayName = "Measure with infinite width should not wrap")]
    public void Measure_InfiniteWidth_NoWrapping()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 10 };
        layout.Add(new TestView(100, 50));
        layout.Add(new TestView(200, 60));
        layout.Add(new TestView(150, 40));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(double.PositiveInfinity, 500);

        // Assert
        result.Width.Should().Be(470); // 100 + 10 + 200 + 10 + 150
        result.Height.Should().Be(60); // Max height
    }

    #endregion

    #region ArrangeChildren Tests - Basic

    [Fact(DisplayName = "ArrangeChildren should position single child correctly")]
    public void ArrangeChildren_SingleChild_PositionedCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout();
        var child = new TestView(100, 50);
        layout.Add(child);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert
        child.Frame.X.Should().Be(0);
        child.Frame.Y.Should().Be(0);
        child.Frame.Width.Should().Be(100);
        child.Frame.Height.Should().Be(50);
    }

    [Fact(DisplayName = "ArrangeChildren should respect padding")]
    public void ArrangeChildren_WithPadding_PositionsCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout { Padding = new Thickness(15, 25) };
        var child = new TestView(100, 50);
        layout.Add(child);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert
        child.Frame.X.Should().Be(15);
        child.Frame.Y.Should().Be(25);
    }

    [Fact(DisplayName = "ArrangeChildren should position multiple children in a row")]
    public void ArrangeChildren_MultipleChildren_PositionedInRow()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 10 };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(80, 50);
        var child3 = new TestView(60, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 500));

        // Assert
        child1.Frame.X.Should().Be(0);
        child2.Frame.X.Should().Be(110); // 100 + 10
        child3.Frame.X.Should().Be(200); // 110 + 80 + 10
    }

    [Fact(DisplayName = "ArrangeChildren should use row height for all children in row")]
    public void ArrangeChildren_DifferentHeights_UsesRowHeight()
    {
        // Arrange
        var layout = new TestWrapLayout();
        var child1 = new TestView(100, 30);
        var child2 = new TestView(100, 60);
        var child3 = new TestView(100, 40);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 500));

        // Assert - All children should have the row's max height
        child1.Frame.Height.Should().Be(60);
        child2.Frame.Height.Should().Be(60);
        child3.Frame.Height.Should().Be(60);
    }

    #endregion

    #region ArrangeChildren Tests - Wrapping

    [Fact(DisplayName = "ArrangeChildren should wrap children to next row")]
    public void ArrangeChildren_WrappedChildren_PositionedCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 10, VerticalSpacing = 5 };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        var child3 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(250, 500); // Only 2 items fit per row

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 250, 500));

        // Assert
        child1.Frame.X.Should().Be(0);
        child1.Frame.Y.Should().Be(0);
        child2.Frame.X.Should().Be(110); // 100 + 10
        child2.Frame.Y.Should().Be(0);
        child3.Frame.X.Should().Be(0); // Wrapped to next row
        child3.Frame.Y.Should().Be(55); // 50 + 5
    }

    [Fact(DisplayName = "ArrangeChildren should handle multiple wrapped rows")]
    public void ArrangeChildren_MultipleRows_PositionedCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout { VerticalSpacing = 10 };
        var children = new List<TestView>();
        for (var i = 0; i < 6; i++)
        {
            var child = new TestView(100, 50);
            children.Add(child);
            layout.Add(child);
        }
        var manager = CreateLayoutManager(layout);
        manager.Measure(250, 500); // 2 items per row

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 250, 500));

        // Assert
        // Row 1: items 0, 1
        children[0].Frame.Y.Should().Be(0);
        children[1].Frame.Y.Should().Be(0);
        // Row 2: items 2, 3
        children[2].Frame.Y.Should().Be(60); // 50 + 10
        children[3].Frame.Y.Should().Be(60);
        // Row 3: items 4, 5
        children[4].Frame.Y.Should().Be(120); // 60 + 50 + 10
        children[5].Frame.Y.Should().Be(120);
    }

    [Fact(DisplayName = "ArrangeChildren should wrap wide items to their own rows")]
    public void ArrangeChildren_WideItems_EachOnOwnRow()
    {
        // Arrange
        var layout = new TestWrapLayout { VerticalSpacing = 10 };
        // Each item is 75% of available width (150 out of 200), so each wraps to its own row
        var child1 = new TestView(150, 40);
        var child2 = new TestView(150, 50);
        var child3 = new TestView(150, 30);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(200, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 200, 500));

        // Assert - Each item should be on its own row
        child1.Frame.X.Should().Be(0);
        child1.Frame.Y.Should().Be(0);
        child1.Frame.Width.Should().Be(150);
        child1.Frame.Height.Should().Be(40);

        child2.Frame.X.Should().Be(0);
        child2.Frame.Y.Should().Be(50); // 40 + 10 (vertical spacing)
        child2.Frame.Width.Should().Be(150);
        child2.Frame.Height.Should().Be(50);

        child3.Frame.X.Should().Be(0);
        child3.Frame.Y.Should().Be(110); // 50 + 50 + 10
        child3.Frame.Width.Should().Be(150);
        child3.Frame.Height.Should().Be(30);
    }

    [Fact(DisplayName = "Measure should calculate correct size when wide items wrap to their own rows")]
    public void Measure_WideItems_CalculatesCorrectSize()
    {
        // Arrange
        var layout = new TestWrapLayout { VerticalSpacing = 10 };
        // Each item is 75% of available width (150 out of 200), so each wraps to its own row
        layout.Add(new TestView(150, 40));
        layout.Add(new TestView(150, 50));
        layout.Add(new TestView(150, 30));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(200, 500);

        // Assert
        result.Width.Should().Be(150); // Max row width
        result.Height.Should().Be(140); // 40 + 10 + 50 + 10 + 30
    }

    [Fact(DisplayName = "ArrangeChildren should expand wide items on their own rows")]
    public void ArrangeChildren_WideItemsWithExpand_ExpandsToFullWidth()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            VerticalSpacing = 10,
            ExpandMode = WrapLayoutExpandMode.Distribute
        };
        var child1 = new TestView(150, 40);
        var child2 = new TestView(150, 50);
        var child3 = new TestView(150, 30);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(200, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 200, 500));

        // Assert - Each item should expand to full width since they're alone on their row
        child1.Frame.Width.Should().Be(200);
        child2.Frame.Width.Should().Be(200);
        child3.Frame.Width.Should().Be(200);
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
            HorizontalSpacing = 0
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        // Available: 300, Used: 200, Remaining: 100
        // Each gets 50 extra (equal ratio)
        manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert
        child1.Frame.Width.Should().Be(150); // 100 + 50
        child2.Frame.Width.Should().Be(150); // 100 + 50
        child2.Frame.X.Should().Be(150);
    }

    [Fact(DisplayName = "ArrangeChildren Distribute mode should respect unequal ratios")]
    public void ArrangeChildren_DistributeMode_RespectsUnequalRatios()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Distribute,
            HorizontalSpacing = 0
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        // Available: 300, Used: 200, Remaining: 100
        // Ratio total: 4, child1 gets 25, child2 gets 75
        manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert
        child1.Frame.Width.Should().Be(125); // 100 + 25
        child2.Frame.Width.Should().Be(175); // 100 + 75
    }

    [Fact(DisplayName = "ArrangeChildren Distribute mode should skip items with zero expand ratio")]
    public void ArrangeChildren_DistributeMode_SkipsZeroRatio()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Distribute,
            HorizontalSpacing = 0
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 0); // No expand
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert
        child1.Frame.Width.Should().Be(100); // No extra
        child2.Frame.Width.Should().Be(200); // Gets all 100 extra
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
            HorizontalSpacing = 0
        };
        var child1 = new TestView(50, 50);  // Smaller
        var child2 = new TestView(150, 50); // Larger
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        // Available: 300, Used: 200, Remaining: 100
        // Weights: child1 = 50*1=50, child2 = 150*1=150, total=200
        // child1 gets 100 * (50/200) = 25
        // child2 gets 100 * (150/200) = 75
        manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert
        child1.Frame.Width.Should().Be(75);  // 50 + 25
        child2.Frame.Width.Should().Be(225); // 150 + 75
    }

    [Fact(DisplayName = "ArrangeChildren DistributeProportionally mode with different ratios")]
    public void ArrangeChildren_DistributeProportionally_WithDifferentRatios()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.DistributeProportionally,
            HorizontalSpacing = 0
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(400, 500);

        // Act
        // Available: 400, Used: 200, Remaining: 200
        // Weights: child1 = 100*1=100, child2 = 100*2=200, total=300
        // child1 gets 200 * (100/300) = 66.67
        // child2 gets 200 * (200/300) = 133.33
        manager.ArrangeChildren(new Rect(0, 0, 400, 500));

        // Assert
        child1.Frame.Width.Should().BeApproximately(166.67, 0.01);
        child2.Frame.Width.Should().BeApproximately(233.33, 0.01);
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
            HorizontalSpacing = 0
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert
        child1.Frame.Width.Should().Be(150);
        child2.Frame.Width.Should().Be(150);
    }

    [Fact(DisplayName = "ArrangeChildren Divide mode should work with only one expanding item")]
    public void ArrangeChildren_DivideMode_SingleExpandingItem()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            HorizontalSpacing = 10
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(50, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 0);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        // Available: 300, Used with spacing: 100 + 10 + 50 = 160, Remaining: 140
        manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert
        child1.Frame.Width.Should().Be(100);
        child2.Frame.Width.Should().Be(190); // 50 + 140
    }

    [Fact(DisplayName = "ArrangeChildren Divide mode should not shrink items below desired size")]
    public void ArrangeChildren_DivideMode_DoesNotShrinkItems()
    {
        // Arrange: Two items with desired sizes 60 and 100, equal expand ratio
        // Available space 180 for both. Equal split would be 90 each, but 100 should not shrink.
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            HorizontalSpacing = 0
        };
        var child1 = new TestView(60, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(180, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 180, 500));

        // Assert: child2 keeps 100 (no shrink), child1 gets remaining 80
        child1.Frame.Width.Should().Be(80);
        child2.Frame.Width.Should().Be(100);
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
            HorizontalSpacing = 10
        };
        var child1 = new TestView(40, 50);
        var child2 = new TestView(100, 50);
        var child3 = new TestView(60, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(220, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 220, 500));

        // Assert: All items keep their desired sizes (no shrinking)
        child1.Frame.Width.Should().Be(40);
        child2.Frame.Width.Should().Be(100);
        child3.Frame.Width.Should().Be(60);
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
            HorizontalSpacing = 0
        };
        var child1 = new TestView(60, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert: Both items get equal share
        child1.Frame.Width.Should().Be(150);
        child2.Frame.Width.Should().Be(150);
    }

    [Fact(DisplayName = "ArrangeChildren Divide mode respects different ratios when no shrink needed")]
    public void ArrangeChildren_DivideMode_RespectsRatiosNoShrink()
    {
        // Arrange: Two items, ratio 1:2, enough space
        // Available: 300, Item1 size 50, Item2 size 50
        // Item1 gets 100, Item2 gets 200
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Divide,
            HorizontalSpacing = 0
        };
        var child1 = new TestView(50, 50);
        var child2 = new TestView(50, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert
        child1.Frame.Width.Should().Be(100);
        child2.Frame.Width.Should().Be(200);
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
            HorizontalSpacing = 10
        };
        var child1 = new TestView(80, 50);
        var child2 = new TestView(40, 50);
        var child3 = new TestView(60, 50);
        var child4 = new TestView(100, 50);
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
        child1.Frame.Width.Should().Be(80);   // Non-expanding, keeps desired
        child2.Frame.Width.Should().Be(110);  // 330 * 1/3
        child3.Frame.Width.Should().Be(60);   // Non-expanding, keeps desired
        child4.Frame.Width.Should().Be(220);  // 330 * 2/3
        
        // Verify positions
        child1.Frame.X.Should().Be(0);
        child2.Frame.X.Should().Be(90);   // 80 + 10
        child3.Frame.X.Should().Be(210);  // 90 + 110 + 10
        child4.Frame.X.Should().Be(280);  // 210 + 60 + 10
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
            HorizontalSpacing = 10
        };
        var child1 = new TestView(30, 50);
        var child2 = new TestView(120, 50);
        var child3 = new TestView(50, 50);
        var child4 = new TestView(80, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.Add(child4);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 1);
        layout.SetExpandRatio(child4, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(320, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 320, 500));

        // Assert
        child1.Frame.Width.Should().Be(40);   // Gets remaining space
        child2.Frame.Width.Should().Be(120);  // Locked at desired (would shrink)
        child3.Frame.Width.Should().Be(50);   // Locked at desired (would shrink)
        child4.Frame.Width.Should().Be(80);   // Locked at desired (would shrink)
    }

    #endregion

    #region ArrangeChildren Tests - Expand Per Row

    [Fact(DisplayName = "ArrangeChildren should apply expand within each row independently")]
    public void ArrangeChildren_Expand_AppliesPerRow()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Distribute,
            HorizontalSpacing = 0,
            VerticalSpacing = 0
        };
        var child1 = new TestView(80, 50);  // Row 1
        var child2 = new TestView(80, 50);  // Row 1
        var child3 = new TestView(100, 50); // Row 2 (wrapped)
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(200, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 200, 500));

        // Assert
        // Row 1: 200 - 160 = 40 remaining, each gets 20
        child1.Frame.Width.Should().Be(100);
        child2.Frame.Width.Should().Be(100);
        // Row 2: 200 - 100 = 100 remaining, child3 gets all
        child3.Frame.Width.Should().Be(200);
    }

    #endregion

    #region ArrangeChildren Tests - Collapsed Items

    [Fact(DisplayName = "ArrangeChildren should skip collapsed items")]
    public void ArrangeChildren_CollapsedItems_AreSkipped()
    {
        // Arrange
        var layout = new TestWrapLayout { HorizontalSpacing = 10 };
        var child1 = new TestView(100, 50);
        var collapsedChild = new TestView(100, 50) { IsVisible = false };
        var child3 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(collapsedChild);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        manager.Measure(500, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 500, 500));

        // Assert
        child1.Frame.X.Should().Be(0);
        child3.Frame.X.Should().Be(110); // 100 + 10, no space for collapsed
    }

    #endregion

    #region ArrangeChildren Tests - Bounds Offset

    [Fact(DisplayName = "ArrangeChildren should respect bounds offset")]
    public void ArrangeChildren_WithBoundsOffset_PositionsCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout();
        var child = new TestView(100, 50);
        layout.Add(child);
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        manager.ArrangeChildren(new Rect(20, 30, 300, 500));

        // Assert
        child.Frame.X.Should().Be(20);
        child.Frame.Y.Should().Be(30);
    }

    #endregion

    #region Edge Cases

    [Fact(DisplayName = "Measure should handle single item larger than constraint")]
    public void Measure_SingleItemLargerThanConstraint_ReturnsConstrainedSize()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(500, 50));
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(300, 500);

        // Assert
        // Item width gets constrained by available width during measurement
        result.Width.Should().Be(300);
        result.Height.Should().Be(50);
    }

    [Fact(DisplayName = "ArrangeChildren should handle no expandable items")]
    public void ArrangeChildren_NoExpandableItems_KeepsOriginalSize()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ExpandMode = WrapLayoutExpandMode.Distribute,
            HorizontalSpacing = 0
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        // No expand ratios set (default 0)
        var manager = CreateLayoutManager(layout);
        manager.Measure(400, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 400, 500));

        // Assert
        child1.Frame.Width.Should().Be(100);
        child2.Frame.Width.Should().Be(100);
    }

    [Fact(DisplayName = "Measure should handle all collapsed children")]
    public void Measure_AllCollapsed_ReturnsZero()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(100, 50) { IsVisible = false });
        layout.Add(new TestView(100, 50) { IsVisible = false });
        var manager = CreateLayoutManager(layout);

        // Act
        var result = manager.Measure(300, 500);

        // Assert
        result.Width.Should().Be(0);
        result.Height.Should().Be(0);
    }

    [Fact(DisplayName = "ArrangeChildren returns bounds size")]
    public void ArrangeChildren_ReturnsSpecifiedBoundsSize()
    {
        // Arrange
        var layout = new TestWrapLayout();
        layout.Add(new TestView(100, 50));
        var manager = CreateLayoutManager(layout);
        manager.Measure(300, 500);

        // Act
        var result = manager.ArrangeChildren(new Rect(0, 0, 300, 500));

        // Assert
        result.Width.Should().Be(300);
        result.Height.Should().Be(500);
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
            HorizontalSpacing = 5,
            VerticalSpacing = 10,
            ExpandMode = WrapLayoutExpandMode.Distribute
        };
        var child1 = new TestView(80, 40);
        var child2 = new TestView(80, 50);
        var child3 = new TestView(80, 30);
        var child4 = new TestView(80, 45);
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        layout.Add(child4);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        layout.SetExpandRatio(child3, 1);
        layout.SetExpandRatio(child4, 1);
        var manager = CreateLayoutManager(layout);

        // Available width: 200 - 20 (padding) = 180
        // Row 1: 80 + 5 + 80 = 165 (fits)
        // Row 2: 80 + 5 + 80 = 165 (fits)
        manager.Measure(200, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 200, 500));

        // Assert positions
        child1.Frame.X.Should().Be(10); // Left padding
        child1.Frame.Y.Should().Be(10); // Top padding
        child2.Frame.Y.Should().Be(10); // Same row

        child3.Frame.Y.Should().Be(70); // 10 + 50 + 10 (row height + vertical spacing)
        child4.Frame.Y.Should().Be(70);

        // Row heights
        child1.Frame.Height.Should().Be(50); // Max height in row 1
        child2.Frame.Height.Should().Be(50);
        child3.Frame.Height.Should().Be(45); // Max height in row 2
        child4.Frame.Height.Should().Be(45);

        // Expand: remaining = 180 - 165 = 15, split = 7.5 each
        child1.Frame.Width.Should().Be(87.5);
        child2.Frame.Width.Should().Be(87.5);
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
            HorizontalSpacing = 10
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(400, 500);

        // Act
        // Available: 400, Used: 100 + 10 + 100 = 210, Remaining: 190
        manager.ArrangeChildren(new Rect(0, 0, 400, 500));

        // Assert - items should start at X=0
        child1.Frame.X.Should().Be(0);
        child2.Frame.X.Should().Be(110);
    }

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment.Center should center items")]
    public void ArrangeChildren_ItemsAlignmentCenter_CentersItems()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.Center,
            HorizontalSpacing = 10
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(400, 500);

        // Act
        // Available: 400, Used: 100 + 10 + 100 = 210, Remaining: 190, Offset: 95
        manager.ArrangeChildren(new Rect(0, 0, 400, 500));

        // Assert - items should be centered
        child1.Frame.X.Should().Be(95);
        child2.Frame.X.Should().Be(205);
    }

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment.End should position items at end")]
    public void ArrangeChildren_ItemsAlignmentEnd_PositionsAtEnd()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.End,
            HorizontalSpacing = 10
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(400, 500);

        // Act
        // Available: 400, Used: 100 + 10 + 100 = 210, Remaining: 190, Offset: 190
        manager.ArrangeChildren(new Rect(0, 0, 400, 500));

        // Assert - items should be at end
        child1.Frame.X.Should().Be(190);
        child2.Frame.X.Should().Be(300);
    }

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment should not affect rows with expand")]
    public void ArrangeChildren_ItemsAlignmentWithExpand_ExpandTakesPrecedence()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.End,
            ExpandMode = WrapLayoutExpandMode.Distribute,
            HorizontalSpacing = 0
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        layout.SetExpandRatio(child1, 1);
        layout.SetExpandRatio(child2, 1);
        var manager = CreateLayoutManager(layout);
        manager.Measure(400, 500);

        // Act
        manager.ArrangeChildren(new Rect(0, 0, 400, 500));

        // Assert - expand takes all space, so alignment has no effect
        child1.Frame.X.Should().Be(0);
        child1.Frame.Width.Should().Be(200);
        child2.Frame.X.Should().Be(200);
        child2.Frame.Width.Should().Be(200);
    }

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment.Center and padding")]
    public void ArrangeChildren_ItemsAlignmentCenterWithPadding_CentersCorrectly()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.Center,
            Padding = new Thickness(20),
            HorizontalSpacing = 10
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        layout.Add(child1);
        layout.Add(child2);
        var manager = CreateLayoutManager(layout);
        manager.Measure(400, 500);

        // Act
        // Available: 400 - 40 = 360, Used: 210, Remaining: 150, Offset: 75
        manager.ArrangeChildren(new Rect(0, 0, 400, 500));

        // Assert - items should be centered within padding
        child1.Frame.X.Should().Be(95); // 20 (padding) + 75 (offset)
        child2.Frame.X.Should().Be(205);
    }

    [Fact(DisplayName = "ArrangeChildren with ItemsAlignment should apply per row")]
    public void ArrangeChildren_ItemsAlignmentCenter_AppliesPerRow()
    {
        // Arrange
        var layout = new TestWrapLayout
        {
            ItemsAlignment = WrapLayoutItemsAlignment.Center,
            HorizontalSpacing = 10,
            VerticalSpacing = 10
        };
        var child1 = new TestView(100, 50);
        var child2 = new TestView(100, 50);
        var child3 = new TestView(50, 50); // Smaller item in row 2
        layout.Add(child1);
        layout.Add(child2);
        layout.Add(child3);
        var manager = CreateLayoutManager(layout);
        // Width constraint causes wrap after child2
        manager.Measure(250, 500);

        // Act
        // Row 1: Available: 250, Used: 210, Remaining: 40, Offset: 20
        // Row 2: Available: 250, Used: 50, Remaining: 200, Offset: 100
        manager.ArrangeChildren(new Rect(0, 0, 250, 500));

        // Assert
        child1.Frame.X.Should().Be(20);
        child2.Frame.X.Should().Be(130);
        child3.Frame.X.Should().Be(100);
    }

    #endregion
}

