using Foundation;

namespace Nalu;

/// <summary>
/// iOS manager: drives ActivityKit through the Swift bridge. The activity's dynamic
/// state is the serialized <see cref="LiveActivityContent"/> payload, decoded and
/// rendered by the app's widget extension (see the Nalu widget template).
/// </summary>
internal sealed class AppleLiveActivityManager : ILiveActivityManager
{
    private readonly List<AppleLiveActivity> _activities;

    /// <summary>Held so the Swift side's escaping callback is never collected.</summary>
    private readonly Action<NSString, NSString> _stateObserver;

    public AppleLiveActivityManager()
    {
        _activities = RehydrateActivities();
        _stateObserver = OnStateChanged;

        if (NaluLiveActivitiesBridge.IsSupported())
        {
            NaluLiveActivitiesBridge.ObserveActivityStates(_stateObserver);
        }
    }

    public LiveActivitySupport Support
        => NaluLiveActivitiesBridge.IsSupported() && NaluLiveActivitiesBridge.AreActivitiesEnabled()
            ? LiveActivitySupport.Full
            : LiveActivitySupport.Unavailable;

    public IReadOnlyList<ILiveActivity> Activities => _activities;

    /// <summary>
    /// iOS has no runtime prompt for Live Activities: the user controls them per app in
    /// Settings. This reflects that switch (and NSSupportsLiveActivities in Info.plist).
    /// </summary>
    public Task<bool> RequestPermissionAsync()
        => Task.FromResult(Support == LiveActivitySupport.Full);

    public async Task<ILiveActivity> StartAsync(string kind, LiveActivityContent content)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentNullException.ThrowIfNull(content);

        var snapshot = content.DeepClone();
        var payload = LiveActivityContentSerializer.Serialize(snapshot);

        if (Support == LiveActivitySupport.Unavailable)
        {
            var inert = new AppleLiveActivity(Guid.NewGuid().ToString("N"), kind, snapshot, payload, active: false);
            _activities.Add(inert);
            return inert;
        }

        var completion = new TaskCompletionSource<(string? Id, string? Error)>(TaskCreationOptions.RunContinuationsAsynchronously);
        NaluLiveActivitiesBridge.StartActivity(
            kind,
            payload,
            ToStaleEpochMs(snapshot.StaleAt),
            (id, error) => completion.TrySetResult(((string?)id, (string?)error))
        );

        var result = await completion.Task.ConfigureAwait(false);

        if (result.Id is null)
        {
            throw new InvalidOperationException($"Failed to start live activity '{kind}': {result.Error ?? "unknown error"}.");
        }

        var activity = new AppleLiveActivity(result.Id, kind, snapshot, payload, active: true);
        _activities.Add(activity);
        return activity;
    }

    /// <summary>
    /// Stale date for ActivityKit, with a date ALREADY IN THE PAST dropped (0 = "no stale date").
    /// </summary>
    /// <remarks>
    /// Handing ActivityKit a stale date that has already passed makes the activity arrive in
    /// <c>ActivityState.stale</c>, and a stale activity is NEVER PRESENTED — verified on device:
    /// SpringBoard emits no "Inserting supplementary item" for it, so nothing reaches the Lock
    /// Screen at all. There is no upside to passing one (it cannot even produce a stale
    /// treatment, because there is nothing on screen to treat), and the trap is easy to fall
    /// into: the documented appointment pattern sets StaleAt to the countdown end, which becomes
    /// a past instant the moment that end goes by. So an activity for a session that finished
    /// half an hour ago would silently never show up.
    /// </remarks>
    internal static double ToStaleEpochMs(DateTimeOffset? staleAt)
        => staleAt is { } instant && instant > DateTimeOffset.UtcNow
            ? instant.ToUnixTimeMilliseconds()
            : 0;

    /// <summary>
    /// ActivityKit already ignores updates to an activity the user removed, so nothing can
    /// resurrect here the way it can on Android — this exists so the handle STATE tells the
    /// truth (and the Dismissed event fires) instead of the app finding out only at the next
    /// cold start. Delivered on a Swift task; re-dispatched to the main thread so the event
    /// reaches app code exactly as it does on Android.
    /// </summary>
    private void OnStateChanged(NSString activityId, NSString state)
    {
        if (state?.ToString() != "dismissed" || activityId?.ToString() is not { } id)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            for (var i = 0; i < _activities.Count; i++)
            {
                if (_activities[i].Id == id)
                {
                    _activities[i].MarkDismissed();

                    return;
                }
            }
        });
    }

    private static List<AppleLiveActivity> RehydrateActivities()
    {
        var activities = new List<AppleLiveActivity>();

        if (!NaluLiveActivitiesBridge.IsSupported())
        {
            return activities;
        }

        var json = NaluLiveActivitiesBridge.ActivitiesJson();

        foreach (var info in LiveActivityRehydration.Parse(json))
        {
            if (info is not { Id: not null, Kind: not null, Payload: not null }
                || LiveActivityContentSerializer.Deserialize(info.Payload) is not { } content)
            {
                continue;
            }

            var activity = new AppleLiveActivity(info.Id, info.Kind, content, info.Payload, active: info.State != "ended");

            if (info.State == "stale")
            {
                activity.MarkStale();
            }
            else if (info.State == "dismissed")
            {
                // Briefly still listed by ActivityKit right after the user removed it.
                activity.MarkDismissed();
            }

            activities.Add(activity);
        }

        return activities;
    }
}
