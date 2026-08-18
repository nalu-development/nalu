using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers LIVE structure edits on a presented scaffold (the "Scaffold Hot Reload Tests"
/// harness) — the building blocks of XAML hot reload support: swapping the tab bar view,
/// adding/removing roots (including the CURRENT one), and a simulated full re-inflation
/// (the whole structure re-added as fresh instances, superseding the old one).
/// </summary>
public class ScaffoldHotReloadChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Hot Reload Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForBoundsAsync("HrAlphaPage", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    [Fact]
    public async Task TabBarViewSwapsLive()
    {
        await App.WaitForElementAsync("TabHrAlpha");

        await App.TapAsync("HrSwapTabBarView");

        await App.WaitForElementAsync("HrCustomBar");
        await App.WaitForElementGoneAsync("TabHrAlpha");
    }

    [Fact]
    public async Task AddedRootAppearsInTheBarAndNavigates()
    {
        await App.TapAsync("HrAddCharlie");
        await App.WaitForElementAsync("TabHrCharlie");

        await App.TapAsync("TabHrCharlie");
        await App.WaitForBoundsAsync("HrCharliePage", b => b.Y > 0);
    }

    [Fact]
    public async Task RemovedRootDisappearsFromTheBar()
    {
        await App.WaitForElementAsync("TabHrBravo");

        await App.TapAsync("HrRemoveBravo");

        await App.WaitForElementGoneAsync("TabHrBravo");

        // The current root is untouched.
        var alpha = await App.GetBoundsAsync("HrAlphaPage");
        alpha.Y.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RemovingTheCurrentRootFallsBackToTheNextOne()
    {
        await App.TapAsync("HrRemoveAlpha");

        // The engine re-presents the surviving root through a real navigation.
        await App.WaitForBoundsAsync("HrBravoPage", b => b.Y > 0);
        await App.WaitForElementGoneAsync("TabHrAlpha");
    }

    [Fact]
    public async Task SimulatedReinflationReplacesTheStructure()
    {
        await App.TapAsync("HrSimulateReload");

        // The fresh structure supersedes the old one (3 roots now) and the presented root is
        // re-created from its same-segment replacement.
        await App.WaitForElementAsync("TabHrCharlie");
        await App.WaitForBoundsAsync("HrAlphaPage", b => b.Y > 0);

        // The bar remains functional on the new instances.
        await App.TapAsync("TabHrBravo");
        await App.WaitForBoundsAsync("HrBravoPage", b => b.Y > 0);
    }
}
