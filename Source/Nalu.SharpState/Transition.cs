namespace Nalu.SharpState;

/// <summary>
/// A single resolved transition built by <see cref="ISyncStateTriggerBuilder{TContext, TState, TActor}"/> (or its arity variants).
/// Carries the optional guard, optional synchronous transition action, optional asynchronous reaction,
/// and either a target state (external transition) or the <see cref="IsInternal"/> flag
/// (internal transition that does not leave the current state).
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TActor">Type of the actor passed into post-transition reactions.</typeparam>
public sealed class Transition<TContext, TState, TActor>
    where TState : struct, Enum
{
    internal Transition(
        TState target,
        Func<TContext, TriggerArgs, TState>? targetSelector,
        bool isInternal,
        Func<TContext, TriggerArgs, bool>? guard,
        IReadOnlyList<GuardCondition> guardConditions,
        Action<TContext, TriggerArgs>? syncAction,
        Func<TActor, TContext, TriggerArgs, ValueTask>? reactionAsync)
    {
        _target = target;
        TargetSelector = targetSelector;
        IsInternal = isInternal;
        Guard = guard;
        GuardConditions = guardConditions;
        SyncAction = syncAction;
        ReactionAsync = reactionAsync;
    }

    private readonly TState _target;

    /// <summary>
    /// When <c>true</c>, the transition does not change the current state; only the action runs.
    /// When <c>false</c>, <see cref="Target"/> is the destination state.
    /// </summary>
    public bool IsInternal { get; }

    /// <summary>
    /// The destination state of this external transition.
    /// Accessing this on an internal transition throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public TState Target
        => IsInternal
            ? throw new InvalidOperationException("Internal transitions do not have a target state.")
            : TargetSelector is not null
                ? throw new InvalidOperationException("Dynamic transitions resolve their target state at dispatch time.")
                : _target;

    /// <summary>
    /// Optional target selector resolved at dispatch time before any state change is committed.
    /// </summary>
    public Func<TContext, TriggerArgs, TState>? TargetSelector { get; }

    /// <summary>
    /// Optional guard predicate. When <c>null</c>, the transition always fires.
    /// When non-null, the transition fires only if the guard returns <c>true</c>.
    /// </summary>
    public Func<TContext, TriggerArgs, bool>? Guard { get; }

    /// <summary>
    /// Ordered metadata for every <c>When(...)</c> call that contributed to <see cref="Guard"/>.
    /// </summary>
    public IReadOnlyList<GuardCondition> GuardConditions { get; }

    /// <summary>
    /// Optional synchronous action executed after the guard passes and before the state change is committed.
    /// </summary>
    public Action<TContext, TriggerArgs>? SyncAction { get; }

    /// <summary>
    /// Optional asynchronous reaction scheduled after the state transition has completed.
    /// </summary>
    public Func<TActor, TContext, TriggerArgs, ValueTask>? ReactionAsync { get; }
}
