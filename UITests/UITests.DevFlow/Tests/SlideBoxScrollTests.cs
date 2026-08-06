using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers a HORIZONTAL <see cref="SlideBox" /> hosting a vertical <c>ScrollView</c> per slide
/// (the "paged content with scrollable pages" case) against the "Slide Box Scroll Tests"
/// harness.
/// </summary>
/// <remarks>
/// Two layers, with different platform reach:
/// <list type="bullet">
/// <item>
/// The scrollable itself — that it scrolls at all inside a slide, keeps a per-slide offset
/// across slide changes and never disturbs the selection. Verified on BOTH platforms through
/// the agent's delta scroll.
/// </item>
/// <item>
/// Gesture ARBITRATION between the SlideBox's pan recognizer and the inner scroll view.
/// This needs real touch physics — the agent's synthetic swipe drives neither the MAUI
/// <c>PanGestureRecognizer</c> nor a native scroll — so it is Android-only, via adb-injected
/// touches (same boundary as <see cref="SlideBoxTests" />).
/// </item>
/// </list>
/// <para>
/// Both axes arbitrate correctly on both platforms: a vertical drag scrolls the inner view
/// without paging, a horizontal one pages without scrolling. iOS gets this from UIKit
/// (the parent's pan recognizes alongside the scroll view's); Android needs the interception
/// implemented in <c>SlideBoxViewGroup</c>, without which the ScrollView swallowed every
/// horizontal drag.
/// </para>
/// </remarks>
public class SlideBoxScrollTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Slide Box Scroll Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private async Task<bool> IsAndroidAsync()
        => (await App.GetPlatformAsync()).Contains("android", StringComparison.OrdinalIgnoreCase);

    [Fact]
    public async Task NestedScrollViewScrollsAndLeavesTheSelectionAlone()
    {
        await App.WaitForElementAsync("NestedTopA");
        (await App.GetDoublePropertyAsync("NestedScrollA", "ScrollY")).Should().Be(0);

        await App.ScrollAsync("NestedScrollA", deltaY: 300);

        // The inner scrollable moved and raised Scrolled (the page's witness label).
        await App.WaitForTextMatchAsync("NestedScrollLabel", t => t is not null && t.StartsWith("A:", StringComparison.Ordinal) && t != "A:0");
        (await App.GetDoublePropertyAsync("NestedScrollA", "ScrollY")).Should().BeGreaterThan(100);

        // Scrolling inside a slide must never page the box.
        (await App.FindElementAsync("NestedIndexLabel"))!.Text.Should().Be("Index:0");
    }

    [Fact]
    public async Task EachSlideKeepsItsOwnScrollOffsetAcrossSlideChanges()
    {
        await App.WaitForElementAsync("NestedTopA");
        await App.ScrollAsync("NestedScrollA", deltaY: 300);
        var offsetA = await App.GetDoublePropertyAsync("NestedScrollA", "ScrollY");
        offsetA.Should().BeGreaterThan(100);

        // The next slide starts at the top — offsets are per-slide, not shared.
        await App.TapAsync("NestedNextButton");
        await App.WaitForTextAsync("NestedIndexLabel", "Index:1");
        await App.WaitForElementAsync("NestedTopB");
        (await App.GetDoublePropertyAsync("NestedScrollB", "ScrollY")).Should().Be(0);

        await App.ScrollAsync("NestedScrollB", deltaY: 200);
        (await App.GetDoublePropertyAsync("NestedScrollB", "ScrollY")).Should().BeGreaterThan(50);

        // Coming back, the first slide is still where it was left.
        await App.TapAsync("NestedPrevButton");
        await App.WaitForTextAsync("NestedIndexLabel", "Index:0");
        (await App.GetDoublePropertyAsync("NestedScrollA", "ScrollY")).Should().Be(offsetA);
    }

    [Fact]
    public async Task RealVerticalDragOverTheScrollableScrollsWithoutPaging()
    {
        Assert.SkipWhen(!await IsAndroidAsync(), "Real touch physics are injected host-side via adb.");

        await App.WaitForElementAsync("NestedTopA");

        // A real upward drag started over the inner scrollable: the scrollable takes it...
        await App.AndroidRealSwipeAsync("NestedScrollA", 0, -250, durationMs: 300);

        await App.WaitForTextMatchAsync("NestedScrollLabel", t => t is not null && t.StartsWith("A:", StringComparison.Ordinal) && t != "A:0");
        (await App.GetDoublePropertyAsync("NestedScrollA", "ScrollY")).Should().BeGreaterThan(50);

        // ...and the SlideBox must not have paged on the incidental horizontal jitter.
        (await App.FindElementAsync("NestedIndexLabel"))!.Text.Should().Be("Index:0");
    }

    /// <summary>
    /// The arbitration that needed the Android platform fix: a horizontal drag whose finger
    /// starts OVER the inner scrollable must still page the box. Android delivers the stream to
    /// the child that consumed the DOWN (the ScrollView), so <c>SlideBoxViewGroup</c> claims it
    /// through <c>OnInterceptTouchEvent</c> once the drag is axis-dominant and past the slop.
    /// </summary>
    [Fact]
    public async Task RealHorizontalDragOverTheScrollableStillPages()
    {
        Assert.SkipWhen(!await IsAndroidAsync(), "Real touch physics are injected host-side via adb.");

        await App.WaitForElementAsync("NestedTopA");
        await App.AndroidRealSwipeAsync("NestedScrollA", -250, 0, durationMs: 300);

        await App.WaitForTextAsync("NestedIndexLabel", "Index:1");
        await App.WaitForElementAsync("NestedTopB");

        // ...and claiming the drag must not have scrolled the slide it left behind.
        (await App.GetDoublePropertyAsync("NestedScrollA", "ScrollY")).Should().Be(0);
    }
}
