#pragma warning disable CS1591

namespace Nalu.SharpState;

/// <summary>
/// Shared state for the concrete <c>StateTriggerBuilder{...}</c> variants.
/// </summary>
public abstract class StateTriggerBuilderBase<TContext, TState>
    where TContext : class
    where TState : struct, Enum
{
    private TState _target = default;
    private bool _targetSet;
    private bool _stay;
    private Func<TContext, TriggerArgs, bool>? _guard;
    private Action<TContext, TriggerArgs>? _syncAction;
    private Func<TContext, TriggerArgs, ValueTask>? _reactionAsync;

    protected void SetTarget(TState target)
    {
        _target = target;
        _targetSet = true;
    }

    protected void SetStay() => _stay = true;

    protected void SetGuard(Func<TContext, TriggerArgs, bool> guard) => _guard = guard;

    protected void SetSyncAction(Action<TContext, TriggerArgs> action) => _syncAction = action;

    protected void SetReactionAsync(Func<TContext, TriggerArgs, ValueTask> action) => _reactionAsync = action;

    public void Validate()
    {
        if (_targetSet && _stay)
        {
            throw new InvalidOperationException(
                "A transition cannot declare both a Target and Stay(). Choose one.");
        }

        if (!_targetSet && !_stay)
        {
            throw new InvalidOperationException(
                "A transition must declare either a Target state or Stay() for an internal transition.");
        }
    }

    public IReadOnlyList<Transition<TContext, TState>> BuildTransitions()
        => new[]
        {
            new Transition<TContext, TState>(
                _target,
                _stay,
                _guard,
                _syncAction,
                _reactionAsync)
        };
}

#pragma warning restore CS1591

/// <summary>
/// Concrete builder for parameterless triggers.
/// </summary>
public sealed class StateTriggerBuilder<TContext, TState> :
    StateTriggerBuilderBase<TContext, TState>,
    ISyncStateTriggerBuilder<TContext, TState>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTriggerBuilder<TContext, TState> ISyncStateTriggerBuilder<TContext, TState>.Target(TState target)
    {
        SetTarget(target);
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState> ISyncStateTriggerBuilder<TContext, TState>.Stay()
    {
        SetStay();
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState> ISyncStateTriggerBuilder<TContext, TState>.Ignore()
    {
        SetStay();
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState> ISyncStateTriggerBuilder<TContext, TState>.When(Func<TContext, bool> guard)
    {
        ArgumentNullException.ThrowIfNull(guard);
        SetGuard((context, _) => guard(context));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState> ISyncStateTriggerBuilder<TContext, TState>.Invoke(Action<TContext> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SetSyncAction((context, _) => action(context));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState> ISyncStateTriggerBuilder<TContext, TState>.ReactAsync(Func<TContext, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SetReactionAsync((context, _) => action(context));
        return this;
    }
}

/// <summary>
/// Concrete builder for single-argument triggers.
/// </summary>
public sealed class StateTriggerBuilder<TContext, TState, TArg0> :
    StateTriggerBuilderBase<TContext, TState>,
    ISyncStateTriggerBuilder<TContext, TState, TArg0>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTriggerBuilder<TContext, TState, TArg0> ISyncStateTriggerBuilder<TContext, TState, TArg0>.Target(TState target)
    {
        SetTarget(target);
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0> ISyncStateTriggerBuilder<TContext, TState, TArg0>.Stay()
    {
        SetStay();
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0> ISyncStateTriggerBuilder<TContext, TState, TArg0>.Ignore()
    {
        SetStay();
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0> ISyncStateTriggerBuilder<TContext, TState, TArg0>.When(Func<TContext, TArg0, bool> guard)
    {
        ArgumentNullException.ThrowIfNull(guard);
        SetGuard((context, args) => guard(context, (TArg0)args[0]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0> ISyncStateTriggerBuilder<TContext, TState, TArg0>.Invoke(Action<TContext, TArg0> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SetSyncAction((context, args) => action(context, (TArg0)args[0]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0> ISyncStateTriggerBuilder<TContext, TState, TArg0>.ReactAsync(Func<TContext, TArg0, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SetReactionAsync((context, args) => action(context, (TArg0)args[0]!));
        return this;
    }
}

/// <summary>
/// Concrete builder for two-argument triggers.
/// </summary>
public sealed class StateTriggerBuilder<TContext, TState, TArg0, TArg1> :
    StateTriggerBuilderBase<TContext, TState>,
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.Target(TState target)
    {
        SetTarget(target);
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.Stay()
    {
        SetStay();
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.Ignore()
    {
        SetStay();
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.When(Func<TContext, TArg0, TArg1, bool> guard)
    {
        ArgumentNullException.ThrowIfNull(guard);
        SetGuard((context, args) => guard(context, (TArg0)args[0]!, (TArg1)args[1]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.Invoke(Action<TContext, TArg0, TArg1> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SetSyncAction((context, args) => action(context, (TArg0)args[0]!, (TArg1)args[1]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.ReactAsync(Func<TContext, TArg0, TArg1, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SetReactionAsync((context, args) => action(context, (TArg0)args[0]!, (TArg1)args[1]!));
        return this;
    }
}

/// <summary>
/// Concrete builder for three-argument triggers.
/// </summary>
public sealed class StateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> :
    StateTriggerBuilderBase<TContext, TState>,
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.Target(TState target)
    {
        SetTarget(target);
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.Stay()
    {
        SetStay();
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.Ignore()
    {
        SetStay();
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.When(Func<TContext, TArg0, TArg1, TArg2, bool> guard)
    {
        ArgumentNullException.ThrowIfNull(guard);
        SetGuard((context, args) => guard(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.Invoke(Action<TContext, TArg0, TArg1, TArg2> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SetSyncAction((context, args) => action(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.ReactAsync(Func<TContext, TArg0, TArg1, TArg2, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SetReactionAsync((context, args) => action(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!));
        return this;
    }
}

/// <summary>
/// Concrete builder for four-argument triggers.
/// </summary>
public sealed class StateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> :
    StateTriggerBuilderBase<TContext, TState>,
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>
    where TContext : class
    where TState : struct, Enum
{
    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.Target(TState target)
    {
        SetTarget(target);
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.Stay()
    {
        SetStay();
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.Ignore()
    {
        SetStay();
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.When(Func<TContext, TArg0, TArg1, TArg2, TArg3, bool> guard)
    {
        ArgumentNullException.ThrowIfNull(guard);
        SetGuard((context, args) =>
            guard(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!, (TArg3)args[3]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.Invoke(Action<TContext, TArg0, TArg1, TArg2, TArg3> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SetSyncAction((context, args) =>
            action(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!, (TArg3)args[3]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.ReactAsync(Func<TContext, TArg0, TArg1, TArg2, TArg3, ValueTask> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        SetReactionAsync((context, args) =>
            action(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!, (TArg3)args[3]!));
        return this;
    }
}
