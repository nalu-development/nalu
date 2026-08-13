using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers a fully CUSTOM <c>ScaffoldTabBar.TabBarView</c> (the "Scaffold Custom TabBar Tests"
/// harness): an edge-to-edge container (SafeAreaEdges None) with an 80dp content band. The
/// platform strips lay the bar over the WHOLE strip — system-inset region included — so the
/// container must paint from the very bottom of the screen while the content band keeps its
/// 80dp at the top of the bar (above the inset).
/// </summary>
public class ScaffoldCustomTabBarChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Custom TabBar Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    [Fact]
    public async Task CustomBarExtendsIntoTheBottomSystemInset()
    {
        await App.WaitForBoundsAsync("CustomTabBarHomePage", b => b.Y > 0);

        var (_, windowHeight) = await App.GetWindowSizeAsync();
        var container = await App.WaitForStableBoundsAsync("CustomTabBarContainer");
        var content = await App.GetBoundsAsync("CustomTabBarContent");

        // The container reaches the very BOTTOM edge of the screen (before the fix it stopped
        // at the top of the system inset, leaving the inset region empty).
        (container.Y + container.Height).Should().BeApproximately(windowHeight, 1.5, "the custom bar must cover the bottom system inset");

        // The 80dp content band keeps its size, anchored at the top of the bar.
        content.Height.Should().BeApproximately(80, 1.5);
        content.Y.Should().BeApproximately(container.Y, 1.5);

        // EXACT inset contribution against platform ground truth. A relative check
        // ("the bar is at least as tall as its content") also passes when the inset
        // contribution is dropped entirely, and is vacuous on an inset-less device.
        var insets = await App.GetSafeAreaInsetsAsync();
        Assert.SkipWhen(insets.Bottom <= 0, "The device reports no bottom system inset: nothing to consume.");

        container.Height.Should().BeApproximately(
            content.Height + insets.Bottom,
            1.5,
            "the bar spans its content band PLUS exactly the bottom system inset it declares (Bottom=Container)"
        );
    }
}
