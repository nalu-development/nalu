using System.Globalization;
using Nalu.Internals;

namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// The scroll-direction engine behind {nalu:ScrollDirectionValue} / {nalu:ThemeScrollDirectionValue}:
/// directional travel accumulation with the activate/deactivate hysteresis, the forced deactivation
/// at the content top, theme endpoint selection, and the timed animator leg.
/// </summary>
public class ScrollDirectionValueTests
{
    private static ScrollDirectionInterpolationConverter CreateConverter(
        double activateThreshold = 100,
        double? deactivateThreshold = null,
        double deactivateBelow = 0,
        ScrollDirectionAnimator? animator = null,
        uint activateDuration = 0,
        uint? deactivateDuration = null,
        ScrollValueKind kind = ScrollValueKind.Double,
        object? deactivated = null,
        object? activated = null,
        object? deactivatedDark = null,
        object? activatedDark = null
    ) => new()
    {
        Kind = kind,
        ActivateThreshold = activateThreshold,
        DeactivateThreshold = deactivateThreshold,
        DeactivateBelow = deactivateBelow,
        ActivateDuration = activateDuration,
        DeactivateDuration = deactivateDuration,
        Animator = animator,
        DeactivatedLight = deactivated ?? 0.0,
        ActivatedLight = activated ?? 1.0,
        DeactivatedDark = deactivatedDark,
        ActivatedDark = activatedDark
    };

    private static object? Convert(ScrollDirectionInterpolationConverter converter, double offset, AppTheme theme = AppTheme.Light)
        => converter.Convert([offset, theme, 0.0], typeof(object), null, CultureInfo.InvariantCulture);

    private static object? Scroll(ScrollDirectionInterpolationConverter converter, params double[] offsets)
    {
        object? result = null;

        foreach (var offset in offsets)
        {
            result = Convert(converter, offset);
        }

        return result;
    }

    [Fact(DisplayName = "Starts deactivated and stays there below the activate threshold")]
    public void StartsDeactivated()
    {
        var converter = CreateConverter(activateThreshold: 100);

        Scroll(converter, 0, 40, 99).Should().Be(0.0);
        converter.Activated.Should().BeFalse();
    }

    [Fact(DisplayName = "Accumulated downward travel beyond the threshold latches activated — anywhere in the content")]
    public void DownTravelActivates()
    {
        var converter = CreateConverter(activateThreshold: 100);

        Scroll(converter, 500, 560, 590).Should().Be(0.0);
        Scroll(converter, 610).Should().Be(1.0);
        converter.Activated.Should().BeTrue();

        // Continuing down keeps it latched.
        Scroll(converter, 900).Should().Be(1.0);
    }

    [Fact(DisplayName = "Any upward movement restarts the downward travel count")]
    public void OppositeMovementRestartsTravel()
    {
        var converter = CreateConverter(activateThreshold: 100);

        // 60 down, a nudge up, then 70 more down: never 100 in ONE downward run.
        Scroll(converter, 100, 160, 150, 220).Should().Be(0.0);

        // 110 down in one run from the last direction change.
        Scroll(converter, 260).Should().Be(1.0);
    }

    [Fact(DisplayName = "Accumulated upward travel beyond the deactivate threshold latches back")]
    public void UpTravelDeactivates()
    {
        var converter = CreateConverter(activateThreshold: 100, deactivateThreshold: 40);

        Scroll(converter, 300, 450).Should().Be(1.0);

        Scroll(converter, 430).Should().Be(1.0);
        Scroll(converter, 405).Should().Be(0.0);
        converter.Activated.Should().BeFalse();
    }

    [Fact(DisplayName = "DeactivateThreshold falls back to ActivateThreshold when omitted")]
    public void DeactivateThresholdFallsBack()
    {
        var converter = CreateConverter(activateThreshold: 100);

        Scroll(converter, 300, 450).Should().Be(1.0);

        Scroll(converter, 390).Should().Be(1.0);
        Scroll(converter, 340).Should().Be(0.0);
    }

    [Fact(DisplayName = "The content top always restores the deactivated state, whatever the travel says")]
    public void TopForcesDeactivated()
    {
        var converter = CreateConverter(activateThreshold: 100, deactivateThreshold: 10_000);

        Scroll(converter, 100, 300).Should().Be(1.0);

        // A fling to the very top: nowhere near 10000dp of upward travel, yet deactivated.
        Scroll(converter, 0).Should().Be(0.0);
        converter.Activated.Should().BeFalse();
    }

    [Fact(DisplayName = "DeactivateBelow widens the always-deactivated window at the top")]
    public void DeactivateBelowWindow()
    {
        var converter = CreateConverter(activateThreshold: 100, deactivateThreshold: 10_000, deactivateBelow: 50);

        Scroll(converter, 100, 300).Should().Be(1.0);
        Scroll(converter, 40).Should().Be(0.0);
    }

    [Fact(DisplayName = "Top over-scroll (negative offsets) and its rebound feed no travel")]
    public void NegativeOverScrollIgnored()
    {
        var converter = CreateConverter(activateThreshold: 100);

        // The -50 → 0 rebound must not count as 50dp of downward travel.
        Scroll(converter, 0, -50, 0, 60).Should().Be(0.0);
        Scroll(converter, 110).Should().Be(1.0);
    }

    [Fact(DisplayName = "Zero thresholds latch on the first movement in that direction")]
    public void ZeroThresholdsSnap()
    {
        var converter = CreateConverter(activateThreshold: 0, deactivateThreshold: 0);

        Scroll(converter, 100, 101).Should().Be(1.0);
        Scroll(converter, 100.5).Should().Be(0.0);
    }

    [Fact(DisplayName = "Re-evaluation at the same offset (theme change) leaves the state untouched")]
    public void SameOffsetKeepsState()
    {
        var converter = CreateConverter(activateThreshold: 100);

        Scroll(converter, 100, 250).Should().Be(1.0);
        Convert(converter, 250, AppTheme.Dark).Should().Be(1.0);
        Convert(converter, 250).Should().Be(1.0);
    }

    [Fact(DisplayName = "Dark theme picks the dark endpoints, falling back to light when omitted")]
    public void DarkThemeSelectsEndpoints()
    {
        var converter = CreateConverter(
            activateThreshold: 100,
            kind: ScrollValueKind.Color,
            deactivated: Colors.White,
            activated: Colors.Black,
            activatedDark: Colors.Red
        );

        Scroll(converter, 100, 250);

        ((Color)Convert(converter, 250, AppTheme.Dark)!).Should().Be(Colors.Red);
        ((Color)Convert(converter, 250)!).Should().Be(Colors.Black);

        // DeactivatedDark omitted: falls back to DeactivatedLight.
        Scroll(converter, 0);
        ((Color)Convert(converter, 0, AppTheme.Dark)!).Should().Be(Colors.White);
    }

    [Fact(DisplayName = "Brush targets accept color endpoints and produce a solid brush")]
    public void BrushTargetsProduceSolidBrush()
    {
        var converter = CreateConverter(
            activateThreshold: 100,
            kind: ScrollValueKind.Brush,
            deactivated: Colors.Transparent,
            activated: new SolidColorBrush(Colors.Black)
        );

        var brush = (SolidColorBrush)Scroll(converter, 100, 250)!;
        brush.Color.Should().Be(Colors.Black);
    }

    [Fact(DisplayName = "A zero-duration animator snaps to the endpoint within the same evaluation")]
    public void ZeroDurationAnimatorSnaps()
    {
        var animator = new ScrollDirectionAnimator();
        var converter = CreateConverter(activateThreshold: 100, animator: animator, activateDuration: 0);

        // The flip animates the progress synchronously (duration 0) and the converter reads it
        // fresh off the animator — the same Convert call already returns the activated value.
        Scroll(converter, 100, 250).Should().Be(1.0);
        animator.Progress.Should().Be(1.0);

        Scroll(converter, 100).Should().Be(0.0);
        animator.Progress.Should().Be(0.0);
    }

    [Fact(DisplayName = "A timed animator moves the value over time after the flip")]
    public Task TimedAnimatorAnimates() => DispatcherTest.RunWithDispatcherStub(async () =>
    {
        var animator = new ScrollDirectionAnimator();
        var converter = CreateConverter(activateThreshold: 100, animator: animator, activateDuration: 80);

        // The flip starts a timer-driven transition: the same evaluation still reads progress 0.
        Scroll(converter, 100, 250).Should().Be(0.0);
        converter.Activated.Should().BeTrue();

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (animator.Progress < 1 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        animator.Progress.Should().Be(1.0);
        Convert(converter, 250).Should().Be(1.0);
    });
}
