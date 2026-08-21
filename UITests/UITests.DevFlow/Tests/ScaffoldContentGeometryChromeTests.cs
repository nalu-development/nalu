using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// A page must come back from a push exactly as it left. The harness is the shape that makes a
/// lost inset visible: a nav bar over a SCROLLABLE root taller than the window, whose first label
/// sits directly under the bar. The page's top inset is the bar's footprint and the scrollable
/// consumes it as content padding, so a page that re-derives its inset while its bar has not been
/// measured yet slides that label under the bar and leaves it there.
/// </summary>
public class ScaffoldContentGeometryChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Content Geometry Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(_pageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    [Fact(DisplayName = "A covered VirtualScroll page returns from a push with its content where it left it")]
    public async Task PushAndPopLeaveTheVirtualScrollContentWhereItWas()
    {
        await App.OpenTestPageAsync("Scaffold Content Geometry Virtual Tests");

        await App.WaitForSettledDisplayAsync("GeoVirtualTopLabel");
        var before = await App.WaitForStableBoundsAsync("GeoVirtualTopLabel");

        await App.TapAsync("GeoVirtualPushDetail");
        await App.WaitForSettledDisplayAsync("GeoDetailPage");

        await App.TapAsync("GeoPopDetail");
        await App.WaitForSettledDisplayAsync("GeoVirtualTopLabel");

        var after = await App.WaitForStableBoundsAsync("GeoVirtualTopLabel");

        // A scrollable that takes its insets ONCE keeps whatever it was first given: this is the
        // shape that turns a momentarily-wrong inset into a permanently wrong layout.
        after.Y.Should().BeApproximately(before.Y, 1, "the page keeps its nav bar inset across a push and pop");
        after.X.Should().BeApproximately(before.X, 1, "nothing moves horizontally either");
    }

    [Fact(DisplayName = "A covered page returns from a push with its content where it left it")]
    public async Task PushAndPopLeaveTheContentWhereItWas()
    {
        await App.WaitForSettledDisplayAsync("GeoTopLabel");
        var before = await App.WaitForStableBoundsAsync("GeoTopLabel");

        await App.TapAsync("GeoPushDetail");
        await App.WaitForSettledDisplayAsync("GeoDetailPage");

        await App.TapAsync("GeoPopDetail");
        await App.WaitForSettledDisplayAsync("GeoTopLabel");

        var after = await App.WaitForStableBoundsAsync("GeoTopLabel");

        after.Y.Should().BeApproximately(before.Y, 1, "the page keeps its nav bar inset across a push and pop");
        after.X.Should().BeApproximately(before.X, 1, "nothing moves horizontally either");
    }
}
