namespace Nalu.SharpState;

/// <summary>
/// A single resolved transition built by <see cref="ISyncStateTriggerBuilder{TContext, TState}"/> (or its async/arity variants).
/// Carries the optional guard, optional sync/async action, and either a target state (external transition)
/// or the <see cref="IsInternal"/> flag (internal transition that does not leave the current state).
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
public sealed class Transition<TContext, TState>
    where TState : struct, Enum
{
    private readonly TState _target;

    internal Transition(
        TState target,
        bool isInternal,
        Func<TContext, object?[], bool>? guard,
        Action<TContext, object?[]>? syncAction,
        Func<TContext, object?[], ValueTask>? asyncAction)
    {
        _target = target;
        IsInternal = isInternal;
        Guard = guard;
        SyncAction = syncAction;
        AsyncAction = asyncAction;
    }

    /// <summary>
    /// When <c>true</c>, the transition does not change the current state; only the action runs.
    /// When <c>false</c>, <see cref="Target"/> is the destination state.
    /// </summary>
    public bool IsInternal { get; }

    /// <summary>
    /// The destination state of this external transition.
    /// Accessing this on an internal transition throws <see cref="InvalidOperationException"/>.
    /// </summary>
    public TState Target => IsInternal
        ? throw new InvalidOperationException("Internal transitions do not have a target state.")
        : _target;

    /// <summary>
    /// Optional guard predicate. When <c>null</c>, the transition always fires.
    /// When non-null, the transition fires only if the guard returns <c>true</c>.
    /// </summary>
    public Func<TContext, object?[], bool>? Guard { get; }

    /// <summary>
    /// Optional synchronous action executed after the guard passes and before the state change is committed.
    /// </summary>
    public Action<TContext, object?[]>? SyncAction { get; }

    /// <summary>
    /// Optional asynchronous action executed after the guard passes and before the state change is committed.
    /// </summary>
    public Func<TContext, object?[], ValueTask>? AsyncAction { get; }
}
