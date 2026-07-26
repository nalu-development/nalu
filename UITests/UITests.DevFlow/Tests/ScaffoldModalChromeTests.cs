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
    private const string PageName = "Scaffold Modal Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

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
    public async Task SystemBackPopsModalThroughEngine()
    {
        Assert.SkipUnless(!await App.IsAppleAsync(), "System back is an Android-only channel.");

        await WaitDisplayedAsync("ModalHomePage");
        await App.TapAsync("PushPlainModal");
        await WaitDisplayedAsync("PlainModalPage");

        // Android system back is NOT blocked for modals: it commits through the engine
        // (ILeavingGuard would be consulted; none here, so the modal pops).
        await App.SystemBackAsync();
        await WaitDisplayedAsync("ModalHomePage");
    }
}
