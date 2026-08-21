using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// The back PREVIEW (Android predictive back peek of the page below) must not start while a
/// navigation is still executing — here a pushed page whose <c>OnAppearingAsync</c> takes 2.5 s.
/// The back request itself keeps working: once the navigation completes, the same gesture pops.
/// </summary>
public class ScaffoldSlowAppearingChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Slow Appearing Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForBoundsAsync("SlowAppearHomePage", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    [Fact]
    public async Task PredictiveBackPreviewDoesNotStartWhileANavigationIsInFlight()
    {
        Assert.SkipUnless(await App.IsAndroidGestureNavigationAsync(), "Predictive back needs Android gesture navigation.");

        await App.TapAsync("PushSlowAppearButton");
        await App.WaitForTextAsync("SlowAppearState", "slow:appearing");
        var detail = await App.WaitForStableBoundsAsync("SlowAppearDetailPage");

        // The page below stays in the element tree at rest (its stack entry is alive); the peek is
        // observable as the TOP page being scrubbed sideways (and the below page gaining a
        // window rect). Neither must happen while OnAppearingAsync is still running.
        var restHome = (await App.FindElementAsync("SlowAppearHomePage"))?.WindowBounds;
        await App.PredictiveBackHoldAsync();
        await Task.Delay(300, TestContext.Current.CancellationToken);
        (await App.GetBoundsAsync("SlowAppearDetailPage")).X.Should().BeApproximately(detail.X, 1, "the top page must not be scrubbed while the navigation is in flight");
        ((await App.FindElementAsync("SlowAppearHomePage"))?.WindowBounds?.Width ?? 0).Should().Be(restHome?.Width ?? 0, "the page below must not be peeked while the navigation is in flight");
        await App.PredictiveBackReleaseAsync(commit: false);

        // Once the navigation completed, the same gesture previews and pops as usual.
        await App.WaitForTextAsync("SlowAppearState", "slow:appeared", TimeSpan.FromSeconds(6));
        await App.PredictiveBackHoldAsync();
        await App.WaitForBoundsAsync("SlowAppearDetailPage", b => b.X > detail.X + 10, TimeSpan.FromSeconds(3));
        await App.PredictiveBackReleaseAsync(commit: true);
        await App.WaitForBoundsAsync("SlowAppearHomePage", b => b.Y > 0);
    }
}
