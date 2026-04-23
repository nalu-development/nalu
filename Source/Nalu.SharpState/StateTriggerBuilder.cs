namespace Nalu.SharpState;

/// <summary>
/// Shared state for the concrete <c>StateTriggerBuilder{...}</c> variants. Carries the accumulated target/stay
/// marker, guard, and sync or async action, then validates the combination and materializes a single
/// <see cref="Transition{TContext, TState}"/> via <see cref="BuildTransitions"/>.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
public abstract class StateTriggerBuilderBase<TContext, TState>
    where TState : struct, Enum
{
    private TState _target;
    private bool _targetSet;
    private bool _stay;
    private Func<TContext, TriggerArgs, bool>? _guard;
    private Action<TContext, TriggerArgs>? _syncAction;
    private Func<TContext, TriggerArgs, ValueTask>? _asyncAction;

    /// <summary>
    /// Records the destination state for this transition. Mutually exclusive with <see cref="SetStay"/>.
    /// </summary>
    /// <param name="target">The destination state.</param>
    protected void SetTarget(TState target)
    {
        _target = target;
        _targetSet = true;
    }

    /// <summary>
    /// Marks this transition as internal (no state change). Mutually exclusive with <see cref="SetTarget"/>.
    /// </summary>
    protected void SetStay() => _stay = true;

    /// <summary>
    /// Records the guard predicate.
    /// </summary>
    protected void SetGuard(Func<TContext, TriggerArgs, bool> guard) => _guard = guard;

    /// <summary>
    /// Records the synchronous action.
    /// </summary>
    protected void SetSyncAction(Action<TContext, TriggerArgs> action) => _syncAction = action;

    /// <summary>
    /// Records the asynchronous action.
    /// </summary>
    protected void SetAsyncAction(Func<TContext, TriggerArgs, ValueTask> action) => _asyncAction = action;

    /// <summary>
    /// Validates that exactly one of <c>Target</c>/<c>Stay</c> was configured. Throws <see cref="InvalidOperationException"/>
    /// if both or neither are set.
    /// </summary>
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

    /// <summary>
    /// Materializes the built transition as a single-element list, ready to be merged into the per-state configuration.
    /// Call <see cref="Validate"/> first.
    /// </summary>
    /// <returns>A single-element list containing the built <see cref="Transition{TContext, TState}"/>.</returns>
    public IReadOnlyList<Transition<TContext, TState>> BuildTransitions()
        => new[]
        {
            new Transition<TContext, TState>(
                _target,
                _stay,
                _guard,
                _syncAction,
                _asyncAction)
        };
}

/// <summary>
/// Concrete builder for a parameterless trigger. Implements both the sync and async builder interfaces;
/// the configure lambda's parameter type selects which set of operations is reachable at call site.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
public sealed class StateTriggerBuilder<TContext, TState>
    : StateTriggerBuilderBase<TContext, TState>,
      ISyncStateTriggerBuilder<TContext, TState>,
      IAsyncStateTriggerBuilder<TContext, TState>
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
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        SetGuard((context, _) => guard(context));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState> ISyncStateTriggerBuilder<TContext, TState>.Invoke(Action<TContext> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        SetSyncAction((context, _) => action(context));
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState> IAsyncStateTriggerBuilder<TContext, TState>.Target(TState target)
    {
        SetTarget(target);
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState> IAsyncStateTriggerBuilder<TContext, TState>.Stay()
    {
        SetStay();
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState> IAsyncStateTriggerBuilder<TContext, TState>.Ignore()
    {
        SetStay();
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState> IAsyncStateTriggerBuilder<TContext, TState>.When(Func<TContext, bool> guard)
    {
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        SetGuard((context, _) => guard(context));
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState> IAsyncStateTriggerBuilder<TContext, TState>.InvokeAsync(Func<TContext, ValueTask> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        SetAsyncAction((context, _) => action(context));
        return this;
    }
}

/// <summary>
/// Concrete builder for a one-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's argument.</typeparam>
public sealed class StateTriggerBuilder<TContext, TState, TArg0>
    : StateTriggerBuilderBase<TContext, TState>,
      ISyncStateTriggerBuilder<TContext, TState, TArg0>,
      IAsyncStateTriggerBuilder<TContext, TState, TArg0>
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
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        SetGuard((context, args) => guard(context, (TArg0)args[0]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0> ISyncStateTriggerBuilder<TContext, TState, TArg0>.Invoke(Action<TContext, TArg0> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        SetSyncAction((context, args) => action(context, (TArg0)args[0]!));
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0> IAsyncStateTriggerBuilder<TContext, TState, TArg0>.Target(TState target)
    {
        SetTarget(target);
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0> IAsyncStateTriggerBuilder<TContext, TState, TArg0>.Stay()
    {
        SetStay();
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0> IAsyncStateTriggerBuilder<TContext, TState, TArg0>.Ignore()
    {
        SetStay();
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0> IAsyncStateTriggerBuilder<TContext, TState, TArg0>.When(Func<TContext, TArg0, bool> guard)
    {
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        SetGuard((context, args) => guard(context, (TArg0)args[0]!));
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0> IAsyncStateTriggerBuilder<TContext, TState, TArg0>.InvokeAsync(Func<TContext, TArg0, ValueTask> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        SetAsyncAction((context, args) => action(context, (TArg0)args[0]!));
        return this;
    }
}

/// <summary>
/// Concrete builder for a two-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's first argument.</typeparam>
/// <typeparam name="TArg1">Type of the trigger's second argument.</typeparam>
public sealed class StateTriggerBuilder<TContext, TState, TArg0, TArg1>
    : StateTriggerBuilderBase<TContext, TState>,
      ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>,
      IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>
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
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        SetGuard((context, args) => guard(context, (TArg0)args[0]!, (TArg1)args[1]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.Invoke(Action<TContext, TArg0, TArg1> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        SetSyncAction((context, args) => action(context, (TArg0)args[0]!, (TArg1)args[1]!));
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.Target(TState target)
    {
        SetTarget(target);
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.Stay()
    {
        SetStay();
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.Ignore()
    {
        SetStay();
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.When(Func<TContext, TArg0, TArg1, bool> guard)
    {
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        SetGuard((context, args) => guard(context, (TArg0)args[0]!, (TArg1)args[1]!));
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1>.InvokeAsync(Func<TContext, TArg0, TArg1, ValueTask> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        SetAsyncAction((context, args) => action(context, (TArg0)args[0]!, (TArg1)args[1]!));
        return this;
    }
}

/// <summary>
/// Concrete builder for a three-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's first argument.</typeparam>
/// <typeparam name="TArg1">Type of the trigger's second argument.</typeparam>
/// <typeparam name="TArg2">Type of the trigger's third argument.</typeparam>
public sealed class StateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>
    : StateTriggerBuilderBase<TContext, TState>,
      ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>,
      IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>
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
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        SetGuard((context, args) => guard(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.Invoke(Action<TContext, TArg0, TArg1, TArg2> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        SetSyncAction((context, args) => action(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!));
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.Target(TState target)
    {
        SetTarget(target);
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.Stay()
    {
        SetStay();
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.Ignore()
    {
        SetStay();
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.When(Func<TContext, TArg0, TArg1, TArg2, bool> guard)
    {
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        SetGuard((context, args) => guard(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!));
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2>.InvokeAsync(Func<TContext, TArg0, TArg1, TArg2, ValueTask> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        SetAsyncAction((context, args) => action(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!));
        return this;
    }
}

/// <summary>
/// Concrete builder for a four-argument trigger.
/// </summary>
/// <typeparam name="TContext">Type of the user-supplied context carried by the machine.</typeparam>
/// <typeparam name="TState">Enum type listing all states of the machine.</typeparam>
/// <typeparam name="TArg0">Type of the trigger's first argument.</typeparam>
/// <typeparam name="TArg1">Type of the trigger's second argument.</typeparam>
/// <typeparam name="TArg2">Type of the trigger's third argument.</typeparam>
/// <typeparam name="TArg3">Type of the trigger's fourth argument.</typeparam>
public sealed class StateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>
    : StateTriggerBuilderBase<TContext, TState>,
      ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>,
      IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>
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
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        SetGuard((context, args) => guard(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!, (TArg3)args[3]!));
        return this;
    }

    ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> ISyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.Invoke(Action<TContext, TArg0, TArg1, TArg2, TArg3> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        SetSyncAction((context, args) => action(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!, (TArg3)args[3]!));
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.Target(TState target)
    {
        SetTarget(target);
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.Stay()
    {
        SetStay();
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.Ignore()
    {
        SetStay();
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.When(Func<TContext, TArg0, TArg1, TArg2, TArg3, bool> guard)
    {
        if (guard is null)
        {
            throw new ArgumentNullException(nameof(guard));
        }

        SetGuard((context, args) => guard(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!, (TArg3)args[3]!));
        return this;
    }

    IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3> IAsyncStateTriggerBuilder<TContext, TState, TArg0, TArg1, TArg2, TArg3>.InvokeAsync(Func<TContext, TArg0, TArg1, TArg2, TArg3, ValueTask> action)
    {
        if (action is null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        SetAsyncAction((context, args) => action(context, (TArg0)args[0]!, (TArg1)args[1]!, (TArg2)args[2]!, (TArg3)args[3]!));
        return this;
    }
}
