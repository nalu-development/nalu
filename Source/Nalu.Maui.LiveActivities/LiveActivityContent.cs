namespace Nalu;

/// <summary>
/// The semantic content of a live activity: what it says, not what it looks like.
/// Each platform renders it natively — Android as a (promoted) ongoing notification,
/// iOS as a Live Activity whose widget shows the same facts in the analogous spots.
/// </summary>
/// <remarks>
/// Instances handed to the library are cloned on intake: mutating an instance after
/// passing it to <see cref="ILiveActivityManager.StartAsync"/> has no effect. After
/// creation, content changes only through the patch lambdas on <see cref="ILiveActivity"/>.
/// </remarks>
public sealed record LiveActivityContent : ILiveActivityContent
{
    /// <summary>The headline: what is happening ("Pizza on the way").</summary>
    public string? Title { get; set; }

    /// <summary>Secondary line under the title.</summary>
    public string? Subtitle { get; set; }

    /// <summary>
    /// Replaces <see cref="Subtitle"/> while a <see cref="LiveActivityTimerMode.CountDown"/>
    /// timer is past its end ("Running over"), and the overflow then renders as a plain
    /// count-up. When <c>null</c>, the overflow keeps ticking as a NEGATIVE duration
    /// (−0:35) instead. On iOS the swap happens system-side (pair it with
    /// <see cref="StaleAt"/> = the end instant so the boundary re-render fires); on
    /// Android it applies from the first post after the end (until then the countdown
    /// chronometer ticks natively into negatives).
    /// </summary>
    public string? SubtitleOverflow { get; set; }

    /// <summary>
    /// The one short value for the tiny always-visible surface: the Android status-bar chip
    /// and the iOS Dynamic Island compact view. Keep it under ~7 characters ("12 min", "3-2").
    /// </summary>
    public string? ChipText { get; set; }

    /// <summary>Icon for the chip / Dynamic Island minimal view (platform image/template name).</summary>
    public string? ChipIcon { get; set; }

    /// <summary>Accent color as a hex string ("#RRGGBB"), applied to progress/notification tint.</summary>
    public string? AccentColor { get; set; }

    /// <summary>Image identity: Android large icon / iOS leading image (platform asset name).</summary>
    public string? ImageName { get; set; }

    /// <summary>Progress along a journey; see <see cref="LiveActivityProgress"/>.</summary>
    public LiveActivityProgress? Progress { get; set; }

    /// <summary>
    /// A ticking clock rendered natively by the OS; see <see cref="LiveActivityTimer"/>.
    /// Updates are for state changes, not for time passing.
    /// </summary>
    public LiveActivityTimer? Timer { get; set; }

    /// <summary>Deep link opened when the activity is tapped.</summary>
    public string? DeepLink { get; set; }

    /// <summary>
    /// When the content should be considered outdated (iOS dims the activity; the state
    /// transitions to <see cref="LiveActivityState.Stale"/>). Reaching this instant does
    /// NOT end the activity.
    /// </summary>
    public DateTimeOffset? StaleAt { get; set; }

    /// <summary>Action buttons; each is a label + deep link (no in-process callbacks).</summary>
    public List<LiveActivityAction>? Actions { get; set; }

    /// <summary>
    /// Free-form payload forwarded verbatim to app-customized iOS widget UIs.
    /// Ignored by the default rendering on both platforms.
    /// </summary>
    public Dictionary<string, string>? Custom { get; set; }

    ILiveActivityProgress? ILiveActivityContent.Progress => Progress;
    ILiveActivityTimer? ILiveActivityContent.Timer => Timer;
    IReadOnlyList<ILiveActivityAction>? ILiveActivityContent.Actions => Actions;
    IReadOnlyDictionary<string, string>? ILiveActivityContent.Custom => Custom;

    /// <summary>
    /// Deep clone: drafts handed to patch lambdas must not alias the applied snapshot,
    /// otherwise mutating a nested object would silently rewrite history.
    /// </summary>
    internal LiveActivityContent DeepClone() => this with
    {
        Progress = Progress?.DeepClone(),
        Timer = Timer is null ? null : Timer with { },
        Actions = Actions?.Select(static a => a with { }).ToList(),
        Custom = Custom is null ? null : new Dictionary<string, string>(Custom)
    };
}

/// <summary>
/// Progress along a journey. Adopts the richer platform shape (Android 16 ProgressStyle):
/// segments and milestone points degrade gracefully to a plain bar elsewhere.
/// </summary>
public sealed record LiveActivityProgress : ILiveActivityProgress
{
    /// <summary>Progress in the 0..1 range; ignored when <see cref="Indeterminate"/> is set.</summary>
    public double? Value { get; set; }

    /// <summary>Show an indeterminate bar (Android; iOS renders an empty gauge).</summary>
    public bool Indeterminate { get; set; }

    /// <summary>Icon travelling along the bar (Android 16 tracker icon; ignored on iOS default UI).</summary>
    public string? TrackerIcon { get; set; }

    /// <summary>Weighted, optionally colored stretches of the bar (Android 16; merged to a plain bar elsewhere).</summary>
    public List<LiveActivityProgressSegment>? Segments { get; set; }

    /// <summary>Milestone markers on the bar (Android 16; ignored on the iOS default UI).</summary>
    public List<LiveActivityProgressPoint>? Points { get; set; }

    IReadOnlyList<ILiveActivityProgressSegment>? ILiveActivityProgress.Segments => Segments;
    IReadOnlyList<ILiveActivityProgressPoint>? ILiveActivityProgress.Points => Points;

    internal LiveActivityProgress DeepClone() => this with
    {
        Segments = Segments?.Select(static s => s with { }).ToList(),
        Points = Points?.Select(static p => p with { }).ToList()
    };
}

/// <summary>A weighted stretch of the progress bar.</summary>
public sealed record LiveActivityProgressSegment : ILiveActivityProgressSegment
{
    /// <summary>Relative weight of this segment (segments are normalized to the full bar).</summary>
    public double Weight { get; set; } = 1;

    /// <summary>Segment color as a hex string ("#RRGGBB"); defaults to the accent color.</summary>
    public string? Color { get; set; }
}

/// <summary>A milestone marker on the progress bar.</summary>
public sealed record LiveActivityProgressPoint : ILiveActivityProgressPoint
{
    /// <summary>Position of the marker in the 0..1 range.</summary>
    public double Position { get; set; }

    /// <summary>Optional icon shown at the marker.</summary>
    public string? Icon { get; set; }
}

/// <summary>How a <see cref="LiveActivityTimer"/> ticks.</summary>
public enum LiveActivityTimerMode
{
    /// <summary>Counts down towards <see cref="LiveActivityTimer.EndsAt"/>.</summary>
    CountDown,

    /// <summary>Counts up from <see cref="LiveActivityTimer.StartsAt"/>.</summary>
    CountUp,

    /// <summary>Frozen at <see cref="LiveActivityTimer.PausedElapsed"/>.</summary>
    Paused
}

/// <summary>
/// A ticking clock anchored to absolute instants, so the OS renders time natively
/// (iOS <c>Text(timerInterval:)</c>, Android chronometer) with zero updates while it runs.
/// </summary>
public sealed record LiveActivityTimer : ILiveActivityTimer
{
    /// <inheritdoc cref="LiveActivityTimerMode"/>
    public LiveActivityTimerMode Mode { get; set; }

    /// <summary>Anchor instant for <see cref="LiveActivityTimerMode.CountUp"/> (and optionally the range start for count-down).</summary>
    public DateTimeOffset? StartsAt { get; set; }

    /// <summary>Target instant for <see cref="LiveActivityTimerMode.CountDown"/>.</summary>
    public DateTimeOffset? EndsAt { get; set; }

    /// <summary>Elapsed time displayed while <see cref="LiveActivityTimerMode.Paused"/>.</summary>
    public TimeSpan? PausedElapsed { get; set; }

    /// <summary>Ticks down towards <paramref name="endsAt"/>.</summary>
    public static LiveActivityTimer CountDown(DateTimeOffset endsAt, DateTimeOffset? startedAt = null)
        => new() { Mode = LiveActivityTimerMode.CountDown, EndsAt = endsAt, StartsAt = startedAt };

    /// <summary>Ticks up from <paramref name="startedAt"/>.</summary>
    public static LiveActivityTimer CountUp(DateTimeOffset startedAt)
        => new() { Mode = LiveActivityTimerMode.CountUp, StartsAt = startedAt };

    /// <summary>Frozen display of <paramref name="elapsed"/>; resume by replacing with a recomputed anchor.</summary>
    public static LiveActivityTimer Paused(TimeSpan elapsed)
        => new() { Mode = LiveActivityTimerMode.Paused, PausedElapsed = elapsed };
}

/// <summary>An action button on the live activity.</summary>
/// <remarks>
/// v1 renders only actions carrying a <see cref="DeepLink"/> (tapping opens the app at
/// that link). Actions without one are reserved for the upcoming direct-callback support
/// — identified by <see cref="Id"/> — and are not rendered yet.
/// </remarks>
public sealed record LiveActivityAction : ILiveActivityAction
{
    /// <summary>
    /// Stable identity of the action, reported back to the app when direct action
    /// callbacks land; defaults to <see cref="Label"/> when omitted.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>Button label.</summary>
    public required string Label { get; set; }

    /// <summary>Optional icon (Android drawable name / iOS SF Symbol name).</summary>
    public string? Icon { get; set; }

    /// <summary>Deep link opened when the button is tapped; see the remarks for omission.</summary>
    public string? DeepLink { get; set; }
}

/// <summary>
/// Alerting configuration for an update: draws the user's attention
/// (iOS Live Activity alert, Android heads-up re-notify) instead of updating silently.
/// </summary>
/// <param name="Title">Short alert title.</param>
/// <param name="Body">Alert body.</param>
public sealed record LiveActivityAlert(string Title, string? Body = null);
