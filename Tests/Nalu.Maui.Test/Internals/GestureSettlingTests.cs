using Nalu.Internals;

namespace Nalu.Maui.Test.Internals;

/// <summary>
/// The rule both swipe gestures settle by: where the drag was HEADING (projection) and whether it
/// was a flick. The controls apply their own thresholds to those two answers — SlideBox commits
/// past a third of a page, the sheet snaps to the nearest detent — so what matters here is that a
/// still release changes nothing and a fast one carries.
/// </summary>
public class GestureSettlingTests
{
    [Fact(DisplayName = "A release with no speed projects onto itself")]
    public void ProjectionOfAStillReleaseIsWhereItIs()
        => GestureSettling.Project(120, 0).Should().Be(120, "a slow drag must behave exactly as it did before projection existed");

    [Fact(DisplayName = "Projection carries a fast release forward, in its own direction")]
    public void ProjectionFollowsTheVelocity()
    {
        GestureSettling.Project(100, 1000).Should().BeApproximately(220, 0.001);
        GestureSettling.Project(100, -1000).Should().BeApproximately(-20, 0.001);
    }

    [Theory(DisplayName = "Only a fast enough release is a flick, and it keeps its sign")]
    [InlineData(0, 0)]
    [InlineData(399, 0)]
    [InlineData(-399, 0)]
    [InlineData(400, 1)]
    [InlineData(-400, -1)]
    [InlineData(2500, 1)]
    public void FlickDirectionIsTheSignOfAFastRelease(double velocity, int expected)
        => GestureSettling.FlickDirection(velocity).Should().Be(expected);

    [Fact(DisplayName = "A still release settles over the control's resting duration")]
    public void SettleDurationFallsBackToTheRestingDuration()
        => GestureSettling.SettleDuration(200, 0, 250).Should().Be(250);

    [Fact(DisplayName = "A settle keeps the speed the finger left behind, within bounds")]
    public void SettleDurationFollowsTheVelocity()
    {
        // 100 units left at 1000/s is 100ms — floored at 120 so it still reads as motion.
        GestureSettling.SettleDuration(100, 1000, 250).Should().Be(120);

        // 200 units at 1000/s is 200ms: fast, and slower than the floor, so it is used as is.
        GestureSettling.SettleDuration(200, 1000, 250).Should().Be(200);

        // A crawl would compute far longer than the resting duration; it is capped there.
        GestureSettling.SettleDuration(200, 10, 250).Should().Be(250);
    }
}

/// <summary>
/// The sampler behind those answers: MAUI's pan reports no velocity, so it is measured from the
/// positions the drag reports.
/// </summary>
public class GestureVelocitySamplerTests
{
    /// <summary>
    /// A clock the test moves by hand. The sampler's whole job is dividing distance by time, and
    /// driving it with real delays measures the machine instead: the same assertions passed here
    /// in ~110ms and failed on a CI runner that stretched them to 376ms.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan by) => _timestamp += by.Ticks;
    }

    [Fact(DisplayName = "A gesture that never moved has no velocity")]
    public void AStillGestureHasNoVelocity()
    {
        var time = new ManualTimeProvider();
        var sampler = new GestureVelocitySampler(time);
        sampler.Begin(0);
        time.Advance(TimeSpan.FromMilliseconds(20));
        sampler.Add(0);

        sampler.Velocity.Should().Be(0);
    }

    [Fact(DisplayName = "Velocity is signed like the positions and survives a still final sample")]
    public void VelocityIsMeasuredOverAWindow()
    {
        var time = new ManualTimeProvider();
        var sampler = new GestureVelocitySampler(time);
        sampler.Begin(0);

        // Moving steadily: 20 units every 20ms is 1000 units per second.
        for (var i = 1; i <= 5; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(20));
            sampler.Add(-i * 20.0);
        }

        // ...then the last event a lifting finger reports: no movement at all. Measured over the
        // last two samples this would read as zero and a real flick would be missed.
        time.Advance(TimeSpan.FromMilliseconds(5));
        sampler.Add(-100.0);

        sampler.Velocity.Should().BeLessThan(-200, "the drag was travelling towards negative positions, fast");
    }

    [Fact(DisplayName = "A gesture that stopped before the release is not a flick")]
    public void AStoppedGestureHasNoVelocity()
    {
        var time = new ManualTimeProvider();
        var sampler = new GestureVelocitySampler(time);
        sampler.Begin(0);

        // A fast swipe: 40 units every 10ms is 4000 units per second.
        for (var i = 1; i <= 5; i++)
        {
            time.Advance(TimeSpan.FromMilliseconds(10));
            sampler.Add(-i * 40.0);
        }

        sampler.Velocity.Should().BeLessThan(-400, "released now, this is a flick");

        // ...and then the finger stops, still down, having changed its mind. Nothing is reported
        // while it rests, so only the clock can tell the difference.
        time.Advance(TimeSpan.FromMilliseconds(250));

        sampler.Velocity.Should().Be(0, "a gesture that has stopped must not settle as a flick");
    }

    [Fact(DisplayName = "A reset gesture reports no velocity until it starts again")]
    public void ResetForgetsTheGesture()
    {
        var time = new ManualTimeProvider();
        var sampler = new GestureVelocitySampler(time);
        sampler.Begin(0);
        time.Advance(TimeSpan.FromMilliseconds(10));
        sampler.Add(-50);
        sampler.Reset();

        sampler.Velocity.Should().Be(0);
    }
}
