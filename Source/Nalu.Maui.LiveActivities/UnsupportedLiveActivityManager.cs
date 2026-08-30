namespace Nalu;

/// <summary>
/// No-op manager for surfaces without live activities (plain .NET, Windows, Mac Catalyst,
/// iOS &lt; 16.2, and Android when the fallback is disabled). Start succeeds and returns an
/// inert handle so app code needs no platform branches.
/// </summary>
internal sealed class UnsupportedLiveActivityManager : ILiveActivityManager
{
    private readonly List<ILiveActivity> _activities = [];

    public LiveActivitySupport Support => LiveActivitySupport.Unavailable;

    public IReadOnlyList<ILiveActivity> Activities => _activities;

    public Task<bool> RequestPermissionAsync() => Task.FromResult(false);

    public Task<ILiveActivity> StartAsync(string kind, LiveActivityContent content)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);
        ArgumentNullException.ThrowIfNull(content);

        var activity = new NoopLiveActivity(Guid.NewGuid().ToString("N"), kind, content.DeepClone());
        _activities.Add(activity);
        return Task.FromResult<ILiveActivity>(activity);
    }

    private sealed class NoopLiveActivity(string id, string kind, LiveActivityContent content)
        : LiveActivityBase(id, kind, content)
    {
        protected override Task ApplyUpdateAsync(LiveActivityContent content, string payload, LiveActivityAlert? alert)
            => Task.CompletedTask;

        protected override Task ApplyEndAsync(LiveActivityContent content, string payload, LiveActivityDismissal dismissal)
            => Task.CompletedTask;
    }
}
