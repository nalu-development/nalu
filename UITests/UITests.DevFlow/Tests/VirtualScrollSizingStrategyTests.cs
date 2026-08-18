using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers <c>VirtualScroll.SizingStrategy</c> against the "Virtual Scroll SizingStrategy Tests"
/// harness: a vertical list of fixed 40dp items inside an AUTO row, so the measured height IS the
/// observable contract (n items ⇒ n*40 of content).
/// </summary>
public class VirtualScrollSizingStrategyTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Virtual Scroll SizingStrategy Tests";
    private const double _itemExtent = 40;
    private const double _cap = 300;

    /// <summary>Platform dp rounding: heights are compared with a couple of dp of slack.</summary>
    private const double _tolerance = 2;

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(_pageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private Task<ElementBounds> WaitForHeightAsync(double expected)
        => App.WaitForBoundsAsync("SizingScroll", b => Math.Abs(b.Height - expected) <= _tolerance);

    [Fact]
    public async Task FillMeasuresNothingAndCollapsesInAnAutoRow()
    {
        // The default: the content size is never consulted, so an Auto row gives it nothing.
        // This is the behavior SizingStrategy exists to opt out of — and the one existing apps rely
        // on everywhere else (star rows / Fill).
        await App.WaitForElementAsync("SizingFillButton");
        await WaitForHeightAsync(0);
    }

    [Fact]
    public async Task MaxHugsTheContentWhileItFitsUnderTheCap()
    {
        await App.TapAsync("SizingMaxButton");

        // 2 items — well under the 300 cap.
        await WaitForHeightAsync(2 * _itemExtent);

        await App.TapAsync("SizingSomeItemsButton");
        await WaitForHeightAsync(5 * _itemExtent);
    }

    [Fact]
    public async Task MaxClampsAtTheCapForLongContent()
    {
        await App.TapAsync("SizingMaxButton");
        await App.TapAsync("SizingManyItemsButton");

        await WaitForHeightAsync(_cap);
    }

    [Fact]
    public async Task MaxStaysPutWhenClampedContentKeepsGrowing()
    {
        await App.TapAsync("SizingMaxButton");
        await App.TapAsync("SizingManyItemsButton");
        await WaitForHeightAsync(_cap);

        // The no-churn property: once clamped, pushing more items cannot move the container, so
        // the measured height must not budge (and no re-measure is requested at all).
        for (var i = 0; i < 3; i++)
        {
            await App.TapAsync("SizingAddItemButton");
        }

        await Task.Delay(500);
        (await App.GetBoundsAsync("SizingScroll")).Height.Should().BeApproximately(_cap, _tolerance);
    }

    [Fact]
    public async Task UnboundedGrowsWithTheContent()
    {
        await App.TapAsync("SizingUnboundedButton");
        await App.TapAsync("SizingSomeItemsButton");
        await WaitForHeightAsync(5 * _itemExtent);

        await App.TapAsync("SizingAddItemButton");
        await WaitForHeightAsync(6 * _itemExtent);
    }

    [Fact]
    public async Task SwitchingBackToFillStopsTrackingTheContent()
    {
        await App.TapAsync("SizingMaxButton");
        await WaitForHeightAsync(2 * _itemExtent);

        await App.TapAsync("SizingFillButton");

        // Fill never consults the content, so 50 items cannot grow the list. What it settles on
        // instead is the platform default for a view with no intrinsic size, and the two differ:
        // Android reports nothing (0), iOS keeps reporting the size it was last given. Both are
        // "not content-driven", which is the whole contract of Fill.
        await App.TapAsync("SizingManyItemsButton");
        await Task.Delay(500);

        (await App.GetBoundsAsync("SizingScroll")).Height.Should().BeLessThan(3 * _itemExtent);
    }
}
