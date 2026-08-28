namespace Nalu;

/// <summary>
/// Android live activity handle: each applied snapshot is rendered by
/// <see cref="AndroidLiveActivityRenderer"/> and (re)posted over the same notification.
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
            AndroidLiveActivityManager.GetNotificationManager()?.Cancel(AndroidLiveActivityManager.NotificationTag, _notificationId);
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
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var notification = AndroidLiveActivityRenderer.Render(_manager, Id, Kind, content, payload, alert, promoted, ongoing);
        AndroidLiveActivityManager.GetNotificationManager()?.Notify(AndroidLiveActivityManager.NotificationTag, _notificationId, notification);
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
