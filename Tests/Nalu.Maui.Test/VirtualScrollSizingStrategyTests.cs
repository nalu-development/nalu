using System.ComponentModel;
using System.Globalization;

namespace Nalu.Maui.Test;

public class VirtualScrollSizingStrategyTests
{
    [Fact(DisplayName = "Fill is the default-constructed strategy and measures nothing")]
    public void FillIsTheDefaultConstructedStrategy()
    {
        var strategy = VirtualScrollSizingStrategy.Fill;

        strategy.Mode.Should().Be(VirtualScrollSizingMode.Fill);
        strategy.Should().Be(default(VirtualScrollSizingStrategy));
        strategy.MaxExtent.Should().Be(double.PositiveInfinity);
    }

    [Fact(DisplayName = "Max carries its extent and compares by value")]
    public void MaxCarriesItsExtentAndComparesByValue()
    {
        var strategy = VirtualScrollSizingStrategy.Max(300);

        strategy.Mode.Should().Be(VirtualScrollSizingMode.Max);
        strategy.MaxExtent.Should().Be(300);
        strategy.Should().Be(VirtualScrollSizingStrategy.Max(300));
        strategy.Should().NotBe(VirtualScrollSizingStrategy.Max(301));
        strategy.Should().NotBe(VirtualScrollSizingStrategy.Unbounded);
    }

    [Theory(DisplayName = "Max rejects non-positive and NaN extents")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void MaxRejectsInvalidExtents(double extent)
        => ((Action) (() => VirtualScrollSizingStrategy.Max(extent))).Should().Throw<ArgumentOutOfRangeException>();

    [Fact(DisplayName = "Max of infinity collapses to Unbounded")]
    public void MaxOfInfinityCollapsesToUnbounded()
        => VirtualScrollSizingStrategy.Max(double.PositiveInfinity).Should().Be(VirtualScrollSizingStrategy.Unbounded);

    [Fact(DisplayName = "A number implicitly converts to Max")]
    public void ANumberImplicitlyConvertsToMax()
    {
        VirtualScrollSizingStrategy strategy = 250d;

        strategy.Should().Be(VirtualScrollSizingStrategy.Max(250));
    }

    [Theory(DisplayName = "Strings convert to the matching strategy")]
    [InlineData("Fill", VirtualScrollSizingMode.Fill)]
    [InlineData("fill", VirtualScrollSizingMode.Fill)]
    [InlineData("", VirtualScrollSizingMode.Fill)]
    [InlineData("Unbounded", VirtualScrollSizingMode.Unbounded)]
    [InlineData("UNBOUNDED", VirtualScrollSizingMode.Unbounded)]
    [InlineData("300", VirtualScrollSizingMode.Max)]
    [InlineData(" 300 ", VirtualScrollSizingMode.Max)]
    [InlineData("12.5", VirtualScrollSizingMode.Max)]
    public void StringsConvertToTheMatchingStrategy(string input, VirtualScrollSizingMode expected)
    {
        VirtualScrollSizingStrategy strategy = input;

        strategy.Mode.Should().Be(expected);
    }

    [Fact(DisplayName = "A numeric string keeps its extent, parsed invariantly")]
    public void ANumericStringKeepsItsExtent()
    {
        VirtualScrollSizingStrategy strategy = "12.5";

        strategy.MaxExtent.Should().Be(12.5);
    }

    [Theory(DisplayName = "Invalid strings throw")]
    [InlineData("Maximum")]
    [InlineData("0")]
    [InlineData("-10")]
    public void InvalidStringsThrow(string input)
        => ((Action) (() => { VirtualScrollSizingStrategy _ = input; })).Should().Throw<Exception>();

    [Fact(DisplayName = "The XAML type converter round-trips the ToString representation")]
    public void TheTypeConverterRoundTripsToString()
    {
        var converter = TypeDescriptor.GetConverter(typeof(VirtualScrollSizingStrategy));

        converter.CanConvertFrom(typeof(string)).Should().BeTrue();

        foreach (var strategy in new[] { VirtualScrollSizingStrategy.Fill, VirtualScrollSizingStrategy.Unbounded, VirtualScrollSizingStrategy.Max(300) })
        {
            converter.ConvertFrom(null, CultureInfo.InvariantCulture, strategy.ToString())
                     .Should()
                     .Be(strategy);
        }
    }
}
