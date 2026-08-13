using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the EDGE-TO-EDGE branch of the custom tab bar contract (the "Scaffold Custom TabBar
/// EdgeToEdge Tests" harness): a bar root declaring <c>SafeAreaEdges.None</c> consumes nothing, so
/// the strip is exactly the bar's own height and the bar paints over the home indicator region.
/// </summary>
/// <remarks>
/// The counterpart of <see cref="ScaffoldCustomTabBarChromeTests"/> (consuming bar). Both branches
/// must hold for the same rule — the strip is the bar's settled measure — so that a fix for one
/// cannot silently regress the other.
/// </remarks>
public class ScaffoldCustomTabBarEdgeToEdgeChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Custom TabBar EdgeToEdge Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    [Fact]
    public async Task EdgeToEdgeBarIsExactlyItsOwnHeightAndPaintsOverTheInset()
    {
        await App.WaitForBoundsAsync("CustomTabBarHomePage", b => b.Y > 0);

        var (_, windowHeight) = await App.GetWindowSizeAsync();
        var container = await App.WaitForStableBoundsAsync("EdgeToEdgeTabBarContainer");

        // Flush with the screen bottom: the bar owns the inset region visually…
        (container.Y + container.Height).Should().BeApproximately(windowHeight, 1.5, "the bar reaches the bottom edge");

        // …while consuming NOTHING: declining the inset must not inflate the strip. Asserted
        // against platform ground truth, so a device without a bottom inset cannot make it pass
        // by accident (the consuming branch would measure 80 + inset here).
        var insets = await App.GetSafeAreaInsetsAsync();
        Assert.SkipWhen(insets.Bottom <= 0, "The device reports no bottom system inset: the two branches are indistinguishable.");

        container.Height.Should().BeApproximately(80, 1.5, "SafeAreaEdges.None declines the inset, so the strip is the bar's own height");
    }
}
