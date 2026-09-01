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

    /// <summary>
    /// Latched by <see cref="MarkDismissed"/> and never cleared. The gates below read THIS
    /// rather than <see cref="State"/>: the dismissal arrives off-lock, so a State check
    /// racing an in-flight update could otherwise be overwritten back to Active and lose the
    /// dismissal for good.
    /// </summary>
    private volatile bool _dismissed;

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

    public event EventHandler? Dismissed;

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

            // The user swiped it away: never push again. Android would happily re-post the
            // notification (NotificationManager.notify resurrects a dismissed one) and both
            // platforms ask apps not to — reposting is what makes users revoke the
            // permission outright. The snapshot still advances so Content stays truthful
            // and a later EndAsync carries the final state.
            if (_dismissed)
            {
                _content = draft;
                _payload = payload;

                return;
            }

            // An alert must fire even when the content is unchanged.
            if (payload == _payload && alert is null)
            {
                return;
            }

            await ApplyUpdateAsync(draft, payload, alert).ConfigureAwait(false);
            _content = draft;
            _payload = payload;

            // A dismissal landing while the update was in flight wins: it is terminal.
            // Re-derived from the latch rather than assigned blindly, so the state converges
            // even when the flag flips during this very block.
            State = _dismissed ? LiveActivityState.Dismissed : LiveActivityState.Active;
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

            // Nothing is on screen to end — and the Default dismissal would POST the final
            // content, resurrecting exactly what the user swiped away.
            if (!_dismissed)
            {
                await ApplyEndAsync(draft, payload, dismissal).ConfigureAwait(false);
            }

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

    /// <summary>
    /// The user removed the activity from screen. Terminal and idempotent, and deliberately
    /// NOT taken under the handle lock: it arrives on a platform callback thread while an
    /// update may be in flight, and <see cref="UpdateAsync"/> re-checks the state after its
    /// await for exactly that race.
    /// </summary>
    internal void MarkDismissed()
    {
        if (_dismissed || State == LiveActivityState.Ended)
        {
            return;
        }

        _dismissed = true;
        State = LiveActivityState.Dismissed;
        Dismissed?.Invoke(this, EventArgs.Empty);
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
