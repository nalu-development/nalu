namespace Nalu.SharpState;

/// <summary>
/// Runtime dispatcher used by every generated actor: holds the current leaf state and the caller's context,
/// walks the hierarchy to resolve transitions, commits state changes, and schedules any post-transition reactions.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TTrigger">Enum type listing all triggers of the machine.</typeparam>
/// <typeparam name="TActor">Type of the actor passed into post-transition reactions.</typeparam>
public sealed class StateMachineEngine<TContext, TState, TTrigger, TActor>
    where TContext : class
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly object _gate = new();
    private readonly StateMachineDefinition<TContext, TState, TTrigger, TActor> _definition;
    private readonly TActor _actor;
    private TState _currentState;
    private bool _isDispatching;

    /// <summary>
    /// Initializes a new <see cref="StateMachineEngine{TContext, TState, TTrigger, TActor}"/>.
    /// If <paramref name="currentState"/> is a composite, its initial child chain is resolved to a leaf before the engine settles.
    /// </summary>
    /// <param name="definition">The immutable definition to dispatch against.</param>
    /// <param name="currentState">The initial state. Composites are resolved to their initial leaf.</param>
    /// <param name="context">The context carried through every transition.</param>
    /// <param name="actor">The actor instance to pass to post-transition reactions.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="context"/> is <c>null</c>.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="currentState"/> is not registered in the definition.</exception>
    public StateMachineEngine(
        StateMachineDefinition<TContext, TState, TTrigger, TActor> definition,
        TState currentState,
        TContext context,
        TActor actor)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Context = context ?? throw new ArgumentNullException(nameof(context));
        _actor = actor;

        if (!_definition.TryGetConfiguration(currentState, out _))
        {
            throw new KeyNotFoundException($"State '{currentState}' is not registered in the state machine definition.");
        }

        _currentState = _definition.LeafOf(currentState);
    }

    /// <summary>
    /// The current leaf state.
    /// </summary>
    public TState CurrentState => _currentState;

    /// <summary>
    /// The user-supplied context passed to every guard and action.
    /// </summary>
    public TContext Context { get; }

    /// <summary>
    /// Raised after a transition has committed. Parameters are the source leaf, the new leaf, and the trigger that caused the change.
    /// Not raised for internal transitions (<see cref="Transition{TContext, TState, TActor}.IsInternal"/>) or unhandled triggers.
    /// </summary>
    public event StateChangedHandler<TState, TTrigger>? StateChanged;

    /// <summary>
    /// Raised when a background <c>ReactAsync(...)</c> callback fails after the transition already completed.
    /// </summary>
    public event ReactionFailedHandler<TState, TTrigger>? ReactionFailed;

    /// <summary>
    /// Callback invoked when a trigger fires but no transition matches (neither on the current leaf
    /// nor on any of its ancestors, or all guards returned <c>false</c>). Handlers receive the current
    /// leaf state, the trigger itself, and the arguments originally passed to it.
    /// Defaults to a handler that throws <see cref="NotSupportedException"/>.
    /// Set to <c>null</c> to silently ignore unhandled triggers, or assign a custom handler.
    /// </summary>
    public UnhandledTriggerHandler<TState, TTrigger>? OnUnhandled { get; set; } = DefaultUnhandled;

    private static void DefaultUnhandled(TState currentState, TTrigger trigger, object?[] args)
        => throw new NotSupportedException(
            $"Trigger '{trigger}' is not handled from state '{currentState}'.");

    /// <summary>
    /// Determines whether the current leaf equals <paramref name="state"/> or any of its ancestors equals <paramref name="state"/>.
    /// </summary>
    /// <param name="state">The state to test against. May be a leaf or a composite.</param>
    /// <returns><c>true</c> if the machine is currently in <paramref name="state"/> or any of its descendants.</returns>
    public bool IsIn(TState state) => _definition.IsSelfOrDescendantOf(_currentState, state);

    /// <summary>
    /// Fires a trigger synchronously. The first matching transition wins.
    /// External transitions run exit actions, transition action, state commit, entry actions, and <see cref="StateChanged"/>
    /// before any configured <c>ReactAsync(...)</c> callback is scheduled in the captured synchronization context.
    /// </summary>
    public void Fire(TTrigger trigger, TriggerArgs args)
    {
        lock (_gate)
        {
            if (_isDispatching)
            {
                throw new InvalidOperationException(
                    $"Trigger '{trigger}' cannot be fired while another trigger is still being processed. Use ReactAsync(...) for post-transition work instead.");
            }

            _isDispatching = true;
            try
            {
                var synchronizationContext = System.Threading.SynchronizationContext.Current;
                var match = FindMatchingTransition(trigger, args);
                if (match is null)
                {
                    OnUnhandled?.Invoke(_currentState, trigger, args.ToArray());
                    return;
                }

                var transition = match.Value.Transition;
                CommitTransition(trigger, match.Value.Source, transition, args, synchronizationContext);
            }
            finally
            {
                _isDispatching = false;
            }
        }
    }

    /// <summary>
    /// Determines whether the specified trigger currently has a matching transition.
    /// Guards are evaluated against the current state and supplied arguments, but no actions run and no state changes are committed.
    /// </summary>
    public bool CanFire(TTrigger trigger, TriggerArgs args)
    {
        lock (_gate)
        {
            return FindMatchingTransition(trigger, args).HasValue;
        }
    }

    private (Transition<TContext, TState, TActor> Transition, TState Source)? FindMatchingTransition(TTrigger trigger, TriggerArgs args)
    {
        var source = _currentState;
        var state = source;
        while (true)
        {
            if (_definition.TryGetConfiguration(state, out var config)
                && config.TryGetTransitions(trigger, out var transitions))
            {
                foreach (var transition in transitions)
                {
                    if (transition.Guard is null || transition.Guard(Context, args))
                    {
                        return (transition, source);
                    }
                }
            }

            if (!_definition.Parent.TryGetValue(state, out var parent))
            {
                return null;
            }

            state = parent;
        }
    }

    private void CommitTransition(
        TTrigger trigger,
        TState source,
        Transition<TContext, TState, TActor> transition,
        TriggerArgs args,
        System.Threading.SynchronizationContext? synchronizationContext)
    {
        if (transition.TargetSelector is not null)
        {
            var resolvedTarget = transition.TargetSelector(Context, args);
            var resolvedLeaf = _definition.LeafOf(resolvedTarget);
            if (EqualityComparer<TState>.Default.Equals(resolvedLeaf, source))
            {
                transition.SyncAction?.Invoke(Context, args);
                ScheduleReaction(source, source, trigger, transition, args, synchronizationContext);
                return;
            }

            InvokeExitActions(source, resolvedLeaf);
            transition.SyncAction?.Invoke(Context, args);
            _currentState = resolvedLeaf;
            InvokeEntryActions(source, resolvedLeaf);
            StateChanged?.Invoke(source, resolvedLeaf, trigger, args.ToArray());
            ScheduleReaction(source, resolvedLeaf, trigger, transition, args, synchronizationContext);
            return;
        }

        if (transition.IsInternal)
        {
            transition.SyncAction?.Invoke(Context, args);
            ScheduleReaction(source, source, trigger, transition, args, synchronizationContext);
            return;
        }

        var newLeaf = _definition.LeafOf(transition.Target);
        InvokeExitActions(source, newLeaf);
        transition.SyncAction?.Invoke(Context, args);
        _currentState = newLeaf;
        InvokeEntryActions(source, newLeaf);
        StateChanged?.Invoke(source, newLeaf, trigger, args.ToArray());
        ScheduleReaction(source, newLeaf, trigger, transition, args, synchronizationContext);
    }

    private void InvokeExitActions(TState source, TState destination)
    {
        foreach (var state in EnumerateExitPath(source, destination))
        {
            var config = _definition.GetConfiguration(state);
            config.ExitAction?.Invoke(Context);
        }
    }

    private void InvokeEntryActions(TState source, TState destination)
    {
        foreach (var state in EnumerateEntryPath(source, destination))
        {
            var config = _definition.GetConfiguration(state);
            config.EntryAction?.Invoke(Context);
        }
    }

    private void ScheduleReaction(
        TState source,
        TState destination,
        TTrigger trigger,
        Transition<TContext, TState, TActor> transition,
        TriggerArgs args,
        System.Threading.SynchronizationContext? synchronizationContext)
    {
        if (transition.ReactionAsync is null)
        {
            return;
        }

        var workItem = new ReactionWorkItem(this, transition.ReactionAsync, source, destination, trigger, args);
        if (synchronizationContext is null)
        {
            _ = Task.Run(workItem.Start);
            return;
        }

#pragma warning disable VSTHRD001
        synchronizationContext.Post(static state => ((ReactionWorkItem)state!).Start(), workItem);
#pragma warning restore VSTHRD001
    }

#pragma warning disable VSTHRD100
    private async void ExecuteReaction(
#pragma warning restore VSTHRD100
        Func<TActor, TContext, TriggerArgs, ValueTask> reactionAsync,
        TState source,
        TState destination,
        TTrigger trigger,
        TriggerArgs args)
    {
        try
        {
            await reactionAsync(_actor, Context, args);
        }
        catch (Exception exception)
        {
            try
            {
                ReactionFailed?.Invoke(source, destination, trigger, args.ToArray(), exception);
            }
            catch
            {
                // Ignore failures in failure-reporting subscribers to avoid turning fire-and-forget work into process-wide faults.
            }
        }
    }

    private IEnumerable<TState> EnumerateExitPath(TState source, TState destination)
    {
        var lca = _definition.LowestCommonAncestor(source, destination);
        var current = source;
        while (!lca.HasValue || !EqualityComparer<TState>.Default.Equals(current, lca.Value))
        {
            yield return current;

            if (!_definition.Parent.TryGetValue(current, out var parent))
            {
                yield break;
            }

            current = parent;
        }
    }

    private IEnumerable<TState> EnumerateEntryPath(TState source, TState destination)
    {
        var lca = _definition.LowestCommonAncestor(source, destination);
        var stack = new Stack<TState>();
        var current = destination;

        while (!lca.HasValue || !EqualityComparer<TState>.Default.Equals(current, lca.Value))
        {
            stack.Push(current);

            if (!_definition.Parent.TryGetValue(current, out var parent))
            {
                break;
            }

            current = parent;
        }

        while (stack.Count > 0)
        {
            yield return stack.Pop();
        }
    }

    private sealed class ReactionWorkItem
    {
        private readonly StateMachineEngine<TContext, TState, TTrigger, TActor> _engine;
        private readonly Func<TActor, TContext, TriggerArgs, ValueTask> _reactionAsync;
        private readonly TState _source;
        private readonly TState _destination;
        private readonly TTrigger _trigger;
        private readonly TriggerArgs _args;

        public ReactionWorkItem(
            StateMachineEngine<TContext, TState, TTrigger, TActor> engine,
            Func<TActor, TContext, TriggerArgs, ValueTask> reactionAsync,
            TState source,
            TState destination,
            TTrigger trigger,
            TriggerArgs args)
        {
            _engine = engine;
            _reactionAsync = reactionAsync;
            _source = source;
            _destination = destination;
            _trigger = trigger;
            _args = args;
        }

        public void Start() => _engine.ExecuteReaction(_reactionAsync, _source, _destination, _trigger, _args);
    }
}
