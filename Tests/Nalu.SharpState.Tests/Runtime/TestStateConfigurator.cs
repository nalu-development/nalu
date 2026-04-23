namespace Nalu.SharpState.Tests.Runtime;

internal sealed class TestStateConfigurator<TContext, TState, TTrigger>
    : StateConfigurator<TContext, TState, TTrigger>
    where TContext : class
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    public TestStateConfigurator<TContext, TState, TTrigger> On(TTrigger trigger, Transition<TContext, TState> transition)
    {
        AddTransitions(trigger, [transition]);
        return this;
    }

    public TestStateConfigurator<TContext, TState, TTrigger> Parent(TState parent)
    {
        SetParent(parent);
        return this;
    }

    public TestStateConfigurator<TContext, TState, TTrigger> AsStateMachine(TState initial)
    {
        SetInitialChild(initial);
        return this;
    }

    public TestStateConfigurator<TContext, TState, TTrigger> OnEntry(Action<TContext> action)
    {
        SetEntryAction(action);
        return this;
    }

    public TestStateConfigurator<TContext, TState, TTrigger> OnExit(Action<TContext> action)
    {
        SetExitAction(action);
        return this;
    }

}

internal static class TestTransition
{
    public static Transition<TContext, TState> ToTarget<TContext, TState>(
        TState target,
        Func<TContext, TriggerArgs, bool>? guard = null,
        Action<TContext, TriggerArgs>? syncAction = null,
        Func<TContext, TriggerArgs, ValueTask>? reactionAsync = null)
        where TContext : class
        where TState : struct, Enum
        => new(target, false, guard, syncAction, reactionAsync);

    public static Transition<TContext, TState> Stay<TContext, TState>(
        Action<TContext, TriggerArgs>? syncAction = null,
        Func<TContext, TriggerArgs, ValueTask>? reactionAsync = null,
        Func<TContext, TriggerArgs, bool>? guard = null)
        where TContext : class
        where TState : struct, Enum
        => new(default, true, guard, syncAction, reactionAsync);
}
