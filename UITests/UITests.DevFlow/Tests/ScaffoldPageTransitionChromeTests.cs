using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the §8.2 declarative page-transition spec against the "Scaffold Page Transition
/// Tests" harness: pages declaring SlideUpFade and ZoomFade land at their natural geometry
/// after push, and the behind page is fully restored (transform/opacity) after pop — the
/// motion itself is verified visually.
/// </summary>
public class ScaffoldPageTransitionChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold Page Transition Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    [Fact]
    public async Task SlideUpFadePageLandsAndRestores()
    {
        await WaitDisplayedAsync("PtRootPage");
        var rootLabel = await App.WaitForStableBoundsAsync("PtRootPage");

        await App.TapAsync("PushPtSlideUp");
        await WaitDisplayedAsync("PtSlideUpPage");

        // The pushed page must settle at NATURAL geometry (no leftover enter offset/scale).
        var pushed = await App.WaitForStableBoundsAsync("PtSlideUpPage");
        pushed.X.Should().BeApproximately(rootLabel.X, 1, "the entered page settles unscaled at its natural position");

        await App.TapAsync("PopPtSlideUp");
        await WaitDisplayedAsync("PtRootPage");

        // The behind page must be fully restored after its Behind (scale 0.97 / dim) round trip.
        await App.WaitForBoundsAsync(
            "PtRootPage",
            b => Math.Abs(b.X - rootLabel.X) <= 1 && Math.Abs(b.Y - rootLabel.Y) <= 1 && Math.Abs(b.Width - rootLabel.Width) <= 1
        );
    }

    [Fact]
    public async Task ZoomFadePageLandsAndRestoresAcrossRoundTrips()
    {
        await WaitDisplayedAsync("PtRootPage");
        var rootLabel = await App.WaitForStableBoundsAsync("PtRootPage");

        for (var i = 0; i < 2; i++)
        {
            await App.TapAsync("PushPtZoom");
            await WaitDisplayedAsync("PtZoomPage");
            (await App.WaitForElementAsync("PtZoomPage")).IsVisible.Should().BeTrue();

            await App.TapAsync("PopPtZoom");
            await WaitDisplayedAsync("PtRootPage");

            await App.WaitForBoundsAsync(
                "PtRootPage",
                b => Math.Abs(b.X - rootLabel.X) <= 1 && Math.Abs(b.Y - rootLabel.Y) <= 1 && Math.Abs(b.Width - rootLabel.Width) <= 1
            );
        }
    }
}
