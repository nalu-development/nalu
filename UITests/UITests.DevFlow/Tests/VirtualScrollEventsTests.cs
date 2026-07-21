using System.Text.RegularExpressions;
using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// DevFlow tests for the VirtualScroll scroll notifications:
/// ScrolledCommand, the OnScrolled event and the scroll event args payload.
/// </summary>
/// <remarks>
/// ScrollStarted/ScrollEnded map to native DRAGGING callbacks, which synthetic DevFlow
/// swipes cannot trigger — they are intentionally not asserted here.
/// </remarks>
public partial class VirtualScrollEventsTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Virtual Scroll Events Tests";

    [GeneratedRegex(@"^Y:(?<y>\d+) T:(?<t>\d+) V:(?<v>\d+) P:(?<p>[\d.,]+)$")]
    private static partial Regex LastScrollRegex();

    private async Task OpenPageAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("E1");
    }

    private static int Counter(string? text) => int.TryParse(text?.Split(": ").Last(), out var n) ? n : -1;

    [Fact]
    public async Task SwipeRaisesScrolledCommandAndEvent()
    {
        await OpenPageAsync();

        await App.SwipeAsync("EventsScroll", "up", 300);

        await App.WaitForTextMatchAsync("ScrolledCountLabel", t => Counter(t) > 0);
        await App.WaitForTextMatchAsync("EventScrolledCountLabel", t => Counter(t) > 0);
    }

    [Fact]
    public async Task ScrolledArgsCarryScrollGeometry()
    {
        await OpenPageAsync();

        await App.SwipeAsync("EventsScroll", "up", 300);

        var text = await App.WaitForTextMatchAsync("LastScrollLabel", t => t is not null && t.StartsWith("Y:"));
        var match = LastScrollRegex().Match(text!);

        match.Success.Should().BeTrue($"unexpected LastScrollLabel format: '{text}'");
        int.Parse(match.Groups["y"].Value).Should().BeGreaterThan(0, "the list scrolled down");
        int.Parse(match.Groups["t"].Value).Should().BeGreaterThan(1000, "50 items produce a tall scrollable area");
        int.Parse(match.Groups["v"].Value).Should().BeGreaterThan(100, "the viewport has a real height");
    }

    [Fact]
    public async Task ResetClearsCounters()
    {
        await OpenPageAsync();

        await App.SwipeAsync("EventsScroll", "up", 300);
        await App.WaitForTextMatchAsync("ScrolledCountLabel", t => Counter(t) > 0);

        // Let the scroll settle: a late Scrolled event would overwrite the reset labels.
        await App.WaitForStableTextAsync("ScrolledCountLabel");

        await App.TapAsync("ResetCountersButton");

        await App.WaitForTextAsync("ScrolledCountLabel", "Scrolled: 0");
        await App.WaitForTextAsync("EventScrolledCountLabel", "EventScrolled: 0");
        await App.WaitForTextAsync("LastScrollLabel", "-");
    }
}
