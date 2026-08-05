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
    private const string PageName = "Slide Box Tests";

    private async Task<bool> IsAndroidAsync()
        => (await App.GetPlatformAsync()).Contains("android", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(PageName);
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
    public async Task PeekRealizesTheNeighborEagerly()
    {
        await App.WaitForTextAsync("SlideCreatedLabel", "Created:1");

        // Turning the end-side peek on makes the NEXT slide partially visible — it must
        // realize right away without navigating.
        await App.TapAsync("SlideTogglePeekButton");
        await App.WaitForTextAsync("SlideCreatedLabel", "Created:2");
        await App.WaitForTextAsync("SlideIndexLabel", "Index:0");
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
