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
/// <param name="timeProvider">
/// The clock. Injectable so the settling MATHS can be tested without depending on the machine's
/// scheduling: driving it with real delays makes a unit test fail on a loaded CI runner, where a
/// 20ms sleep is not 20ms, and the reading it produces is the runner's mood rather than the code's
/// behaviour.
/// </param>
internal sealed class GestureVelocitySampler(TimeProvider? timeProvider = null)
{
    /// <summary>How far back a release looks. Long enough to survive one stalled frame, short enough to stay "now".</summary>
    private static readonly TimeSpan _window = TimeSpan.FromMilliseconds(100);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly (TimeSpan Time, double Position)[] _samples = new (TimeSpan, double)[16];
    private long _origin;
    private int _count;
    private int _next;

    /// <summary>Starts a fresh gesture at <paramref name="position"/>.</summary>
    public void Begin(double position)
    {
        _origin = _time.GetTimestamp();
        _count = 0;
        _next = 0;
        Push(TimeSpan.Zero, position);
    }

    /// <summary>Records where the gesture is now (same units the caller settles in).</summary>
    public void Add(double position)
    {
        if (_count == 0)
        {
            Begin(position);

            return;
        }

        Push(_time.GetElapsedTime(_origin), position);
    }

    private void Push(TimeSpan time, double position)
    {
        _samples[_next] = (time, position);
        _next = (_next + 1) % _samples.Length;

        if (_count < _samples.Length)
        {
            _count++;
        }
    }

    /// <summary>
    /// Speed over the window in units per second, signed like the positions. Zero when the gesture
    /// never moved, or when it has STOPPED.
    /// </summary>
    /// <remarks>
    /// Measured from the OLDEST sample still inside the window to the latest, over the time since
    /// that oldest one — against NOW rather than against the last sample, because a finger that
    /// stops reports nothing at all: swipe fast, hold, release, and a reading taken between the
    /// last two samples would still be the swipe. Someone who changes their mind usually pauses
    /// first, and a pause must not be read as intent.
    /// Several samples are kept because two are not enough: a lifting finger's final event
    /// usually reports no movement, and with a single sample of history the window can roll
    /// forward until both readings sit at the same position — a genuine flick then measures as
    /// perfectly still, which is the opposite of the truth.
    /// </remarks>
    public double Velocity
    {
        get
        {
            if (_count < 2)
            {
                return 0;
            }

            var now = _time.GetElapsedTime(_origin);
            var newest = _samples[(_next - 1 + _samples.Length) % _samples.Length];

            if (now - newest.Time > _window)
            {
                return 0;
            }

            var oldest = newest;

            for (var i = 1; i < _count; i++)
            {
                var candidate = _samples[(_next - 1 - i + (2 * _samples.Length)) % _samples.Length];

                if (now - candidate.Time > _window)
                {
                    break;
                }

                oldest = candidate;
            }

            var elapsed = (now - oldest.Time).TotalSeconds;

            return elapsed > 0.001 ? (newest.Position - oldest.Position) / elapsed : 0;
        }
    }

    /// <summary>Forgets the gesture; <see cref="Velocity"/> reads zero until the next <see cref="Begin"/>.</summary>
    public void Reset()
    {
        _origin = 0;
        _count = 0;
        _next = 0;
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
