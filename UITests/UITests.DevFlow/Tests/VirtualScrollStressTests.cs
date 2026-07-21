using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Battle tests: a deterministic storm of grouped mutations (1-3 items or 1-2 whole
/// sections added/removed/moved/replaced every ~24ms, 120 ticks) hits the VirtualScroll
/// while it is scrolling — programmatic end/start oscillation from the page plus driver
/// swipes from the test — for both the grouped ObservableCollection adapter and the
/// DynamicData SourceCache pipeline (whose 30-record changesets escalate to Bind Resets).
/// </summary>
/// <remarks>
/// The storms are driven by a seeded LCG, so the final "Done S:x I:y" state is exact:
/// any missed/duplicated change or swallowed exception shifts the counts and fails the test.
/// </remarks>
public class VirtualScrollStressTests(NaluApp app) : BaseUiTest(app)
{
    private static readonly TimeSpan _stormTimeout = TimeSpan.FromSeconds(45);

    [Theory]
    [InlineData("Virtual Scroll Stress Tests", "StormButton", "Done S:30 I:94")]
    [InlineData("Virtual Scroll Stress Tests", "StormScrollButton", "Done S:30 I:94")]
    [InlineData("Virtual Scroll DynamicData Stress Tests", "StormButton", "Done S:47 I:587")]
    [InlineData("Virtual Scroll DynamicData Stress Tests", "StormScrollButton", "Done S:47 I:587")]
    public async Task MutationStormWhileScrollingSurvives(string pageName, string stormButton, string expectedStatus)
    {
        await App.OpenTestPageAsync(pageName);
        await App.WaitForElementAsync("StressStatusLabel");

        await App.TapAsync(stormButton);

        // Drive user-like scrolling concurrently with the storm (and, for the
        // Storm+scroll variant, with the page's own animated ScrollTo oscillation).
        for (var i = 0; i < 6; i++)
        {
            await App.SwipeAsync("StressScroll", i % 2 == 0 ? "up" : "down", 350);
        }

        await App.WaitForTextAsync("StressStatusLabel", expectedStatus, _stormTimeout);

        // The list must still be fully functional: jump to the last and first sections
        // and materialize their headers (the page reports which header to expect,
        // since DynamicData group ordering is not deterministic).
        await App.TapAsync("ScrollToLastButton");
        var lastHeader = await App.WaitForTextMatchAsync("LastSectionLabel", t => t?.StartsWith("SH ") == true);
        (await App.WaitForElementAsync(lastHeader!)).IsVisible.Should().BeTrue();

        await App.TapAsync("ScrollToFirstButton");
        var firstHeader = await App.WaitForTextMatchAsync("LastSectionLabel", t => t?.StartsWith("SH ") == true && t != lastHeader);
        (await App.WaitForElementAsync(firstHeader!)).IsVisible.Should().BeTrue();
    }
}
