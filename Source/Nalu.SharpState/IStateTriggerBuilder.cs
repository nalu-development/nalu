namespace Nalu.SharpState;

/// <summary>
/// Fluent builder describing a single synchronous transition for a parameterless trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
public interface ISyncStateTriggerBuilder<TContext, TState>
    where TState : struct, Enum
{
    /// <summary>Sets the destination state of this transition.</summary>
    /// <param name="target">The state to move to when this transition fires.</param>
    ISyncStateTriggerBuilder<TContext, TState> Target(TState target);

    /// <summary>Marks this transition as internal: the action runs but the state does not change.</summary>
    ISyncStateTriggerBuilder<TContext, TState> Stay();

    /// <summary>Marks this trigger as explicitly ignored. Equivalent to <see cref="Stay"/> with no action.</summary>
    ISyncStateTriggerBuilder<TContext, TState> Ignore();

    /// <summary>Sets the guard predicate. The transition only fires when the guard returns <c>true</c>.</summary>
    /// <param name="guard">The predicate evaluated with the current context.</param>
    ISyncStateTriggerBuilder<TContext, TState> When(Func<TContext, bool> guard);

    /// <summary>Sets the synchronous action executed when the guard passes and before the state is committed.</summary>
    /// <param name="action">The action to execute.</param>
    ISyncStateTriggerBuilder<TContext, TState> Invoke(Action<TContext> action);
}

/// <summary>
/// Fluent builder describing a single synchronous transition for a one-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's argument.</typeparam>
public interface ISyncStateTriggerBuilder<TContext, TState, TArg0>
    where TState : struct, Enum
{
    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Target"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0> Target(TState target);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Stay"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0> Stay();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Ignore"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0> Ignore();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.When(Func{TContext, bool})"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0> When(Func<TContext, TArg0, bool> guard);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Invoke(Action{TContext})"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0> Invoke(Action<TContext, TArg0> action);
}

/// <summary>
/// Fluent builder describing a single synchronous transition for a two-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's first argument.</typeparam>
/// <typeparam name="TArg1">Type of the trigger's second argument.</typeparam>
public interface ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>
    where TState : struct, Enum
{
    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Target"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Target(TState target);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Stay"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Stay();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Ignore"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Ignore();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.When(Func{TContext, bool})"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> When(Func<TContext, TArg0, TArg1, bool> guard);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Invoke(Action{TContext})"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Invoke(Action<TContext, TArg0, TArg1> action);
}

/// <summary>
/// Fluent builder describing a single synchronous transition for a three-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's first argument.</typeparam>
/// <typeparam name="TArg1">Type of the trigger's second argument.</typeparam>
/// <typeparam name="TArg2">Type of the trigger's third argument.</typeparam>
public interface ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>
    where TState : struct, Enum
{
    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Target"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Target(TState target);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Stay"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Stay();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Ignore"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Ignore();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.When(Func{TContext, bool})"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> When(Func<TContext, TArg0, TArg1, TArg2, bool> guard);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Invoke(Action{TContext})"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Invoke(Action<TContext, TArg0, TArg1, TArg2> action);
}

/// <summary>
/// Fluent builder describing a single synchronous transition for a four-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's first argument.</typeparam>
/// <typeparam name="TArg1">Type of the trigger's second argument.</typeparam>
/// <typeparam name="TArg2">Type of the trigger's third argument.</typeparam>
/// <typeparam name="TArg3">Type of the trigger's fourth argument.</typeparam>
public interface ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>
    where TState : struct, Enum
{
    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Target"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Target(TState target);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Stay"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Stay();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Ignore"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Ignore();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.When(Func{TContext, bool})"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> When(Func<TContext, TArg0, TArg1, TArg2, TArg3, bool> guard);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Invoke(Action{TContext})"/>
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Invoke(Action<TContext, TArg0, TArg1, TArg2, TArg3> action);
}

/// <summary>
/// Fluent builder describing a single asynchronous transition for a parameterless trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
public interface IAsyncStateTriggerBuilder<TContext, TState>
    where TState : struct, Enum
{
    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Target"/>
    IAsyncStateTriggerBuilder<TContext, TState> Target(TState target);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Stay"/>
    IAsyncStateTriggerBuilder<TContext, TState> Stay();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Ignore"/>
    IAsyncStateTriggerBuilder<TContext, TState> Ignore();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.When(Func{TContext, bool})"/>
    IAsyncStateTriggerBuilder<TContext, TState> When(Func<TContext, bool> guard);

    /// <summary>Sets the asynchronous action executed when the guard passes and before the state is committed.</summary>
    /// <param name="action">The asynchronous action to execute.</param>
    IAsyncStateTriggerBuilder<TContext, TState> InvokeAsync(Func<TContext, ValueTask> action);
}

/// <summary>
/// Fluent builder describing a single asynchronous transition for a one-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's argument.</typeparam>
public interface IAsyncStateTriggerBuilder<TContext, TState, TArg0>
    where TState : struct, Enum
{
    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Target"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0> Target(TState target);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Stay"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0> Stay();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Ignore"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0> Ignore();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.When(Func{TContext, bool})"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0> When(Func<TContext, TArg0, bool> guard);

    /// <inheritdoc cref="IAsyncStateTriggerBuilder{TContext, TState}.InvokeAsync(Func{TContext, ValueTask})"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0> InvokeAsync(Func<TContext, TArg0, ValueTask> action);
}

/// <summary>
/// Fluent builder describing a single asynchronous transition for a two-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's first argument.</typeparam>
/// <typeparam name="TArg1">Type of the trigger's second argument.</typeparam>
public interface IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>
    where TState : struct, Enum
{
    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Target"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Target(TState target);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Stay"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Stay();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Ignore"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Ignore();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.When(Func{TContext, bool})"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> When(Func<TContext, TArg0, TArg1, bool> guard);

    /// <inheritdoc cref="IAsyncStateTriggerBuilder{TContext, TState}.InvokeAsync(Func{TContext, ValueTask})"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> InvokeAsync(Func<TContext, TArg0, TArg1, ValueTask> action);
}

/// <summary>
/// Fluent builder describing a single asynchronous transition for a three-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's first argument.</typeparam>
/// <typeparam name="TArg1">Type of the trigger's second argument.</typeparam>
/// <typeparam name="TArg2">Type of the trigger's third argument.</typeparam>
public interface IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>
    where TState : struct, Enum
{
    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Target"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Target(TState target);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Stay"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Stay();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Ignore"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Ignore();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.When(Func{TContext, bool})"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> When(Func<TContext, TArg0, TArg1, TArg2, bool> guard);

    /// <inheritdoc cref="IAsyncStateTriggerBuilder{TContext, TState}.InvokeAsync(Func{TContext, ValueTask})"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> InvokeAsync(Func<TContext, TArg0, TArg1, TArg2, ValueTask> action);
}

/// <summary>
/// Fluent builder describing a single asynchronous transition for a four-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's first argument.</typeparam>
/// <typeparam name="TArg1">Type of the trigger's second argument.</typeparam>
/// <typeparam name="TArg2">Type of the trigger's third argument.</typeparam>
/// <typeparam name="TArg3">Type of the trigger's fourth argument.</typeparam>
public interface IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>
    where TState : struct, Enum
{
    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Target"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Target(TState target);

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Stay"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Stay();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.Ignore"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Ignore();

    /// <inheritdoc cref="ISyncStateTriggerBuilder{TContext, TState}.When(Func{TContext, bool})"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> When(Func<TContext, TArg0, TArg1, TArg2, TArg3, bool> guard);

    /// <inheritdoc cref="IAsyncStateTriggerBuilder{TContext, TState}.InvokeAsync(Func{TContext, ValueTask})"/>
    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> InvokeAsync(Func<TContext, TArg0, TArg1, TArg2, TArg3, ValueTask> action);
}
