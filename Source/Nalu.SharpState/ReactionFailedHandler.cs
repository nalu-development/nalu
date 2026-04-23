namespace Nalu.SharpState;

/// <summary>
/// Callback invoked when a background <c>ReactAsync(...)</c> reaction fails after the state transition has already completed.
/// </summary>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TTrigger">Enum type listing all triggers of the machine.</typeparam>
/// <param name="from">The committed source leaf state.</param>
/// <param name="to">The committed destination leaf state.</param>
/// <param name="trigger">The trigger that produced the reaction.</param>
/// <param name="args">The arguments originally passed to the trigger, boxed into an array.</param>
/// <param name="exception">The exception thrown by the background reaction.</param>
public delegate void ReactionFailedHandler<in TState, in TTrigger>(
    TState from,
    TState to,
    TTrigger trigger,
    object?[] args,
    Exception exception)
    where TState : struct, Enum
    where TTrigger : struct, Enum;
