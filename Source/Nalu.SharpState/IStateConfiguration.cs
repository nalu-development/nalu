namespace Nalu.SharpState;

/// <summary>
/// Read-only view of a single state's configuration: the set of transitions indexed by trigger,
/// optional hierarchy metadata (parent state, initial child state), and optional entry/exit hooks.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TTrigger">Enum type listing all triggers of the machine.</typeparam>
/// <typeparam name="TActor">Type of the actor passed into post-transition reactions.</typeparam>
public interface IStateConfiguration<TContext, TState, TTrigger, TActor>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    /// <summary>
    /// The parent of this state, when this state is a sub-state of a composite. <c>null</c> otherwise.
    /// </summary>
    TState? ParentState { get; }

    /// <summary>
    /// When this state behaves as a sub-state-machine (composite), the default child state entered
    /// when the composite itself is targeted by a transition. <c>null</c> for leaf states.
    /// </summary>
    TState? InitialChildState { get; }

    /// <summary>
    /// Optional synchronous callback invoked when the machine enters this state during an external transition.
    /// </summary>
    Action<TContext>? EntryAction { get; }

    /// <summary>
    /// Optional synchronous callback invoked when the machine exits this state during an external transition.
    /// </summary>
    Action<TContext>? ExitAction { get; }

    /// <summary>
    /// Attempts to look up all transitions declared on this state for the given trigger.
    /// Multiple transitions may share a trigger (each guarded by a distinct <c>When</c>).
    /// </summary>
    /// <param name="trigger">The trigger to look up.</param>
    /// <param name="transitions">When the method returns <c>true</c>, the list of transitions in source order.</param>
    /// <returns><c>true</c> if at least one transition is configured for <paramref name="trigger"/>.</returns>
    bool TryGetTransitions(TTrigger trigger, out IReadOnlyList<Transition<TContext, TState, TActor>> transitions);
}
