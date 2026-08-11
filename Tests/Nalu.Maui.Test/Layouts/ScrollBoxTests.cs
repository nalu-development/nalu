namespace Nalu.Maui.Test.Layouts;

public class ScrollBoxTests
{
    // ComputeTargetDistance(position, elementStart, elementSize, referenceDistance, visibleSize, contentSize)

    [Fact(DisplayName = "Start scrolls the element to the leading edge")]
    public void StartScrollsTheElementToTheLeadingEdge()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.Start, 500, 100, 0, 400, 2000).Should().Be(500);

    [Fact(DisplayName = "Center scrolls the element to the middle of the visible area")]
    public void CenterScrollsTheElementToTheMiddle()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.Center, 500, 100, 0, 400, 2000).Should().Be(500 + 50 - 200);

    [Fact(DisplayName = "End scrolls the element to the trailing edge")]
    public void EndScrollsTheElementToTheTrailingEdge()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.End, 500, 100, 0, 400, 2000).Should().Be(500 + 100 - 400);

    [Fact(DisplayName = "MakeVisible does not move when the element is already fully visible")]
    public void MakeVisibleDoesNotMoveWhenAlreadyVisible()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.MakeVisible, 500, 100, 450, 400, 2000).Should().Be(450);

    [Fact(DisplayName = "MakeVisible scrolls minimally forward when the element is below the viewport")]
    public void MakeVisibleScrollsMinimallyForward()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.MakeVisible, 900, 100, 0, 400, 2000).Should().Be(900 + 100 - 400);

    [Fact(DisplayName = "MakeVisible scrolls minimally backward when the element is above the viewport")]
    public void MakeVisibleScrollsMinimallyBackward()
        => ScrollBox.ComputeTargetDistance(ScrollToPosition.MakeVisible, 100, 100, 800, 400, 2000).Should().Be(100);

    [Fact(DisplayName = "Targets are clamped to the valid scroll range")]
    public void TargetsAreClampedToTheValidScrollRange()
    {
        // Element at the very end: Start would overshoot past the max distance.
        ScrollBox.ComputeTargetDistance(ScrollToPosition.Start, 1900, 100, 0, 400, 2000).Should().Be(1600);

        // Element at the very beginning: End would undershoot below zero.
        ScrollBox.ComputeTargetDistance(ScrollToPosition.End, 0, 100, 500, 400, 2000).Should().Be(0);

        // Content smaller than the viewport: nothing to scroll.
        ScrollBox.ComputeTargetDistance(ScrollToPosition.Center, 100, 50, 0, 400, 300).Should().Be(0);
    }
}

public class ScrollBoxSizingStrategyTests
{
    [Fact(DisplayName = "Fill is the default-constructed strategy")]
    public void FillIsTheDefaultConstructedStrategy()
    {
        var strategy = ScrollBoxSizingStrategy.Fill;

        strategy.Mode.Should().Be(ScrollBoxSizingMode.Fill);
        strategy.Should().Be(default(ScrollBoxSizingStrategy));
        strategy.MaxExtent.Should().Be(double.PositiveInfinity);
    }

    [Fact(DisplayName = "Max carries its extent and compares by value")]
    public void MaxCarriesItsExtentAndComparesByValue()
    {
        var strategy = ScrollBoxSizingStrategy.Max(300);

        strategy.Mode.Should().Be(ScrollBoxSizingMode.Max);
        strategy.MaxExtent.Should().Be(300);
        strategy.Should().Be(ScrollBoxSizingStrategy.Max(300));
        strategy.Should().NotBe(ScrollBoxSizingStrategy.Unbounded);
    }

    [Theory(DisplayName = "Max rejects non-positive and NaN extents")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void MaxRejectsInvalidExtents(double extent)
        => ((Action) (() => ScrollBoxSizingStrategy.Max(extent))).Should().Throw<ArgumentOutOfRangeException>();

    [Theory(DisplayName = "String conversion parses modes and bare numbers")]
    [InlineData("Fill", ScrollBoxSizingMode.Fill)]
    [InlineData("fill", ScrollBoxSizingMode.Fill)]
    [InlineData("Unbounded", ScrollBoxSizingMode.Unbounded)]
    [InlineData("300", ScrollBoxSizingMode.Max)]
    [InlineData("", ScrollBoxSizingMode.Fill)]
    public void StringConversionParsesModesAndBareNumbers(string input, ScrollBoxSizingMode expectedMode)
        => ((ScrollBoxSizingStrategy) input).Mode.Should().Be(expectedMode);

    [Fact(DisplayName = "String conversion rejects unknown values")]
    public void StringConversionRejectsUnknownValues()
        => ((Action) (() => _ = (ScrollBoxSizingStrategy) "nope")).Should().Throw<FormatException>();

    [Theory(DisplayName = "ToString round-trips")]
    [InlineData("Fill")]
    [InlineData("Unbounded")]
    [InlineData("300")]
    public void ToStringRoundTrips(string value)
        => ((ScrollBoxSizingStrategy) value).ToString().Should().Be(value);
}
