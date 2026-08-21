using System.Globalization;

namespace Nalu.Internals;

/// <summary>
/// The engine behind <see cref="ScrollDirectionValueExtension"/>/<see cref="ThemeScrollDirectionValueExtension"/>:
/// a stateful multi-value converter over [offset, theme, animator progress] that watches the scroll
/// DIRECTION — accumulated downward travel beyond <c>ActivateThreshold</c> latches the activated state,
/// accumulated upward travel beyond <c>DeactivateThreshold</c> latches it back — and hands state flips to a
/// <see cref="ScrollDirectionAnimator"/>, which animates the endpoint interpolation over time.
/// One instance per binding: the latched state and travel are per-target.
/// </summary>
internal sealed class ScrollDirectionInterpolationConverter : IMultiValueConverter
{
    public required ScrollValueKind Kind { get; init; }

    /// <summary>Downward travel (dp) latching the activated state; zero or negative latches on the first downward frame.</summary>
    public required double ActivateThreshold { get; init; }

    /// <summary>Upward travel (dp) latching back to deactivated; null falls back to <see cref="ActivateThreshold"/>.</summary>
    public double? DeactivateThreshold { get; init; }

    /// <summary>Offsets at or below this always force the deactivated state (0 = the content top).</summary>
    public double DeactivateBelow { get; init; }

    /// <summary>Milliseconds of the deactivated → activated transition.</summary>
    public uint ActivateDuration { get; init; }

    /// <summary>Milliseconds of the activated → deactivated transition; null falls back to <see cref="ActivateDuration"/>.</summary>
    public uint? DeactivateDuration { get; init; }

    /// <summary>Time curve of both transitions (null = linear).</summary>
    public Easing? Easing { get; init; }

    /// <summary>
    /// The per-binding transition driver; its <see cref="ScrollDirectionAnimator.Progress"/> is read
    /// directly (not through the values array) so a synchronous snap inside a state flip is never
    /// observed stale. Null (unit tests) degrades to stepping between the endpoints.
    /// </summary>
    public ScrollDirectionAnimator? Animator { get; init; }

    public required object? DeactivatedLight { get; init; }

    public required object? ActivatedLight { get; init; }

    public object? DeactivatedDark { get; init; }

    public object? ActivatedDark { get; init; }

    /// <summary>Gets the latched state (starts deactivated).</summary>
    public bool Activated { get; private set; }

    private readonly ScrollValueBrushInterpolator _brushInterpolator = new();
    private double _lastOffset;
    private bool _tracking;

    // Signed travel in the current direction (down positive); any opposite movement restarts it.
    private double _travel;

    public object? Convert(object?[]? values, Type targetType, object? parameter, CultureInfo culture)
    {
        // Top over-scroll (iOS bounce) reports negative offsets: clamping BEFORE the delta keeps the
        // bounce excursion and its rebound from feeding the travel accumulator in either direction.
        var offset = Math.Max(0, ScrollValueMath.ValueOrDefault(values, 0, 0.0));
        var dark = ScrollValueMath.ValueOrDefault(values, 1, AppTheme.Light) == AppTheme.Dark;

        if (!_tracking)
        {
            _tracking = true;
            _lastOffset = offset;
        }

        var delta = offset - _lastOffset;
        _lastOffset = offset;
        var wasActivated = Activated;

        if (delta > 0)
        {
            _travel = _travel < 0 ? delta : _travel + delta;

            if (!Activated && _travel >= ActivateThreshold)
            {
                Activated = true;
            }
        }
        else if (delta < 0)
        {
            _travel = _travel > 0 ? delta : _travel + delta;

            if (Activated && -_travel >= (DeactivateThreshold ?? ActivateThreshold))
            {
                Activated = false;
            }
        }

        // Whatever the accumulated direction says, resting at (or above) the top means the default
        // chrome must be back — otherwise a fast fling to the top could leave the mode stuck active.
        if (offset <= DeactivateBelow)
        {
            Activated = false;
            _travel = 0;
        }

        if (Activated != wasActivated)
        {
            Animator?.AnimateTo(
                Activated ? 1 : 0,
                Activated ? ActivateDuration : DeactivateDuration ?? ActivateDuration,
                Easing
            );
        }

        var t = Animator?.Progress ?? (Activated ? 1 : 0);
        var deactivated = dark ? DeactivatedDark ?? DeactivatedLight : DeactivatedLight;
        var activated = dark ? ActivatedDark ?? ActivatedLight : ActivatedLight;

        return ScrollValueMath.Interpolate(Kind, deactivated, activated, t, _brushInterpolator);
    }

    public object?[]? ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// The time leg of a scroll-direction value: an observable 0..1 <see cref="Progress"/> driven by a
/// dispatcher timer. Each change re-fires the multi-binding it participates in, pulling the converter
/// again — that is how a binding-based value animates over time. An interrupted transition restarts
/// from the current progress with the duration scaled by the remaining distance, keeping the
/// perceived speed constant.
/// </summary>
internal sealed class ScrollDirectionAnimator : BindableObject
{
    // Declared BEFORE ProgressProperty: static initializers run in declaration order.
    private static readonly BindablePropertyKey _progressPropertyKey =
        BindableProperty.CreateReadOnly(nameof(Progress), typeof(double), typeof(ScrollDirectionAnimator), 0.0);

    public static readonly BindableProperty ProgressProperty = _progressPropertyKey.BindableProperty;

    /// <summary>Gets the current transition progress: 0 = deactivated endpoint, 1 = activated endpoint.</summary>
    public double Progress => (double)GetValue(ProgressProperty);

    private IDispatcherTimer? _timer;

    public void AnimateTo(double target, uint durationMs, Easing? easing)
    {
        _timer?.Stop();
        _timer = null;

        var start = Progress;
        var scaledDuration = durationMs * Math.Abs(target - start);

        // No dispatcher on this thread (unit tests / design time) degrades to an instant snap.
        if (scaledDuration < 1 || DispatcherProvider.Current.GetForCurrentThread() is not { } dispatcher)
        {
            SetValue(_progressPropertyKey, target);

            return;
        }

        var startTicks = Environment.TickCount64;
        var timer = dispatcher.CreateTimer();
        _timer = timer;
        timer.Interval = TimeSpan.FromMilliseconds(16);
        timer.IsRepeating = true;

        timer.Tick += (_, _) =>
        {
            var fraction = Math.Min(1, (Environment.TickCount64 - startTicks) / scaledDuration);
            var eased = easing?.Ease(fraction) ?? fraction;
            SetValue(_progressPropertyKey, start + ((target - start) * eased));

            if (fraction >= 1 && ReferenceEquals(_timer, timer))
            {
                timer.Stop();
                _timer = null;
            }
        };

        timer.Start();
    }
}
