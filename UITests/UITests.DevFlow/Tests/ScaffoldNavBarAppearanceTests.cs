using System.Globalization;
using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the nav bar appearance chain (§5.2 revision) against the
/// "Scaffold NavBar Appearance Tests" harness: the scaffold-level appearance as global surface,
/// a page-level per-property delta, live mutation of a page appearance object, and the
/// TitleView page-model binding contract.
/// </summary>
public class ScaffoldNavBarAppearanceTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold NavBar Appearance Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    /// <summary>The strip surface opacity ("NavBarSurface" is the appearance target).</summary>
    private async Task<double> GetSurfaceOpacityAsync()
    {
        // The agent serializes numbers with the DEVICE locale (e.g. "0,25" on an Italian
        // simulator): normalize the decimal separator before an invariant parse.
        var raw = await App.GetPropertyAsync("NavBarSurface", "Opacity");

        return double.Parse(raw!.Replace(',', '.'), CultureInfo.InvariantCulture);
    }

    private async Task WaitForSurfaceOpacityAsync(double expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        double last;

        do
        {
            last = await GetSurfaceOpacityAsync();

            if (Math.Abs(last - expected) < 0.01)
            {
                return;
            }

            await Task.Delay(100);
        }
        while (DateTime.UtcNow < deadline);

        last.Should().BeApproximately(expected, 0.01, "the strip surface must reflect the effective appearance opacity");
    }

    [Fact]
    public async Task PageAppearanceDeltaAppliesAndRestoresOnPop()
    {
        await WaitDisplayedAsync("AppearancePageHome");
        await App.WaitForTextAsync("NavBarTitleLabel", "Appearance Home");
        await WaitForSurfaceOpacityAsync(1.0);

        await App.TapAsync("PushAppearanceStyled");
        await WaitDisplayedAsync("AppearancePageStyled");
        await WaitForSurfaceOpacityAsync(0.5);

        await App.TapAsync("PopAppearanceStyled");
        await WaitDisplayedAsync("AppearancePageHome");
        await WaitForSurfaceOpacityAsync(1.0);
    }

    [Fact]
    public async Task AppearanceMutationAppliesLive()
    {
        await WaitDisplayedAsync("AppearancePageHome");

        await App.TapAsync("PushAppearanceStyled");
        await WaitDisplayedAsync("AppearancePageStyled");
        await WaitForSurfaceOpacityAsync(0.5);

        // Mutating the page's live appearance object re-applies without any navigation.
        await App.TapAsync("MutateAppearance");
        await WaitForSurfaceOpacityAsync(0.25);
    }

    [Fact]
    public async Task OverlapModeRemovesTheTopInset()
    {
        await WaitDisplayedAsync("AppearancePageHome");

        // Regular pages sit BELOW the bar strip (status inset + bar content).
        var insetBounds = await App.GetBoundsAsync("AppearancePageHome");
        insetBounds.Y.Should().BeGreaterThan(40);

        await App.TapAsync("PushAppearanceOverlap");

        // Overlap mode: the page lays out from the absolute top edge — the marker (in a
        // SafeAreaEdges.None grid) lands at y = 0 while the bar still presents above it.
        await App.WaitForBoundsAsync("AppearanceOverlapTop", b => b.Y < 10);
        await App.WaitForTextAsync("NavBarTitleLabel", "Overlap Title");

        await App.TapAsync("PopAppearanceOverlap");
        await WaitDisplayedAsync("AppearancePageHome");
        await App.WaitForBoundsAsync("AppearancePageHome", b => Math.Abs(b.Y - insetBounds.Y) <= 1);
    }

    [Fact]
    public async Task ScrollTrackerFeedsTheContextOffset()
    {
        await WaitDisplayedAsync("AppearancePageHome");

        await App.TapAsync("PushAppearanceScroll");
        await WaitDisplayedAsync("AppearancePageScroll");

        // The TitleView binds the context's ScrollOffset via NavBarBinding: 0 at rest.
        await App.WaitForTextAsync("ScrollOffsetTitle", "0");

        // Deterministic page-side scroll (400dp): the native observation must follow.
        await App.TapAsync("ScrollTrackedDown");

        await App.WaitForTextMatchAsync(
            "ScrollOffsetTitle",
            text => double.TryParse(text, out var offset) && offset > 300
        );

        await App.TapAsync("PopAppearanceScroll");
        await WaitDisplayedAsync("AppearancePageHome");
    }

    [Fact]
    public async Task ScrollObservationIsReleasedOnPop()
    {
        await WaitDisplayedAsync("AppearancePageHome");

        await App.TapAsync("PushAppearanceScroll");
        await WaitDisplayedAsync("AppearancePageScroll");
        await App.TapAsync("ScrollTrackedDown");
        await App.WaitForTextMatchAsync("ScrollOffsetTitle", text => double.TryParse(text, out var offset) && offset > 300);

        // Pop disposes the model, which registers itself for collection: the model covers the
        // page and its tracked ScrollView, so a leaked native scroll observer (iOS KVO token
        // retains the UIScrollView) would keep the whole chain alive and fail the GC check.
        await App.TapAsync("PopAppearanceScroll");
        await WaitDisplayedAsync("AppearancePageHome");
        await App.TapAsync("ExitAppearanceHome");

        await App.WaitForElementAsync("TestName");
        await App.TapAsync("CheckLeaksButton");
        await App.WaitForTextAsync("LeaksLabel", "Leaked:0");
    }

    [Fact]
    public async Task TitleViewBindsThePageModel()
    {
        await WaitDisplayedAsync("AppearancePageHome");

        await App.TapAsync("PushAppearanceStyled");
        await WaitDisplayedAsync("AppearancePageStyled");

        // The TitleView label binds "Heading" on the PAGE MODEL — resolving means the slot
        // propagated the page's BindingContext (not the nav bar context) to hosted content.
        await App.WaitForTextAsync("AppearanceTitleView", "Model Heading");

        await App.TapAsync("PopAppearanceStyled");
        await WaitDisplayedAsync("AppearancePageHome");
        await App.WaitForTextAsync("NavBarTitleLabel", "Appearance Home");
    }

    [Fact]
    public async Task TitleForegroundIsASeparateChannelFromForeground()
    {
        await WaitDisplayedAsync("AppearancePageHome");

        await App.TapAsync("PushAppearanceOverlap");
        await App.WaitForTextAsync("NavBarTitleLabel", "Overlap Title");

        // The overlap page sets Foreground = White (buttons) and TitleForeground = Gold (title):
        // both resolve through the context independently — the probes read the effective values.
        await App.WaitForTextAsync("AppearanceOverlapForeground", "#FFFFFF");
        await App.WaitForTextAsync("AppearanceOverlapTitleForeground", "#FFD700");

        await App.TapAsync("PopAppearanceOverlap");
        await WaitDisplayedAsync("AppearancePageHome");
    }
}
