using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nalu;

/// <summary>
/// iOS manager: drives ActivityKit through the Swift bridge. The activity's dynamic
/// state is the serialized <see cref="LiveActivityContent"/> payload, decoded and
/// rendered by the app's widget extension (see the Nalu widget template).
/// </summary>
internal sealed class AppleLiveActivityManager : ILiveActivityManager
{
    private readonly List<AppleLiveActivity> _activities;

    public AppleLiveActivityManager()
    {
        _activities = RehydrateActivities();
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
            ToEpochMs(snapshot.StaleAt),
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

    internal static double ToEpochMs(DateTimeOffset? instant) => instant?.ToUnixTimeMilliseconds() ?? 0;

    private static List<AppleLiveActivity> RehydrateActivities()
    {
        var activities = new List<AppleLiveActivity>();

        if (!NaluLiveActivitiesBridge.IsSupported())
        {
            return activities;
        }

        var json = NaluLiveActivitiesBridge.ActivitiesJson();
        List<BridgeActivityInfo>? infos;

        try
        {
            infos = JsonSerializer.Deserialize(json, BridgeJsonContext.Default.ListBridgeActivityInfo);
        }
        catch (JsonException)
        {
            return activities;
        }

        foreach (var info in infos ?? [])
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

            activities.Add(activity);
        }

        return activities;
    }

    internal sealed record BridgeActivityInfo(string? Id, string? Kind, string? Payload, string? State);
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(List<AppleLiveActivityManager.BridgeActivityInfo>))]
internal sealed partial class BridgeJsonContext : JsonSerializerContext;
