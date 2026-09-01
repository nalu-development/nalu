using Android.App;
using Android.Content;
using Application = Android.App.Application;

[assembly: UsesPermission("android.permission.POST_NOTIFICATIONS")]
[assembly: UsesPermission("android.permission.POST_PROMOTED_NOTIFICATIONS")]

namespace Nalu;

/// <summary>
/// Android manager: renders live activities as (promoted) ongoing notifications through
/// the native Java layer (see AndroidNative — one JNI call per update). On Android 16+
/// the notification carries ProgressStyle, the status-bar chip text and the
/// promoted-ongoing request; Android 8–15 gets a plain ongoing notification with a classic
/// progress bar and chronometer. Below API 26 (notification channels) the feature is
/// unavailable.
/// </summary>
internal sealed class AndroidLiveActivityManager : ILiveActivityManager
{
    internal const string NotificationTag = "nalu.live";

    private readonly LiveActivityOptions _options;
    private readonly List<AndroidLiveActivity> _activities;

    public AndroidLiveActivityManager(LiveActivityOptions options)
    {
        _options = options;
        _activities = RehydrateActivities();
        RegisterDismissReceiver();
    }

    public LiveActivitySupport Support
    {
        get
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(26)
                || Application.Context.GetSystemService(Android.Content.Context.NotificationService) is not NotificationManager manager
                || !manager.AreNotificationsEnabled())
            {
                return LiveActivitySupport.Unavailable;
            }

            // The Live Update promotion API (status-bar chip + floating card) only exists
            // from Android 16 QPR1 (API 36.1): base Android 16 renders the same content
            // as a plain ongoing notification, which is exactly the Degraded contract.
            if (OperatingSystem.IsAndroidVersionAtLeast(36) && Platform.NaluLiveUpdates.SupportsPromotion())
            {
                return LiveActivitySupport.Full;
            }

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

        if (Support != LiveActivitySupport.Unavailable)
        {
            activity.Post(alert: null, promoted: true, ongoing: true);
        }

        return Task.FromResult<ILiveActivity>(activity);
    }

    internal LiveActivityOptions Options => _options;

    internal string GetChannelId(string kind) => $"nalu_live_{kind}";

    /// <summary>
    /// Routes a user dismissal to its handle, which then stops pushing updates. Unknown ids
    /// (a notification from a previous process, or an already-ended activity whose final
    /// content the user swiped) are ignored.
    /// </summary>
    private void OnDismissed(string? activityId)
    {
        if (activityId is null)
        {
            return;
        }

        // Indexed rather than foreach: StartAsync appends from app code, and this runs on
        // the main thread whenever the notification is swiped.
        for (var i = 0; i < _activities.Count; i++)
        {
            if (_activities[i].Id == activityId)
            {
                _activities[i].MarkDismissed();

                return;
            }
        }
    }

    /// <summary>
    /// Listens for the delete intent the native layer attaches to every posted activity.
    /// Registered at RUNTIME rather than declared in the manifest on purpose: the signal
    /// only matters while this process is alive. If the app is dead when the user swipes,
    /// the next start rehydrates from getActiveNotifications(), which no longer lists the
    /// dismissed notification — the right outcome, reached without waking the app.
    /// The receiver lives as long as the manager (an app-lifetime singleton), so it is
    /// never unregistered.
    /// </summary>
    private void RegisterDismissReceiver()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        var receiver = new DismissReceiver(OnDismissed);
        var filter = new IntentFilter(Platform.NaluLiveUpdates.ActionDismissed);

        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            // The broadcast is sent with this app's own identity, so NotExported is enough.
            Application.Context.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
        }
        else
        {
            Application.Context.RegisterReceiver(receiver, filter);
        }
    }

    private sealed class DismissReceiver(Action<string?> onDismissed) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
            => onDismissed(intent?.GetStringExtra(Platform.NaluLiveUpdates.ExtraId));
    }

    private List<AndroidLiveActivity> RehydrateActivities()
    {
        var activities = new List<AndroidLiveActivity>();

        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return activities;
        }

        var json = Platform.NaluLiveUpdates.GetActiveJson(Application.Context, NotificationTag);

        foreach (var info in LiveActivityRehydration.Parse(json))
        {
            if (info is not { Id: not null, Kind: not null, Payload: not null }
                || LiveActivityContentSerializer.Deserialize(info.Payload) is not { } content)
            {
                continue;
            }

            var activity = new AndroidLiveActivity(this, info.Id, info.Kind, content, info.Payload);

            if (content.StaleAt is { } staleAt && staleAt <= DateTimeOffset.UtcNow)
            {
                activity.MarkStale();
            }

            activities.Add(activity);
        }

        return activities;
    }
}
