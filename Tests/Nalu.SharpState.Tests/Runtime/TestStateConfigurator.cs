namespace Nalu.SharpState.Tests.Runtime;

internal sealed class TestStateConfigurator<TContext, TState, TTrigger, TActor>
    : StateConfigurator<TContext, TState, TTrigger, TActor>
    where TContext : class
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    public TestStateConfigurator<TContext, TState, TTrigger, TActor> On(TTrigger trigger, Transition<TContext, TState, TActor> transition)
    {
        AddTransitions(trigger, [transition]);
        return this;
    }

    public TestStateConfigurator<TContext, TState, TTrigger, TActor> Parent(TState parent)
    {
        SetParent(parent);
        return this;
    }

    public TestStateConfigurator<TContext, TState, TTrigger, TActor> AsStateMachine(TState initial)
    {
        SetInitialChild(initial);
        return this;
    }

    public TestStateConfigurator<TContext, TState, TTrigger, TActor> WhenEntering(Action<TContext> action)
    {
        SetEntryAction(action);
        return this;
    }

    public TestStateConfigurator<TContext, TState, TTrigger, TActor> WhenExiting(Action<TContext> action)
    {
        SetExitAction(action);
        return this;
    }

}

internal static class TestTransition
{
    public static Transition<TContext, TState, TActor> ToTarget<TContext, TState, TActor>(
        TState target,
        Func<TContext, TriggerArgs, bool>? guard = null,
        IReadOnlyList<GuardCondition>? guardConditions = null,
        Action<TContext, TriggerArgs>? syncAction = null,
        Func<TActor, TContext, TriggerArgs, ValueTask>? reactionAsync = null)
        where TContext : class
        where TState : struct, Enum
        => new(target, null, false, guard, guardConditions ?? Array.Empty<GuardCondition>(), syncAction, reactionAsync);

    public static Transition<TContext, TState, TActor> ToDynamicTarget<TContext, TState, TActor>(
        Func<TContext, TriggerArgs, TState> targetSelector,
        Func<TContext, TriggerArgs, bool>? guard = null,
        IReadOnlyList<GuardCondition>? guardConditions = null,
        Action<TContext, TriggerArgs>? syncAction = null,
        Func<TActor, TContext, TriggerArgs, ValueTask>? reactionAsync = null)
        where TContext : class
        where TState : struct, Enum
        => new(default!, targetSelector, false, guard, guardConditions ?? Array.Empty<GuardCondition>(), syncAction, reactionAsync);

    public static Transition<TContext, TState, TActor> Stay<TContext, TState, TActor>(
        Action<TContext, TriggerArgs>? syncAction = null,
        Func<TActor, TContext, TriggerArgs, ValueTask>? reactionAsync = null,
        Func<TContext, TriggerArgs, bool>? guard = null,
        IReadOnlyList<GuardCondition>? guardConditions = null)
        where TContext : class
        where TState : struct, Enum
        => new(default!, null, true, guard, guardConditions ?? Array.Empty<GuardCondition>(), syncAction, reactionAsync);
}

internal sealed class TestActor;
