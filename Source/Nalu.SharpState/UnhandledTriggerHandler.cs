namespace Nalu.SharpState;

/// <summary>
/// Callback invoked by <see cref="StateMachineEngine{TContext, TState, TTrigger, TActor}"/> when a trigger fires but no
/// transition matches on the current leaf state nor any of its ancestors.
/// </summary>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TTrigger">Enum type listing all triggers of the machine.</typeparam>
/// <param name="currentState">The current leaf state that could not process <paramref name="trigger"/>.</param>
/// <param name="trigger">The trigger that was fired without a matching transition.</param>
/// <param name="args">The arguments originally passed to the trigger, boxed into an array.</param>
public delegate void UnhandledTriggerHandler<TState, TTrigger>(TState currentState, TTrigger trigger, object?[] args)
    where TState : struct, Enum
    where TTrigger : struct, Enum;
