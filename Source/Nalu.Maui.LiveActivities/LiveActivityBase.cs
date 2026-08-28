namespace Nalu;

/// <summary>
/// Platform-agnostic live activity handle: owns the snapshot, the patch pipeline
/// (clone → patch → serialize → dedupe → apply) and the handle lock.
/// </summary>
internal abstract class LiveActivityBase : ILiveActivity
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private LiveActivityContent _content;
    private string _payload;

    protected LiveActivityBase(string id, string kind, LiveActivityContent content, string? payload = null)
    {
        Id = id;
        Kind = kind;
        _content = content;
        _payload = payload ?? LiveActivityContentSerializer.Serialize(content);
    }

    public string Id { get; }
    public string Kind { get; }
    public LiveActivityState State { get; protected set; }
    public ILiveActivityContent Content => _content;

    /// <summary>The serialized form of the current snapshot (the iOS widget/persistence payload).</summary>
    protected string Payload => _payload;

    protected LiveActivityContent Snapshot => _content;

    public async Task UpdateAsync(Action<LiveActivityContent> patch, LiveActivityAlert? alert = null)
    {
        ArgumentNullException.ThrowIfNull(patch);
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            ThrowIfEnded();

            var draft = _content.DeepClone();
            patch(draft);
            var payload = LiveActivityContentSerializer.Serialize(draft);

            // An alert must fire even when the content is unchanged.
            if (payload == _payload && alert is null)
            {
                return;
            }

            await ApplyUpdateAsync(draft, payload, alert).ConfigureAwait(false);
            _content = draft;
            _payload = payload;
            State = LiveActivityState.Active;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EndAsync(Action<LiveActivityContent>? finalPatch = null, LiveActivityDismissal dismissal = LiveActivityDismissal.Default)
    {
        await _gate.WaitAsync().ConfigureAwait(false);

        try
        {
            if (State == LiveActivityState.Ended)
            {
                return;
            }

            var draft = _content;
            var payload = _payload;

            if (finalPatch is not null)
            {
                draft = _content.DeepClone();
                finalPatch(draft);
                payload = LiveActivityContentSerializer.Serialize(draft);
            }

            await ApplyEndAsync(draft, payload, dismissal).ConfigureAwait(false);
            _content = draft;
            _payload = payload;
            State = LiveActivityState.Ended;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Marks the handle stale (past <see cref="LiveActivityContent.StaleAt"/>).</summary>
    internal void MarkStale()
    {
        if (State == LiveActivityState.Active)
        {
            State = LiveActivityState.Stale;
        }
    }

    /// <summary>Pushes the new snapshot to the platform surface.</summary>
    protected abstract Task ApplyUpdateAsync(LiveActivityContent content, string payload, LiveActivityAlert? alert);

    /// <summary>Ends the platform surface with the final snapshot.</summary>
    protected abstract Task ApplyEndAsync(LiveActivityContent content, string payload, LiveActivityDismissal dismissal);

    private void ThrowIfEnded()
    {
        if (State == LiveActivityState.Ended)
        {
            throw new InvalidOperationException($"Live activity '{Id}' has ended and can no longer be updated.");
        }
    }
}
