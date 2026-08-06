using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers a custom <c>ScaffoldTabBar.TabBarView</c> with NATURAL-size content (the "Scaffold
/// Custom TabBar Label Tests" harness): a default-SafeAreaEdges inner container holding a bare
/// label. The bar owns the bottom inset — the inner container pads itself by it — so the label
/// keeps its natural height at the top of the bar and never inflates by the inset (the
/// double-count regression this suite guards against).
/// </summary>
public class ScaffoldCustomTabBarLabelChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Custom TabBar Label Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    [Fact]
    public async Task NaturalSizeContentIsNotInflatedByTheInset()
    {
        await App.WaitForBoundsAsync("CustomTabBarHomePage", b => b.Y > 0);

        var (_, windowHeight) = await App.GetWindowSizeAsync();
        var outer = await App.WaitForStableBoundsAsync("LabelTabBarOuter");
        var label = await App.GetBoundsAsync("LabelTabBarLabel");

        // The bar still reaches the very bottom edge of the screen (its inner container
        // consumes and paints the inset region).
        (outer.Y + outer.Height).Should().BeApproximately(windowHeight, 1.5, "the bar must cover the bottom system inset");

        // The label keeps its NATURAL single-line height at the top of the bar — before the
        // ownership fix the inset was counted twice and inflated it.
        label.Height.Should().BeLessThan(40, "a single-line label must not be inflated by the system inset");
        label.Y.Should().BeApproximately(outer.Y, 1.5);

        // Bar = label + the inset consumed by the inner container (>= 0 on inset-less devices).
        outer.Height.Should().BeGreaterThanOrEqualTo(label.Height - 0.5);
    }
}
