using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers {nalu:ScrollDirectionValue} against the "Scaffold Scroll Direction Tests" harness:
/// direction-driven activation with hysteresis (down ActivateThreshold dp → activated, up
/// DeactivateThreshold dp → deactivated), the forced deactivation at the content top, and the
/// timed transition on a real target. The page scrolls by exact deltas through page-side
/// buttons — synthetic swipes travel differently per platform and would make thresholds flaky.
/// </summary>
public class ScaffoldScrollDirectionTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Scroll Direction Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForElementAsync("ScrollDirSnap");
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    /// <summary>Polls until the element's double property settles at the endpoint (covers the timed transition).</summary>
    private async Task WaitForDoubleAsync(string automationId, string propertyName, double expected, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(5));
        var last = double.NaN;

        do
        {
            last = await App.GetDoublePropertyAsync(automationId, propertyName);

            if (Math.Abs(last - expected) <= 0.01)
            {
                return;
            }

            await Task.Delay(100);
        }
        while (DateTime.UtcNow < deadline);

        last.Should().BeApproximately(expected, 0.01, $"{automationId}.{propertyName} must settle at the {expected} endpoint");
    }

    /// <summary>Asserts the property HOLDS the value for a moment (a state that must not flip).</summary>
    private async Task AssertDoubleHoldsAsync(string automationId, string propertyName, double expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(1);

        do
        {
            var value = await App.GetDoublePropertyAsync(automationId, propertyName);
            value.Should().BeApproximately(expected, 0.01, $"{automationId}.{propertyName} must hold {expected}");
            await Task.Delay(100);
        }
        while (DateTime.UtcNow < deadline);
    }

    private Task WaitForOffsetAtLeastAsync(double minimum)
        => App.WaitForTextMatchAsync("ScrollDirOffset", text => double.TryParse(text, out var offset) && offset >= minimum);

    [Fact]
    public async Task StartsDeactivatedAtRest()
    {
        await App.WaitForTextAsync("ScrollDirOffset", "0");

        await WaitForDoubleAsync("ScrollDirSnap", "Opacity", 1.0);
        await WaitForDoubleAsync("ScrollDirBar", "TranslationY", 0.0);
    }

    [Fact]
    public async Task DownTravelBelowTheThresholdDoesNotActivate()
    {
        await App.TapAsync("ScrollDirDown40");
        await WaitForOffsetAtLeastAsync(40);

        // 40dp of downward travel against a 100dp threshold: the mode must hold deactivated.
        await AssertDoubleHoldsAsync("ScrollDirSnap", "Opacity", 1.0);
    }

    [Fact]
    public async Task DownTravelBeyondTheThresholdActivatesAndAnimatesTheBar()
    {
        await App.TapAsync("ScrollDirDown120");
        await WaitForOffsetAtLeastAsync(120);

        // Snap target steps immediately; the bar slides to 96 over its 150ms transition.
        await WaitForDoubleAsync("ScrollDirSnap", "Opacity", 0.2);
        await WaitForDoubleAsync("ScrollDirBar", "TranslationY", 96.0);
    }

    [Fact]
    public async Task UpTravelBeyondTheDeactivateThresholdRestores()
    {
        await App.TapAsync("ScrollDirDown120");
        await WaitForOffsetAtLeastAsync(120);
        await WaitForDoubleAsync("ScrollDirSnap", "Opacity", 0.2);
        await WaitForDoubleAsync("ScrollDirSticky", "Opacity", 0.2);

        // 60dp back up: beyond the snap/bar deactivate threshold (50), nowhere near the
        // sticky one (100000) — the two states must diverge.
        await App.TapAsync("ScrollDirUp60");

        await WaitForDoubleAsync("ScrollDirSnap", "Opacity", 1.0);
        await WaitForDoubleAsync("ScrollDirBar", "TranslationY", 0.0);
        await AssertDoubleHoldsAsync("ScrollDirSticky", "Opacity", 0.2);
    }

    [Fact]
    public async Task GradientBackgroundRepaintsOnActivation()
    {
        // Deactivated: a solid LightGray (the gradient plan paints every stop gray).
        var bounds = await App.GetBoundsAsync("ScrollDirGradient");
        await App.WaitForPixelColorAsync("ScrollDirGradient", 6, 10, IsLightGray, TimeSpan.FromSeconds(5));
        await App.WaitForPixelColorAsync("ScrollDirGradient", bounds.Width - 6, 10, IsLightGray, TimeSpan.FromSeconds(5));

        // Activation swaps in the interpolated brush: the native background must repaint
        // into the red → blue gradient.
        await App.TapAsync("ScrollDirDown120");
        await WaitForOffsetAtLeastAsync(120);

        await App.WaitForPixelColorAsync("ScrollDirGradient", 6, 10, c => c.R > 160 && c.B < 96, TimeSpan.FromSeconds(5));
        await App.WaitForPixelColorAsync("ScrollDirGradient", bounds.Width - 6, 10, c => c.B > 160 && c.R < 96, TimeSpan.FromSeconds(5));

        // And back to the flat gray on deactivation.
        await App.TapAsync("ScrollDirUp60");
        await App.WaitForPixelColorAsync("ScrollDirGradient", 6, 10, IsLightGray, TimeSpan.FromSeconds(5));

        static bool IsLightGray((byte R, byte G, byte B) c)
            => Math.Abs(c.R - 211) < 20 && Math.Abs(c.G - 211) < 20 && Math.Abs(c.B - 211) < 20;
    }

    [Fact]
    public async Task TheContentTopRestoresEvenAnUnreachableDeactivateThreshold()
    {
        await App.TapAsync("ScrollDirDown120");
        await WaitForOffsetAtLeastAsync(120);
        await WaitForDoubleAsync("ScrollDirSticky", "Opacity", 0.2);

        // Back to the very top: the built-in force-deactivate must restore the sticky mode
        // its own threshold never could.
        await App.TapAsync("ScrollDirTop");
        await App.WaitForTextAsync("ScrollDirOffset", "0");

        await WaitForDoubleAsync("ScrollDirSticky", "Opacity", 1.0);
        await WaitForDoubleAsync("ScrollDirSnap", "Opacity", 1.0);
    }
}
