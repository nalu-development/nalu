using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// SlideBox behavior against the "Slide Box Tests" harness: lazy realization with
/// forever-retention (the created-counter), disabled-slide skipping + teardown, peek-driven
/// eager neighbor realization, and (Android) a real swipe committing a slide change.
/// </summary>
public class SlideBoxTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Slide Box Tests";

    private async Task<bool> IsAndroidAsync()
        => (await App.GetPlatformAsync()).Contains("android", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForElementAsync("SlideBox");
        await WaitDisplayedAsync("SlideA");
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private async Task WaitDisplayedAsync(string automationId)
        => await App.WaitForBoundsAsync(automationId, bounds => bounds is { Width: > 0, Height: > 0 });

    [Fact]
    public async Task SlidesRealizeLazilyAndAreRetainedForever()
    {
        // Only the first slide exists at rest (no peek configured).
        await App.WaitForTextAsync("SlideCreatedLabel", "Created:1");

        await App.TapAsync("SlideNextButton");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:1");
        await App.WaitForTextAsync("SlideCreatedLabel", "Created:2");

        // Back and forth again: retention means NOTHING new is created.
        await App.TapAsync("SlidePrevButton");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:0");
        await App.TapAsync("SlideNextButton");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:1");
        await App.WaitForTextAsync("SlideCreatedLabel", "Created:2");
    }

    [Fact]
    public async Task DisabledSlideIsSkippedAndTornDown()
    {
        // Visit B so it has content to tear down.
        await App.TapAsync("SlideNextButton");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:1");

        // Disabling the CURRENT slide advances to the nearest enabled one and tears B down.
        await App.TapAsync("SlideToggleBButton");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:2");
        await WaitDisplayedAsync("SlideC");

        // Navigation now skips index 1 entirely.
        await App.TapAsync("SlidePrevButton");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:0");
        await App.TapAsync("SlideNextButton");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:2");

        // Re-enabling rebuilds LAZILY: nothing is created until B is visited again.
        var created = await App.GetPropertyAsync("SlideCreatedLabel", "Text");
        await App.TapAsync("SlideToggleBButton");
        await App.TapAsync("SlidePrevButton");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:1");
        await WaitDisplayedAsync("SlideB");
        Assert.NotEqual(created, await App.GetPropertyAsync("SlideCreatedLabel", "Text"));
    }

    [Fact]
    public async Task PeekRealizesTheNeighborEagerlyAndTogglingBackRestoresTheFullSlot()
    {
        await App.WaitForTextAsync("SlideCreatedLabel", "Created:1");
        var fullWidth = (await App.GetBoundsAsync("SlideRootA")).Width;

        // Turning the end-side peek on makes the NEXT slide partially visible — it must
        // realize right away without navigating, and the current slide narrows by the peek.
        await App.TapAsync("SlideTogglePeekButton");
        await App.WaitForTextAsync("SlideCreatedLabel", "Created:2");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:0");
        await App.WaitForBoundsAsync("SlideRootA", bounds => bounds.Width < fullWidth - 1);

        // Toggling the peek back off re-expands the slide to the full slot.
        await App.TapAsync("SlideTogglePeekButton");
        await App.WaitForBoundsAsync("SlideRootA", bounds => Math.Abs(bounds.Width - fullWidth) < 1);
    }

    [Fact]
    public async Task CrossAxisSafeAreaFlowsThroughToTheSlides()
    {
        // Horizontal orientation: the box must NOT consume the VERTICAL safe-area insets.
        // PLATFORM ground truth: the harness measures, in native window coordinates, that the
        // slide's platform view sits flush with the PHYSICAL window bottom, and reports the
        // real bottom inset — a zero inset would make the check vacuous.
        await App.TapAsync("SlideProbeButton");
        var probe = await App.WaitForTextMatchAsync("SlideProbeLabel", text => text is not null && text.StartsWith("Flush:", StringComparison.Ordinal));

        Assert.NotNull(probe);
        Assert.SkipWhen(probe.EndsWith("Inset:0", StringComparison.Ordinal), "No bottom system inset on this device: the flow-through check would be vacuous.");
        Assert.StartsWith("Flush:True", probe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PeekBandShowsTheIncomingSecondSlideDuringTheSettle()
    {
        Assert.SkipWhen(!await IsAndroidAsync(), "Mid-settle pixel sampling drives a real swipe via adb.");

        // End-side peek on + slowed transition so the settle phase is sampleable.
        await App.TapAsync("SlideTogglePeekButton");
        await App.WaitForTextAsync("SlideCreatedLabel", "Created:2");
        await App.TapAsync("SlideToggleSlowButton");

        var box = await App.GetBoundsAsync("SlideBox");

        // Swipe towards B and sample the peek band while the strip is still settling:
        // slide C must already be riding the strip there — never the box background
        // (DarkSlateGray) nor a late pop-in.
        await App.AndroidRealSwipeAsync("SlideBox", -300, 0, durationMs: 500);
        await Task.Delay(650, TestContext.Current.CancellationToken);
        var (r, g, b) = await App.GetPixelColorAsync("SlideBox", box.Width - 12, box.Height / 2);

        Assert.True(g >= 120 && r <= 110, $"Peek band mid-settle was ({r},{g},{b}) — expected slide C's LightSeaGreen, not the box background");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:1");
    }

    [Fact]
    public async Task RealSwipeCommitsASlideChange()
    {
        Assert.SkipWhen(!await IsAndroidAsync(), "Real swipes are injected host-side via adb.");

        await App.AndroidRealSwipeAsync("SlideBox", -250, 0, durationMs: 300);
        await App.WaitForTextAsync("SlideIndexLabel", "Index:1");
        await WaitDisplayedAsync("SlideB");

        // Swiping back returns to the first slide.
        await App.AndroidRealSwipeAsync("SlideBox", 250, 0, durationMs: 300);
        await App.WaitForTextAsync("SlideIndexLabel", "Index:0");
    }
}
