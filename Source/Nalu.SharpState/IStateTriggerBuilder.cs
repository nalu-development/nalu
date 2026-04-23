#pragma warning disable CS1591

namespace Nalu.SharpState;

/// <summary>
/// Fluent builder for triggers that take no parameters.
/// </summary>
public interface ISyncStateTriggerBuilder<TContext, TState>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTriggerBuilder<TContext, TState> Target(TState target);
    ISyncStateTriggerBuilder<TContext, TState> Stay();
    ISyncStateTriggerBuilder<TContext, TState> Ignore();
    ISyncStateTriggerBuilder<TContext, TState> When(Func<TContext, bool> guard);
    ISyncStateTriggerBuilder<TContext, TState> Invoke(Action<TContext> action);
    ISyncStateTriggerBuilder<TContext, TState> ReactAsync(Func<TContext, ValueTask> action);
}

/// <summary>
/// Fluent builder for triggers that take one parameter.
/// </summary>
public interface ISyncStateTriggerBuilder<TContext, TState, TArg0>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTriggerBuilder<TContext, TState, TArg0> Target(TState target);
    ISyncStateTriggerBuilder<TContext, TState, TArg0> Stay();
    ISyncStateTriggerBuilder<TContext, TState, TArg0> Ignore();
    ISyncStateTriggerBuilder<TContext, TState, TArg0> When(Func<TContext, TArg0, bool> guard);
    ISyncStateTriggerBuilder<TContext, TState, TArg0> Invoke(Action<TContext, TArg0> action);
    ISyncStateTriggerBuilder<TContext, TState, TArg0> ReactAsync(Func<TContext, TArg0, ValueTask> action);
}

/// <summary>
/// Fluent builder for triggers that take two parameters.
/// </summary>
public interface ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Target(TState target);
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Stay();
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Ignore();
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> When(Func<TContext, TArg0, TArg1, bool> guard);
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> Invoke(Action<TContext, TArg0, TArg1> action);
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> ReactAsync(Func<TContext, TArg0, TArg1, ValueTask> action);
}

/// <summary>
/// Fluent builder for triggers that take three parameters.
/// </summary>
public interface ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Target(TState target);
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Stay();
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Ignore();
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> When(Func<TContext, TArg0, TArg1, TArg2, bool> guard);
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> Invoke(Action<TContext, TArg0, TArg1, TArg2> action);
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> ReactAsync(Func<TContext, TArg0, TArg1, TArg2, ValueTask> action);
}

/// <summary>
/// Fluent builder for triggers that take four parameters.
/// </summary>
public interface ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Target(TState target);
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Stay();
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Ignore();
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> When(Func<TContext, TArg0, TArg1, TArg2, TArg3, bool> guard);
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> Invoke(Action<TContext, TArg0, TArg1, TArg2, TArg3> action);
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> ReactAsync(Func<TContext, TArg0, TArg1, TArg2, TArg3, ValueTask> action);
}

#pragma warning restore CS1591
