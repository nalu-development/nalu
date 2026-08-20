using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers modal presentation (§7.1, Scaffold.PageMode) against the "Scaffold Modal Tests"
/// harness: modal pages cover the tab bar, show a title-only nav bar (X only for
/// DismissableModal), and pop through the engine (X tap / programmatic / Android system back).
/// </summary>
public class ScaffoldModalChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Modal Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(_pageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    [Fact]
    public async Task DismissableModalCoversTabBarAndClosesViaX()
    {
        await WaitDisplayedAsync("ModalHomePage");
        (await App.WaitForElementAsync("TabMHome")).IsVisible.Should().BeTrue();

        await App.TapAsync("PushDismissableModal");
        await WaitDisplayedAsync("DismissableModalPage");

        // The modal covers the tab bar and shows a title-only bar with the X.
        // (Hidden chrome buttons remain in the element tree with IsVisible=false.)
        await App.WaitForElementGoneAsync("TabMHome");
        (await App.WaitForElementAsync("NavBarBackButton")).IsVisible.Should().BeFalse();
        (await App.WaitForElementAsync("NavBarCloseButton")).IsVisible.Should().BeTrue();

        // X pops through the engine; the tab bar returns.
        await App.TapAsync("NavBarCloseButton");
        await WaitDisplayedAsync("ModalHomePage");
        (await App.WaitForElementAsync("TabMHome")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task TabBarReturnsToRestingFrameAfterModalRoundTrip()
    {
        // Regression: on Android, the strip translated into the system-bars region picked up
        // safe-area padding from the net10 inset listener while hidden, and the stale padding
        // survived the slide back in — the bar re-appeared ABOVE its resting position.
        await WaitDisplayedAsync("ModalHomePage");
        var restingBounds = await App.WaitForStableBoundsAsync("TabMHome");

        await App.TapAsync("PushDismissableModal");
        await WaitDisplayedAsync("DismissableModalPage");
        await App.WaitForElementGoneAsync("TabMHome");

        await App.TapAsync("NavBarCloseButton");
        await WaitDisplayedAsync("ModalHomePage");

        // Retry-until-match: the slide-in is still settling when the page becomes visible.
        await App.WaitForBoundsAsync(
            "TabMHome",
            b => Math.Abs(b.Y - restingBounds.Y) <= 1 && Math.Abs(b.Height - restingBounds.Height) <= 1
        );
    }

    [Fact]
    public async Task PlainModalHasNoCloseButtonAndClosesProgrammatically()
    {
        await WaitDisplayedAsync("ModalHomePage");

        await App.TapAsync("PushPlainModal");
        await WaitDisplayedAsync("PlainModalPage");

        await App.WaitForElementGoneAsync("TabMHome");
        (await App.WaitForElementAsync("NavBarCloseButton")).IsVisible.Should().BeFalse();
        (await App.WaitForElementAsync("NavBarBackButton")).IsVisible.Should().BeFalse();

        await App.TapAsync("ClosePlainModal");
        await WaitDisplayedAsync("ModalHomePage");
        (await App.WaitForElementAsync("TabMHome")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task SystemBackIsConsumedByPlainModal()
    {
        Assert.SkipUnless(!await App.IsAppleAsync(), "System back is an Android-only channel.");

        await WaitDisplayedAsync("ModalHomePage");
        await App.TapAsync("PushPlainModal");
        await WaitDisplayedAsync("PlainModalPage");

        // A plain Modal blocks system back entirely: the press is consumed, nothing pops.
        await App.SystemBackAsync();
        await Task.Delay(800, TestContext.Current.CancellationToken);
        await WaitDisplayedAsync("PlainModalPage");

        // Programmatic close remains the only dismissal.
        await App.TapAsync("ClosePlainModal");
        await WaitDisplayedAsync("ModalHomePage");
    }

    [Fact]
    public async Task SystemBackDismissesDismissableModal()
    {
        Assert.SkipUnless(!await App.IsAppleAsync(), "System back is an Android-only channel.");

        await WaitDisplayedAsync("ModalHomePage");
        await App.TapAsync("PushDismissableModal");
        await WaitDisplayedAsync("DismissableModalPage");

        // DismissableModal keeps the system back channel: pops through the engine.
        await App.SystemBackAsync();
        await WaitDisplayedAsync("ModalHomePage");
    }
}
