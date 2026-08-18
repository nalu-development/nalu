using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Regression test for the WrapLayout measure/arrange wrap flip inside VirtualScroll:
/// the cell is measured at a fractional width (339.1) whose UIKit pixel-aligned frame is
/// NARROWER (339.0), so a wrap row summing exactly to the measured width used to re-wrap
/// during arrange, pushing the second child onto a phantom line outside the measured cell.
/// The width is deliberately smaller than every test device's window: Android clamps
/// RecyclerView cells to the window width, which would make the row wrap legitimately.
/// </summary>
public class WrapRoundingTests(NaluApp app) : BaseUiTest(app)
{
    private const string _pageName = "Wrap Rounding Tests";

    [Fact]
    public async Task WrapRowDoesNotReWrapWhenCellFrameIsPixelAligned()
    {
        await App.OpenTestPageAsync(_pageName);

        var a = await App.GetBoundsAsync("WrapA");
        var b = await App.GetBoundsAsync("WrapB");

        b.Y.Should().BeApproximately(a.Y, 1, "both children were measured on a single line, so arrange must keep them on it");
        b.X.Should().BeGreaterThan(a.X, "the second child sits to the right of the first");

        // And the row must stay inside the cell the wrap layout was measured for.
        var layout = await App.GetBoundsAsync("WrapRoundingLayout");
        b.Bottom.Should().BeLessThanOrEqualTo(layout.Bottom + 1, "no child may overflow the measured cell height");
    }
}
