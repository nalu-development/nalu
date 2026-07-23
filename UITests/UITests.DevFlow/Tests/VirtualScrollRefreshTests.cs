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

    // Android's SwipeRefreshLayout overlays its spinner disk over the content near the
    // top-center of the scroll (verified: disk surface at element-relative y≈44, with the
    // RefreshAccentColor arc inside). Sampling its center pixel proves the REAL loader shows.
    private const double _androidSpinnerProbeY = 44;

    private static int Delta((byte R, byte G, byte B) a, (byte R, byte G, byte B) b)
        => Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);

    /// <summary>Asserts the platform's real loading indicator is visible (or gone again).</summary>
    private async Task AssertLoaderVisibleAsync(ElementBounds firstItemBefore, (byte R, byte G, byte B) spinnerAreaBefore, bool visible)
    {
        if (await App.IsAppleAsync())
        {
            // The revealed UIRefreshControl physically pushes the content down by its height.
            if (visible)
            {
                await App.WaitForBoundsAsync("R1", b => b.Y >= firstItemBefore.Y + 30);
            }
            else
            {
                await App.WaitForBoundsAsync("R1", b => Math.Abs(b.Y - firstItemBefore.Y) <= 2);
            }
        }
        else
        {
            var scroll = await App.GetBoundsAsync("RefreshScroll");

            if (visible)
            {
                await App.WaitForPixelColorAsync("RefreshScroll", scroll.Width / 2, _androidSpinnerProbeY, c => Delta(c, spinnerAreaBefore) > 30);
            }
            else
            {
                await App.WaitForPixelColorAsync("RefreshScroll", scroll.Width / 2, _androidSpinnerProbeY, c => Delta(c, spinnerAreaBefore) < 20);
            }
        }
    }

    private async Task<(ElementBounds FirstItem, (byte R, byte G, byte B) SpinnerArea)> CaptureLoaderBaselineAsync()
    {
        var firstItem = await App.GetBoundsAsync("R1");
        var scroll = await App.GetBoundsAsync("RefreshScroll");
        var spinnerArea = await App.GetPixelColorAsync("RefreshScroll", scroll.Width / 2, _androidSpinnerProbeY);

        return (firstItem, spinnerArea);
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
    public async Task NativePullRunsFullRefreshPipeline()
    {
        await OpenPageAsync();

        // Guard: platform detection drives the platform-specific visual assertions below —
        // an "unknown" platform would silently skip them, making this test worthless.
        (await App.GetPlatformAsync()).Should().NotBe("unknown");

        var baseline = await CaptureLoaderBaselineAsync();

        // Fires the native refresh control's ValueChanged — the exact action a physical
        // pull fires — exercising control → handler → controller → command/event.
        await App.TapAsync("NativePullButton");

        await App.WaitForTextAsync("RefreshCommandCountLabel", "Command: 1");
        await App.WaitForTextAsync("RefreshEventCountLabel", "Event: 1");
        await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: True");

        // The native spinner is showing while the refresh is pending.
        await App.TapAsync("ReadNativeStateButton");
        await App.WaitForTextAsync("NativeStateLabel", "attached:True refreshing:True");

        // The REAL loading animation must be visible (state-only refreshing renders nothing).
        await AssertLoaderVisibleAsync(baseline.FirstItem, baseline.SpinnerArea, visible: true);

        // Completing the refresh ends the native spinner and restores the resting state.
        await App.TapAsync("CompleteRefreshButton");
        await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: False");
        await App.TapAsync("ReadNativeStateButton");
        await App.WaitForTextAsync("NativeStateLabel", "attached:True refreshing:False");
        await AssertLoaderVisibleAsync(baseline.FirstItem, baseline.SpinnerArea, visible: false);
    }

    [Fact]
    public async Task DisablingRefreshDetachesNativeControl()
    {
        await OpenPageAsync();

        // Disable: the native refresh control is detached, a pull becomes impossible.
        await App.TapAsync("ToggleRefreshEnabledButton");
        await App.WaitForTextAsync("NativeStateLabel", "attached:False refreshing:False");

        await App.TapAsync("NativePullButton");
        (await App.WaitForElementAsync("RefreshCommandCountLabel")).Text.Should().Be("Command: 0");
        (await App.WaitForElementAsync("RefreshEventCountLabel")).Text.Should().Be("Event: 0");

        // Re-enable: the control re-attaches and pulls work again.
        await App.TapAsync("ToggleRefreshEnabledButton");
        await App.WaitForTextAsync("NativeStateLabel", "attached:True refreshing:False");

        await App.TapAsync("NativePullButton");
        await App.WaitForTextAsync("RefreshCommandCountLabel", "Command: 1");

        await App.TapAsync("CompleteRefreshButton");
        await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: False");
    }

    [Fact]
    public async Task ProgrammaticIsRefreshingShowsNativeSpinner()
    {
        await OpenPageAsync();

        var baseline = await CaptureLoaderBaselineAsync();

        await App.TapAsync("StartRefreshButton");
        await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: True");
        await App.TapAsync("ReadNativeStateButton");
        await App.WaitForTextAsync("NativeStateLabel", "attached:True refreshing:True");

        // The REAL loading animation must be visible (state-only refreshing renders nothing).
        await AssertLoaderVisibleAsync(baseline.FirstItem, baseline.SpinnerArea, visible: true);

        await App.TapAsync("CompleteRefreshButton");
        await App.WaitForTextAsync("IsRefreshingLabel", "Refreshing: False");
        await App.TapAsync("ReadNativeStateButton");
        await App.WaitForTextAsync("NativeStateLabel", "attached:True refreshing:False");
        await AssertLoaderVisibleAsync(baseline.FirstItem, baseline.SpinnerArea, visible: false);
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
