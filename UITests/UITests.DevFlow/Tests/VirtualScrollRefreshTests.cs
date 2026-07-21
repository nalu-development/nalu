using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// DevFlow tests for the VirtualScroll pull-to-refresh surface:
/// RefreshCommand, OnRefresh event, IsRefreshing two-way state and completion callbacks.
/// </summary>
/// <remarks>
/// The native pull GESTURE cannot be exercised: DevFlow synthetic swipes lack the touch
/// physics required by the platform refresh control. The page's TriggerRefreshButton invokes
/// the same IVirtualScrollController.Refresh pipeline the platform calls on a real pull.
/// </remarks>
public class VirtualScrollRefreshTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Virtual Scroll Refresh Tests";

    private async Task OpenPageAsync()
    {
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("R1");
    }

    [Fact]
    public async Task ItemsRender()
    {
        await OpenPageAsync();

        (await App.WaitForElementAsync("R1")).IsVisible.Should().BeTrue();
        (await App.WaitForElementAsync("R2")).IsVisible.Should().BeTrue();
    }

    [Fact]
    public async Task ProgrammaticIsRefreshingShowsAndHidesIndicator()
    {
        await OpenPageAsync();

        await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: False");

        await App.TapAsync("StartRefreshButton");
        await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: True");

        // Setting IsRefreshing programmatically shows the indicator only: no refresh callbacks.
        (await App.WaitForElementAsync("RefreshCommandCountLabel")).Text.Should().Be("Command: 0");
        (await App.WaitForElementAsync("RefreshEventCountLabel")).Text.Should().Be("Event: 0");

        await App.TapAsync("CompleteRefreshButton");
        await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: False");
    }

    [Fact]
    public async Task TriggerRefreshInvokesCommandAndEvent()
    {
        await OpenPageAsync();

        await App.TapAsync("TriggerRefreshButton");

        await App.WaitForTextAsync("RefreshCommandCountLabel", "Command: 1");
        await App.WaitForTextAsync("RefreshEventCountLabel", "Event: 1");

        // The refresh pipeline sets IsRefreshing while the refresh is pending.
        await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: True");

        // Invoking the completion callback ends the refresh.
        await App.TapAsync("CompleteRefreshButton");
        await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: False");
    }

    [Fact]
    public async Task RefreshCanRunMultipleTimes()
    {
        await OpenPageAsync();

        for (var i = 1; i <= 2; i++)
        {
            await App.TapAsync("TriggerRefreshButton");
            await App.WaitForTextAsync("RefreshCommandCountLabel", $"Command: {i}");
            await App.TapAsync("CompleteRefreshButton");
            await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: False");
        }
    }
}
