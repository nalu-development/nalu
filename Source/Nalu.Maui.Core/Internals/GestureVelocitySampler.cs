using System.Diagnostics;

namespace Nalu.Internals;

/// <summary>
/// Tracks the speed of a drag from the positions it reports, so a release can be settled by where
/// the gesture was HEADING rather than only where it ended.
/// </summary>
/// <remarks>
/// <para>
/// It exists because MAUI's <c>PanUpdatedEventArgs</c> carries no velocity — only the cumulative
/// translation — and the cross-platform pan is the one path every gesture in the libraries shares.
/// Where a native recognizer offers the real thing (UIKit's <c>velocityInView</c>, Android's
/// <c>VelocityTracker</c>) that number is better; this is what the rest use.
/// </para>
/// <para>
/// Smoothed over a short WINDOW rather than the last two samples: a finger lifting off usually
/// reports a near-zero move as its final event, and the last-two-samples reading of a genuine
/// flick is then zero. Samples older than the window are dropped, so the value describes how the
/// gesture ended, not how it started.
/// </para>
/// </remarks>
internal sealed class GestureVelocitySampler
{
    /// <summary>How far back a release looks. Long enough to survive one stalled frame, short enough to stay "now".</summary>
    private static readonly TimeSpan _window = TimeSpan.FromMilliseconds(100);

    private readonly Stopwatch _clock = new();
    private (TimeSpan Time, double Position) _oldest;
    private (TimeSpan Time, double Position) _newest;
    private bool _hasOldest;

    /// <summary>Starts a fresh gesture at <paramref name="position"/>.</summary>
    public void Begin(double position)
    {
        _clock.Restart();
        _newest = (TimeSpan.Zero, position);
        _oldest = _newest;
        _hasOldest = true;
    }

    /// <summary>Records where the gesture is now (same units the caller settles in).</summary>
    public void Add(double position)
    {
        if (!_hasOldest)
        {
            Begin(position);

            return;
        }

        var now = _clock.Elapsed;

        // The previous newest becomes the baseline once the current one falls out of the window,
        // which keeps exactly one sample of history older than it — the shortest span that can
        // still measure a flick whose last event barely moved.
        if (now - _oldest.Time > _window)
        {
            _oldest = _newest;
        }

        _newest = (now, position);
    }

    /// <summary>
    /// Speed over the window in units per second, signed like the positions. Zero when the gesture
    /// never moved, or when it has STOPPED.
    /// </summary>
    /// <remarks>
    /// Measured against NOW rather than against the last sample, because a finger that stops
    /// reports nothing at all: swipe fast, hold, release — with the last two samples this reads as
    /// the swipe, and a settled gesture would commit as if it had been flicked. Someone who
    /// changes their mind usually pauses first, and a pause must not be read as intent.
    /// Including the still time in the denominator decays the reading as the pause grows; past a
    /// whole window of stillness it is simply zero.
    /// </remarks>
    public double Velocity
    {
        get
        {
            if (!_hasOldest)
            {
                return 0;
            }

            var now = _clock.Elapsed;

            if (now - _newest.Time > _window)
            {
                return 0;
            }

            var elapsed = (now - _oldest.Time).TotalSeconds;

            return elapsed > 0.001 ? (_newest.Position - _oldest.Position) / elapsed : 0;
        }
    }

    /// <summary>Forgets the gesture; <see cref="Velocity"/> reads zero until the next <see cref="Begin"/>.</summary>
    public void Reset()
    {
        _clock.Reset();
        _hasOldest = false;
        _oldest = default;
        _newest = default;
    }
}

/// <summary>
/// The shared rule for settling a released drag: where would it coast to, and was it a flick?
/// </summary>
/// <remarks>
/// Applying the control's existing threshold to the PROJECTED position rather than the released
/// one is what makes distance and speed one decision instead of two. A slow drag projects onto
/// itself and behaves exactly as before; a fast one carries itself over the line.
/// The flick rule is separate because projection alone still needs a plausible amount of travel:
/// a short, sharp swipe is an unambiguous instruction to advance ONE step, and reads as ignored
/// when the content springs back.
/// </remarks>
internal static class GestureSettling
{
    /// <summary>How far ahead a release is projected. UIKit-ish deceleration, rounded to something explainable.</summary>
    public const double ProjectionSeconds = 0.12;

    /// <summary>Above this (device-independent units per second) a gesture is a FLICK: one step, whatever the distance.</summary>
    public const double FlickVelocity = 400;

    /// <summary>Where the drag would coast to, from where it was released.</summary>
    public static double Project(double position, double velocity) => position + (velocity * ProjectionSeconds);

    /// <summary>The direction of a flick (-1 / +1), or 0 when the release was not fast enough to be one.</summary>
    public static int FlickDirection(double velocity)
        => Math.Abs(velocity) < FlickVelocity ? 0 : Math.Sign(velocity);

    /// <summary>
    /// How long the settle animation should take to cover <paramref name="remaining"/> units while
    /// keeping the speed the finger left behind — clamped so a slow release still animates and a
    /// violent one still reads as motion rather than a cut.
    /// </summary>
    public static uint SettleDuration(double remaining, double velocity, uint restingDuration)
    {
        var speed = Math.Abs(velocity);

        if (speed < 1)
        {
            return restingDuration;
        }

        var seconds = Math.Abs(remaining) / speed;

        return (uint) Math.Clamp(seconds * 1000, 120, restingDuration);
    }
}
