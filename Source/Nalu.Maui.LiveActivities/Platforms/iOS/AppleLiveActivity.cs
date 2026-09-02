namespace Nalu;

/// <summary>
/// iOS live activity handle: pushes each applied snapshot to ActivityKit through the
/// Swift bridge. An inactive handle (started while unavailable) applies nothing.
/// </summary>
internal sealed class AppleLiveActivity : LiveActivityBase
{
    private readonly bool _active;

    public AppleLiveActivity(string id, string kind, LiveActivityContent content, string payload, bool active)
        : base(id, kind, content, payload)
    {
        _active = active;

        if (!active)
        {
            State = LiveActivityState.Ended;
        }
    }

    protected override Task ApplyUpdateAsync(LiveActivityContent content, string payload, LiveActivityAlert? alert)
    {
        if (!_active)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NaluLiveActivitiesBridge.UpdateActivity(
            Id,
            payload,
            AppleLiveActivityManager.ToStaleEpochMs(content.StaleAt),
            alert?.Title,
            alert?.Body,
            () => completion.TrySetResult()
        );

        return completion.Task;
    }

    protected override Task ApplyEndAsync(LiveActivityContent content, string payload, LiveActivityDismissal dismissal)
    {
        if (!_active)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        NaluLiveActivitiesBridge.EndActivity(
            Id,
            payload,
            dismissal == LiveActivityDismissal.Immediate,
            () => completion.TrySetResult()
        );

        return completion.Task;
    }
}
