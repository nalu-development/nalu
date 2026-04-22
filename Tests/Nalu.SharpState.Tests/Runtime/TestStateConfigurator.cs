namespace Nalu.SharpState.Tests.Runtime;

internal sealed class TestStateConfigurator<TContext, TState, TTrigger>
    : StateConfigurator<TContext, TState, TTrigger>
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
}

internal static class TestTransition
{
    public static Transition<TContext, TState> ToTarget<TContext, TState>(
        TState target,
        Func<TContext, object?[], bool>? guard = null,
        Action<TContext, object?[]>? syncAction = null,
        Func<TContext, object?[], ValueTask>? asyncAction = null)
        where TState : struct, Enum
        => new(target, false, guard, syncAction, asyncAction);

    public static Transition<TContext, TState> Stay<TContext, TState>(
        Action<TContext, object?[]>? syncAction = null,
        Func<TContext, object?[], ValueTask>? asyncAction = null,
        Func<TContext, object?[], bool>? guard = null)
        where TState : struct, Enum
        => new(default, true, guard, syncAction, asyncAction);
}
