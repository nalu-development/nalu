#pragma warning disable CS1591

namespace Nalu.SharpState;

/// <summary>
/// Continuation builder for a configured transition with no trigger parameters.
/// </summary>
public interface ISyncStateTransitionBuilder<TContext, TState, TActor>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTransitionBuilder<TContext, TState, TActor> When(Func<TContext, bool> guard);
    ISyncStateTransitionBuilder<TContext, TState, TActor> When(Func<TContext, bool> guard, string? label);
    ISyncStateTransitionBuilder<TContext, TState, TActor> Invoke(Action<TContext> action);
    ISyncStateTransitionBuilder<TContext, TState, TActor> ReactAsync(Func<TActor, TContext, ValueTask> action);
}

/// <summary>
/// Fluent builder for triggers that take no parameters.
/// </summary>
public interface ISyncStateTriggerBuilder<TContext, TState, TActor>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTransitionBuilder<TContext, TState, TActor> Target(TState target);
    ISyncStateTransitionBuilder<TContext, TState, TActor> Target(Func<TContext, TState> targetSelector);
    ISyncStateTransitionBuilder<TContext, TState, TActor> Stay();
    void Ignore();
}

/// <summary>
/// Continuation builder for a configured transition with one trigger parameter.
/// </summary>
public interface ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0> When(Func<TContext, TArg0, bool> guard);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0> When(Func<TContext, TArg0, bool> guard, string? label);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0> Invoke(Action<TContext, TArg0> action);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0> ReactAsync(Func<TActor, TContext, TArg0, ValueTask> action);
}

/// <summary>
/// Fluent builder for triggers that take one parameter.
/// </summary>
public interface ISyncStateTriggerBuilder<TContext, TState, TActor, TArg0>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0> Target(TState target);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0> Target(Func<TContext, TArg0, TState> targetSelector);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0> Stay();
    void Ignore();
}

/// <summary>
/// Continuation builder for a configured transition with two trigger parameters.
/// </summary>
public interface ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1> When(Func<TContext, TArg0, TArg1, bool> guard);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1> When(Func<TContext, TArg0, TArg1, bool> guard, string? label);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1> Invoke(Action<TContext, TArg0, TArg1> action);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1> ReactAsync(Func<TActor, TContext, TArg0, TArg1, ValueTask> action);
}

/// <summary>
/// Fluent builder for triggers that take two parameters.
/// </summary>
public interface ISyncStateTriggerBuilder<TContext, TState, TActor, TArg0, TArg1>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1> Target(TState target);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1> Target(Func<TContext, TArg0, TArg1, TState> targetSelector);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1> Stay();
    void Ignore();
}

/// <summary>
/// Continuation builder for a configured transition with three trigger parameters.
/// </summary>
public interface ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2> When(Func<TContext, TArg0, TArg1, TArg2, bool> guard);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2> When(Func<TContext, TArg0, TArg1, TArg2, bool> guard, string? label);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2> Invoke(Action<TContext, TArg0, TArg1, TArg2> action);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2> ReactAsync(Func<TActor, TContext, TArg0, TArg1, TArg2, ValueTask> action);
}

/// <summary>
/// Fluent builder for triggers that take three parameters.
/// </summary>
public interface ISyncStateTriggerBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2> Target(TState target);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2> Target(Func<TContext, TArg0, TArg1, TArg2, TState> targetSelector);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2> Stay();
    void Ignore();
}

/// <summary>
/// Continuation builder for a configured transition with four trigger parameters.
/// </summary>
public interface ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2, TArg3>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2, TArg3> When(Func<TContext, TArg0, TArg1, TArg2, TArg3, bool> guard);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2, TArg3> When(Func<TContext, TArg0, TArg1, TArg2, TArg3, bool> guard, string? label);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2, TArg3> Invoke(Action<TContext, TArg0, TArg1, TArg2, TArg3> action);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2, TArg3> ReactAsync(Func<TActor, TContext, TArg0, TArg1, TArg2, TArg3, ValueTask> action);
}

/// <summary>
/// Fluent builder for triggers that take four parameters.
/// </summary>
public interface ISyncStateTriggerBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2, TArg3>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2, TArg3> Target(TState target);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2, TArg3> Target(Func<TContext, TArg0, TArg1, TArg2, TArg3, TState> targetSelector);
    ISyncStateTransitionBuilder<TContext, TState, TActor, TArg0, TArg1, TArg2, TArg3> Stay();
    void Ignore();
}

#pragma warning restore CS1591
