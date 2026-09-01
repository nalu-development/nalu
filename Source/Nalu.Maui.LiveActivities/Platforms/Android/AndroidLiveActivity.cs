using System.Globalization;
using Application = Android.App.Application;

namespace Nalu;

/// <summary>
/// Android live activity handle: each applied snapshot is flattened into ONE typed call
/// into the native Java layer ((re)posting over the same notification tag/id). The
/// serialized payload travels along only as the opaque rehydration snapshot — nothing
/// is parsed on the Java side.
/// </summary>
internal sealed class AndroidLiveActivity : LiveActivityBase
{
    private readonly AndroidLiveActivityManager _manager;
    private readonly int _notificationId;

    public AndroidLiveActivity(AndroidLiveActivityManager manager, string id, string kind, LiveActivityContent content, string? payload = null)
        : base(id, kind, content, payload)
    {
        _manager = manager;
        _notificationId = ComputeNotificationId(id);
    }

    protected override Task ApplyUpdateAsync(LiveActivityContent content, string payload, LiveActivityAlert? alert)
    {
        if (_manager.Support != LiveActivitySupport.Unavailable)
        {
            Post(content, payload, alert, promoted: true, ongoing: true);
        }

        return Task.CompletedTask;
    }

    protected override Task ApplyEndAsync(LiveActivityContent content, string payload, LiveActivityDismissal dismissal)
    {
        if (_manager.Support == LiveActivitySupport.Unavailable)
        {
            return Task.CompletedTask;
        }

        if (dismissal == LiveActivityDismissal.Immediate)
        {
            Platform.NaluLiveUpdates.Cancel(Application.Context, AndroidLiveActivityManager.NotificationTag, _notificationId);
        }
        else
        {
            // Mirror the iOS default-dismissal semantics: leave the final content visible
            // as a regular, swipeable notification (no chip, not ongoing).
            Post(content, payload, alert: null, promoted: false, ongoing: false);
        }

        return Task.CompletedTask;
    }

    internal void Post(LiveActivityAlert? alert, bool promoted, bool ongoing)
        => Post(Snapshot, Payload, alert, promoted, ongoing);

    private void Post(LiveActivityContent content, string payload, LiveActivityAlert? alert, bool promoted, bool ongoing)
    {
        var progress = content.Progress;
        var progressMode = progress switch
        {
            null => Platform.NaluLiveUpdates.ProgressNone,
            { Indeterminate: true } => Platform.NaluLiveUpdates.ProgressIndeterminate,
            _ => Platform.NaluLiveUpdates.ProgressValue
        };

        var subtitle = content.Subtitle;

        var (timerMode, timerAnchorMs, pausedElapsedMs) = content.Timer switch
        {
            { Mode: LiveActivityTimerMode.CountDown, EndsAt: { } endsAt } => (Platform.NaluLiveUpdates.TimerCountDown, endsAt.ToUnixTimeMilliseconds(), 0L),
            { Mode: LiveActivityTimerMode.CountUp, StartsAt: { } startsAt } => (Platform.NaluLiveUpdates.TimerCountUp, startsAt.ToUnixTimeMilliseconds(), 0L),
            { Mode: LiveActivityTimerMode.Paused, PausedElapsed: { } elapsed } => (Platform.NaluLiveUpdates.TimerPaused, 0L, (long)elapsed.TotalMilliseconds),
            _ => (Platform.NaluLiveUpdates.TimerNone, 0L, 0L)
        };

        // A countdown already past its end with overflow wording renders as that wording
        // plus a plain count-up from the end — mirroring the iOS widget. Without the
        // wording the countdown chronometer keeps ticking natively into negatives.
        // Android has no boundary re-render, so this applies from the first post after
        // the end instant.
        if (content is { SubtitleOverflow: { } subtitleOverflow, Timer: { Mode: LiveActivityTimerMode.CountDown, EndsAt: { } end } } && end <= DateTimeOffset.UtcNow)
        {
            subtitle = subtitleOverflow;
            timerMode = Platform.NaluLiveUpdates.TimerCountUp;
            timerAnchorMs = end.ToUnixTimeMilliseconds();
        }

        var segments = progress?.Segments;
        var points = progress?.Points;

        // v1 renders only link-backed actions; id-only actions are reserved for the
        // upcoming direct-callback support.
        var actions = content.Actions?.Where(static a => a.DeepLink is not null).ToList();

        Platform.NaluLiveUpdates.Post(
            Application.Context,
            AndroidLiveActivityManager.NotificationTag,
            _notificationId,
            Id,
            Kind,
            _manager.GetChannelId(Kind),
            _manager.Options.GetKindDisplayName(Kind),
            _manager.Options.AndroidSmallIcon,
            content.Title,
            subtitle,
            content.ChipText,
            ParseColor(content.AccentColor),
            content.ImageName,
            progressMode,
            progress?.Value ?? 0,
            segments is { Count: > 0 } ? segments.Select(static s => s.Weight).ToArray() : null,
            segments is { Count: > 0 } ? segments.Select(static s => ParseColor(s.Color)).ToArray() : null,
            points is { Count: > 0 } ? points.Select(static p => p.Position).ToArray() : null,
            progress?.TrackerIcon,
            timerMode,
            timerAnchorMs,
            pausedElapsedMs,
            content.DeepLink,
            actions is { Count: > 0 } ? actions.Select(static a => a.Label).ToArray() : null,
            actions is { Count: > 0 } ? actions.Select(static a => a.DeepLink!).ToArray() : null,
            actions is { Count: > 0 } ? actions.Select(static a => a.Icon ?? string.Empty).ToArray() : null,
            promoted,
            ongoing,
            alert?.Title,
            payload
        );
    }

    /// <summary>ARGB int from "#RRGGBB"; 0 means "not set" (real colors always carry FF alpha).</summary>
    private static int ParseColor(string? hex)
    {
        if (hex is null || !hex.StartsWith('#') || hex.Length != 7 || !int.TryParse(hex.AsSpan(1), NumberStyles.HexNumber, null, out var rgb))
        {
            return 0;
        }

        return unchecked((int)0xFF000000 | rgb);
    }

    /// <summary>Deterministic notification id from the activity id (stable across restarts).</summary>
    private static int ComputeNotificationId(string id)
    {
        var hash = 17;

        foreach (var c in id)
        {
            hash = unchecked((hash * 31) + c);
        }

        return hash;
    }
}
