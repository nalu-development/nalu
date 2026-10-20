using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Native hosts must give back what they host when retired (iOS <c>ScaffoldHostView</c>): hosted
/// MAUI views outlive their host — the tab bar area keeps its bar for the return, page hosts keep
/// their nav bar host, overlay content outlives its presentation — and a view left parented to a
/// host whose managed peer the GC collected crashes the next managed <c>Superview</c> read ("Failed
/// to marshal the Objective-C object … (type: Nalu_ScaffoldTabBarStrip)"), first of all the
/// remount when a tab bar area comes back.
/// </summary>
/// <remarks>
/// The probes are the deterministic guards, each asserting <c>superview=null</c> right after its host
/// is retired; on the unfixed code they read the retired host (or, after a collection, the field
/// exception itself: <c>threw=RuntimeException</c>). The tab bar probe must run only once the strip
/// is RETIRED — the bar slides out first, and a probe during the slide legitimately finds the
/// still-mounted strip. The round trip collects INSIDE the switch, the field shape; driven by raw
/// taps it fails on the unfixed code in <c>ScaffoldChromeStrip..ctor</c> ← <c>MountTabBar</c>, under
/// the agent its timing is not guaranteed to land in the window, so it guards the remount itself.
/// </remarks>
public class ScaffoldStripReuseChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Strip Reuse Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForElementAsync("SrTabOnePage");
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    [Fact]
    public async Task RetiredTabBarStripReleasesItsBar()
    {
        await SkipUnlessAppleAsync();
        await SwitchToPlainAreaAsync();

        await App.TapAsync("SrProbe");
        await App.WaitForTextAsync("SrProbeResult", "superview=null");
    }

    [Fact]
    public async Task RetiredNavBarStripReleasesItsBarHost()
    {
        await SkipUnlessAppleAsync();

        await App.TapAsync("SrNavBarProbe");
        await App.WaitForTextAsync("SrNavBarProbeResult", "superview=null", TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task ClosedPopupHostReleasesItsPanel()
    {
        await SkipUnlessAppleAsync();

        await App.TapAsync("SrPopupProbe");
        await App.WaitForTextAsync("SrPopupProbeResult", "superview=null", TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task HandlerDisconnectRetiresTheTabBarAndTheReconnectRemountsIt()
    {
        await SkipUnlessAppleAsync();

        await App.TapAsync("SrReconnectProbe");
        await App.WaitForTextAsync("SrReconnectProbeResult", "superview=null parent=null");

        (await App.WaitForElementAsync("TabOne")).IsVisible.Should().BeTrue("the reconnected scaffold must mount the same bar again");
    }

    [Fact]
    public async Task TabBarRemountsWhenACollectionLandsInsideTheAreaSwitch()
    {
        await SkipUnlessAppleAsync();

        for (var round = 0; round < 3; round++)
        {
            await SwitchToPlainAreaAsync();

            await App.TapUntilAsync("SrCollectDuringSwitch", "SrTabOnePage");

            (await App.WaitForElementAsync("TabOne")).IsVisible.Should().BeTrue($"the tab bar must be back after round {round}");
        }
    }

    private async Task SkipUnlessAppleAsync()
        => Assert.SkipUnless(await App.IsAppleAsync(), "The managed-peer marshalling trap is UIKit-specific (the probes are iOS-only).");

    /// <summary>
    /// Leaves the tab bar area and waits for the strip to be RETIRED: the bar slides out first and
    /// is unmounted after, and its logical detach (the item leaving the tree) follows the unmount.
    /// </summary>
    private async Task SwitchToPlainAreaAsync()
    {
        await App.TapUntilAsync("SrGoPlain", "SrPlainPage");
        await App.WaitForElementGoneAsync("TabOne");
    }
}
