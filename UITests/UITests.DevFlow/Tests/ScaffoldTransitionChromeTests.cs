using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers shared-element transitions (§8) against the "Scaffold Transition Tests" harness:
/// push/pop with a matching image pair and label pair. End-state focused: the animation itself
/// is verified visually; these tests prove the pairs land at their destination geometry and the
/// engine's flight cleanup restores the source views (alpha/transforms) on return.
/// </summary>
public class ScaffoldTransitionChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Transition Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    [Fact]
    public async Task SharedElementsLandAndCleanUpAcrossPushAndPop()
    {
        await WaitDisplayedAsync("TransitionGridPage");
        var gridImage = await App.WaitForStableBoundsAsync("GridHeroImage");
        gridImage.Width.Should().BeApproximately(120, 2);

        // Push: the detail hero must land at its own (full-width) geometry.
        await App.TapAsync("PushTransitionDetail");
        await WaitDisplayedAsync("TransitionDetailPage");
        var detailImage = await App.WaitForStableBoundsAsync("DetailHeroImage");
        detailImage.Width.Should().BeGreaterThan(gridImage.Width * 2, "the detail hero is full-width");
        (await App.WaitForElementAsync("DetailHeroTitle")).IsVisible.Should().BeTrue();

        // Pop: the grid page must come back with the source views fully restored (the flight
        // hides them during the animation — cleanup must undo that).
        await App.TapAsync("PopTransitionDetail");
        await WaitDisplayedAsync("TransitionGridPage");

        await App.WaitForBoundsAsync(
            "GridHeroImage",
            b => Math.Abs(b.X - gridImage.X) <= 1 && Math.Abs(b.Y - gridImage.Y) <= 1 && Math.Abs(b.Width - gridImage.Width) <= 1
        );

        (await App.WaitForElementAsync("GridHeroImage")).IsVisible.Should().BeTrue();
        (await App.WaitForElementAsync("GridHeroTitle")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task PredictiveBackRestoresSharedElementRendering()
    {
        // Android-only: the predictive-back pop skips the shared-element transition, so the
        // push's setTransitionAlpha(0) on the outgoing hero would never be undone without the
        // presenter's remount repair. A blank ImageView still reports visible bounds — only
        // pixels prove it renders (the regression this covers).
        Assert.SkipUnless(await App.IsAndroidGestureNavigationAsync(), "Predictive back needs Android gesture navigation.");

        await WaitDisplayedAsync("TransitionGridPage");
        var gridImage = await App.WaitForStableBoundsAsync("GridHeroImage");

        // Reference pixel at the hero's center while it is known-good.
        var reference = await App.GetPixelColorAsync("GridHeroImage", gridImage.Width / 2, gridImage.Height / 2);

        await App.TapAsync("PushTransitionDetail");
        await WaitDisplayedAsync("TransitionDetailPage");

        await App.PredictiveBackScrubAsync();
        await WaitDisplayedAsync("TransitionGridPage");

        await App.WaitForPixelColorAsync(
            "GridHeroImage",
            gridImage.Width / 2,
            gridImage.Height / 2,
            c => Math.Abs(c.R - reference.R) <= 3 && Math.Abs(c.G - reference.G) <= 3 && Math.Abs(c.B - reference.B) <= 3
        );
    }

    [Fact]
    public async Task RepeatedRoundTripsStayStable()
    {
        await WaitDisplayedAsync("TransitionGridPage");

        for (var i = 0; i < 3; i++)
        {
            await App.TapAsync("PushTransitionDetail");
            await WaitDisplayedAsync("TransitionDetailPage");
            await App.TapAsync("PopTransitionDetail");
            await WaitDisplayedAsync("TransitionGridPage");
        }

        (await App.WaitForElementAsync("GridHeroImage")).IsVisible.Should().BeTrue();
    }
}
