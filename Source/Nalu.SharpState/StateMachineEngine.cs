namespace Nalu.SharpState;

/// <summary>
/// Runtime dispatcher used by every generated <c>Instance</c>: holds the current leaf state and the caller's context,
/// and implements <see cref="Fire"/>/<see cref="FireAsync"/> by walking the hierarchy, evaluating guards,
/// running the action, and committing the new leaf state.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TTrigger">Enum type listing all triggers of the machine.</typeparam>
public sealed class StateMachineEngine<TContext, TState, TTrigger>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly StateMachineDefinition<TContext, TState, TTrigger> _definition;
    private TState _currentState;

    /// <summary>
    /// Initializes a new <see cref="StateMachineEngine{TContext, TState, TTrigger}"/>.
    /// If <paramref name="currentState"/> is a composite, its initial child chain is resolved to a leaf before the engine settles.
    /// </summary>
    /// <param name="definition">The immutable definition to dispatch against.</param>
    /// <param name="currentState">The initial state. Composites are resolved to their initial leaf.</param>
    /// <param name="context">The context carried through every transition.</param>
    /// <exception cref="ArgumentNullException"><paramref name="definition"/> or <paramref name="context"/> is <c>null</c>.</exception>
    /// <exception cref="KeyNotFoundException"><paramref name="currentState"/> is not registered in the definition.</exception>
    public StateMachineEngine(
        StateMachineDefinition<TContext, TState, TTrigger> definition,
        TState currentState,
        TContext context)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Context = context ?? throw new ArgumentNullException(nameof(context));

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
    /// Not raised for internal transitions (<see cref="Transition{TContext, TState}.IsInternal"/>) or unhandled triggers.
    /// </summary>
    public event StateChangedHandler<TState, TTrigger>? StateChanged;

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
    /// Fires a synchronous trigger. Walks the ancestor chain of <see cref="CurrentState"/> looking for a configured
    /// transition whose guard (if any) returns <c>true</c>. The first match wins: its <see cref="Transition{TContext, TState}.SyncAction"/>
    /// runs and the state is updated (unless the transition is internal).
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    /// <param name="args">Arguments matching the original trigger method's parameter list.</param>
    public void Fire(TTrigger trigger, TriggerArgs args)
    {
        var match = FindMatchingTransition(trigger, args);
        if (match is null)
        {
            OnUnhandled?.Invoke(_currentState, trigger, args.ToArray());
            return;
        }

        var transition = match.Value.Transition;
        transition.SyncAction?.Invoke(Context, args);
        CommitTransition(trigger, match.Value.Source, transition, args);
    }

    /// <summary>
    /// Fires an asynchronous trigger. Semantics mirror <see cref="Fire"/> but awaits the
    /// <see cref="Transition{TContext, TState}.AsyncAction"/> before committing the state change.
    /// </summary>
    /// <param name="trigger">The trigger to fire.</param>
    /// <param name="args">Arguments matching the original trigger method's parameter list.</param>
    /// <returns>A task that completes after the action has run and the state has been committed.</returns>
    public async ValueTask FireAsync(TTrigger trigger, TriggerArgs args)
    {
        var match = FindMatchingTransition(trigger, args);
        if (match is null)
        {
            OnUnhandled?.Invoke(_currentState, trigger, args.ToArray());
            return;
        }

        var transition = match.Value.Transition;
        if (transition.AsyncAction is { } asyncAction)
        {
            await asyncAction(Context, args).ConfigureAwait(false);
        }
        else
        {
            transition.SyncAction?.Invoke(Context, args);
        }

        await CommitTransitionAsync(trigger, match.Value.Source, transition, args).ConfigureAwait(false);
    }

    private (Transition<TContext, TState> Transition, TState Source)? FindMatchingTransition(TTrigger trigger, TriggerArgs args)
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

    private void CommitTransition(TTrigger trigger, TState source, Transition<TContext, TState> transition, TriggerArgs args)
    {
        if (transition.IsInternal)
        {
            return;
        }

        var newLeaf = _definition.LeafOf(transition.Target);
        InvokeExitActions(source, newLeaf);
        _currentState = newLeaf;
        InvokeEntryActions(source, newLeaf);
        StateChanged?.Invoke(source, newLeaf, trigger, args.ToArray());
    }

    private async ValueTask CommitTransitionAsync(TTrigger trigger, TState source, Transition<TContext, TState> transition, TriggerArgs args)
    {
        if (transition.IsInternal)
        {
            return;
        }

        var newLeaf = _definition.LeafOf(transition.Target);
        await InvokeExitActionsAsync(source, newLeaf).ConfigureAwait(false);
        _currentState = newLeaf;
        await InvokeEntryActionsAsync(source, newLeaf).ConfigureAwait(false);
        StateChanged?.Invoke(source, newLeaf, trigger, args.ToArray());
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

    private async ValueTask InvokeExitActionsAsync(TState source, TState destination)
    {
        foreach (var state in EnumerateExitPath(source, destination))
        {
            var config = _definition.GetConfiguration(state);
            if (config.ExitActionAsync is { } exitAsync)
            {
                await exitAsync(Context).ConfigureAwait(false);
            }
            else
            {
                config.ExitAction?.Invoke(Context);
            }
        }
    }

    private async ValueTask InvokeEntryActionsAsync(TState source, TState destination)
    {
        foreach (var state in EnumerateEntryPath(source, destination))
        {
            var config = _definition.GetConfiguration(state);
            if (config.EntryActionAsync is { } entryAsync)
            {
                await entryAsync(Context).ConfigureAwait(false);
            }
            else
            {
                config.EntryAction?.Invoke(Context);
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
}
