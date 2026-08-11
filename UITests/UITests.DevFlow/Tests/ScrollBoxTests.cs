using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers <c>ScrollBox</c> programmatic scrolling: the ScrollToAsync completion contract
/// (exact targets, always-completing tasks), descendant targeting with
/// Start/Center/End/MakeVisible, and scroll event plumbing.
/// </summary>
/// <remarks>
/// User gesture surfaces (ScrollStarted/ScrollEnded sessions, pull-to-refresh) are not
/// harness-testable: synthetic swipes have no touch physics (see the DevFlow skill notes).
/// </remarks>
public class ScrollBoxTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Scroll Box Tests";

    // The page hosts 40 items, each exactly 44 units tall, in a vertical ScrollBox.

    [Fact]
    public async Task JumpScrollLandsOnTheExactTargetAndCompletes()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("Item1");

        await App.TapAsync("JumpTo400Button");

        // "done" proves the task completed; Y proves the exact landing position.
        await App.WaitForTextAsync("ScrollResultLabel", "done Y:400 X:0");
    }

    [Fact]
    public async Task AnimatedScrollCompletesItsTaskAtTheTarget()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("Item1");

        await App.TapAsync("AnimateTo600Button");

        // The task must complete when the animation settles — not before, not never.
        await App.WaitForTextAsync("ScrollResultLabel", "done Y:600 X:0", TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DescendantStartScrollsTheViewToTheLeadingEdge()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("Item1");

        await App.TapAsync("Item5StartButton");

        // Item5 starts at 4 * 44 = 176 in content coordinates.
        await App.WaitForTextAsync("ScrollResultLabel", "done Y:176 X:0");

        // And it must now sit at the top of the viewport (same Y as Item1 had at rest).
        var item5 = await App.WaitForStableBoundsAsync("Item5");
        var scrollBox = await App.GetBoundsAsync("TestScrollBox");
        item5.Y.Should().BeApproximately(scrollBox.Y, 1.5);
    }

    [Fact]
    public async Task DescendantCenterPlacesTheItemMidViewport()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("Item1");

        await App.TapAsync("Item30CenterButton");
        await App.WaitForTextMatchAsync("ScrollResultLabel", text => text?.StartsWith("done") == true);

        var item30 = await App.WaitForStableBoundsAsync("Item30");
        var scrollBox = await App.GetBoundsAsync("TestScrollBox");

        // Centered within the visible area: allow slack for platform safe-area insets, but the
        // item center must be well inside the middle third of the viewport.
        var itemCenter = item30.Y + (item30.Height / 2);
        var viewportCenter = scrollBox.Y + (scrollBox.Height / 2);
        itemCenter.Should().BeApproximately(viewportCenter, scrollBox.Height / 6);
    }

    [Fact]
    public async Task DescendantEndOnLastItemClampsToTheScrollRange()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("Item1");

        await App.TapAsync("Item40EndButton");
        await App.WaitForTextMatchAsync("ScrollResultLabel", text => text?.StartsWith("done") == true);

        // The last item must be fully visible at the bottom of the viewport.
        var item40 = await App.WaitForStableBoundsAsync("Item40");
        var scrollBox = await App.GetBoundsAsync("TestScrollBox");
        (item40.Y + item40.Height).Should().BeLessThanOrEqualTo(scrollBox.Y + scrollBox.Height + 1.5);
        item40.Y.Should().BeGreaterThan(scrollBox.Y);
    }

    [Fact]
    public async Task ScrolledEventsFlowDuringProgrammaticScrolls()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("Item1");
        await App.WaitForTextAsync("ScrolledCountLabel", "Scrolled: 0");

        await App.TapAsync("AnimateTo600Button");
        await App.WaitForTextMatchAsync("ScrollResultLabel", text => text?.StartsWith("done") == true, TimeSpan.FromSeconds(5));

        // An animated scroll produces a stream of Scrolled notifications and a final position.
        var scrolled = await App.WaitForTextMatchAsync("ScrolledCountLabel", text => text is not null and not "Scrolled: 0");
        scrolled.Should().NotBeNull();
        await App.WaitForTextMatchAsync("ScrollPositionLabel", text => text?.StartsWith("Y:600") == true);
    }

    [Fact]
    public async Task BackToStartCompletesEvenWhenAlreadyThere()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("Item1");

        // First tap scrolls nothing (already at start) — the task must still complete.
        await App.TapAsync("BackToStartButton");
        await App.WaitForTextAsync("ScrollResultLabel", "done Y:0 X:0");

        // And a no-op repeat must complete again (no "already at target" hang).
        await App.TapAsync("BackToStartButton");
        await App.WaitForTextAsync("ScrollResultLabel", "done Y:0 X:0");
    }
}

/// <summary>
/// Covers <c>ScrollBox.SizingStrategy</c> hugging (grow AND shrink with content, capped by
/// <c>Max</c>) and the pre-layout ScrollToAsync queue.
/// </summary>
public class ScrollBoxSizingTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Scroll Box Sizing Tests";

    [Fact]
    public async Task HugGrowsToTheCapAndShrinksBackWithContent()
    {
        await App.OpenTestPageAsync(PageName);

        // 2 items x 40 = 80: hugged exactly.
        await App.WaitForTextAsync("HugHeightLabel", "H:80");

        // 7 items x 40 = 280, capped by Max(200).
        await App.TapAsync("AddHugItemsButton");
        await App.WaitForTextAsync("HugHeightLabel", "H:200");

        // Back to 2 items: the box must SHRINK (the classic MAUI ScrollView failure).
        await App.TapAsync("RemoveHugItemsButton");
        await App.WaitForTextAsync("HugHeightLabel", "H:80");
    }

    [Fact]
    public async Task ScrollToIssuedBeforeFirstLayoutCompletesAfterIt()
    {
        await App.OpenTestPageAsync(PageName);

        // The request was issued in the page constructor, long before any layout pass:
        // it must execute right after the first layout and complete at the exact target.
        await App.WaitForTextAsync("PendingScrollResultLabel", "pending done Y:220");
    }
}
