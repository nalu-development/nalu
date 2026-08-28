using Android.App;
using Android.Content;
using Android.OS;
using Application = Android.App.Application;

[assembly: UsesPermission("android.permission.POST_NOTIFICATIONS")]
[assembly: UsesPermission("android.permission.POST_PROMOTED_NOTIFICATIONS")]

namespace Nalu;

/// <summary>
/// Android manager: renders live activities as (promoted) ongoing notifications.
/// On Android 16+ (with net10 bindings) the notification carries ProgressStyle, the
/// status-bar chip text and the promoted-ongoing flag; older versions get a plain
/// ongoing notification with a classic progress bar and chronometer.
/// </summary>
internal sealed class AndroidLiveActivityManager : ILiveActivityManager
{
    internal const string NotificationTag = "nalu.live";
    internal const string ExtraKind = "nalu.live.kind";
    internal const string ExtraId = "nalu.live.id";
    internal const string ExtraPayload = "nalu.live.payload";

    private readonly LiveActivityOptions _options;
    private readonly List<AndroidLiveActivity> _activities;

    public AndroidLiveActivityManager(LiveActivityOptions options)
    {
        _options = options;
        _activities = RehydrateActivities();
    }

    public LiveActivitySupport Support
    {
        get
        {
            // API 26 (notification channels) is the effective floor of the feature.
            if (!OperatingSystem.IsAndroidVersionAtLeast(26) || GetNotificationManager() is not { } manager || !manager.AreNotificationsEnabled())
            {
                return LiveActivitySupport.Unavailable;
            }

#if NET10_0_OR_GREATER
            if (OperatingSystem.IsAndroidVersionAtLeast(36))
            {
                return LiveActivitySupport.Full;
            }
#endif

            return _options.DisableAndroidFallback ? LiveActivitySupport.Unavailable : LiveActivitySupport.Degraded;
        }
    }

    public IReadOnlyList<ILiveActivity> Activities => _activities;

    public async Task<bool> RequestPermissionAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.PostNotifications>().ConfigureAwait(false);
        return status == PermissionStatus.Granted && Support != LiveActivitySupport.Unavailable;
    }

    public Task<ILiveActivity> StartAsync(string kind, LiveActivityContent content)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentNullException.ThrowIfNull(content);

        var activity = new AndroidLiveActivity(this, Guid.NewGuid().ToString("N"), kind, content.DeepClone());
        _activities.Add(activity);

        if (Support == LiveActivitySupport.Unavailable)
        {
            return Task.FromResult<ILiveActivity>(activity);
        }

        EnsureChannel(kind);
        activity.Post(alert: null, promoted: true, ongoing: true);
        return Task.FromResult<ILiveActivity>(activity);
    }

    internal LiveActivityOptions Options => _options;

    internal static NotificationManager? GetNotificationManager()
        => Application.Context.GetSystemService(Context.NotificationService) as NotificationManager;

    internal string GetChannelId(string kind) => $"nalu_live_{kind}";

    private void EnsureChannel(string kind)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var manager = GetNotificationManager();

        if (manager is null)
        {
            return;
        }

        var channelId = GetChannelId(kind);

        if (manager.GetNotificationChannel(channelId) is null)
        {
            var channel = new NotificationChannel(channelId, _options.GetKindDisplayName(kind), NotificationImportance.Default);
            manager.CreateNotificationChannel(channel);
        }
    }

    private List<AndroidLiveActivity> RehydrateActivities()
    {
        var activities = new List<AndroidLiveActivity>();

        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return activities;
        }

        var manager = GetNotificationManager();

        if (manager is null)
        {
            return activities;
        }

        foreach (var statusBarNotification in manager.GetActiveNotifications() ?? [])
        {
            if (statusBarNotification.Tag != NotificationTag)
            {
                continue;
            }

            var extras = statusBarNotification.Notification?.Extras;
            var id = extras?.GetString(ExtraId);
            var kind = extras?.GetString(ExtraKind);
            var payload = extras?.GetString(ExtraPayload);

            if (id is null || kind is null || payload is null || LiveActivityContentSerializer.Deserialize(payload) is not { } content)
            {
                continue;
            }

            var activity = new AndroidLiveActivity(this, id, kind, content, payload);

            if (content.StaleAt is { } staleAt && staleAt <= DateTimeOffset.UtcNow)
            {
                activity.MarkStale();
            }

            activities.Add(activity);
        }

        return activities;
    }
}
