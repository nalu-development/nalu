using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class AsyncTests
{
    [Fact]
    public void Fire_schedules_reaction_after_transition_finishes()
    {
        var syncContext = new RecordingSynchronizationContext();
        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger>>
        {
            [FlatState.A] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
                .WhenExiting(ctx => ctx.Log.Add("exit:A"))
                .On(FlatTrigger.Go, TestTransition.ToTarget<TestContext, FlatState>(
                    FlatState.B,
                    syncAction: (ctx, _) => ctx.Log.Add("invoke"),
                    reactionAsync: async (ctx, _) =>
                    {
                        await Task.Yield();
                        ctx.Log.Add("react");
                    })),
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
                .WhenEntering(ctx => ctx.Log.Add("enter:B")),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
        };

        var ctx = new TestContext();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(
            new StateMachineDefinition<TestContext, FlatState, FlatTrigger>(map),
            FlatState.A,
            ctx);
        engine.StateChanged += (_, _, _, _) => ctx.Log.Add("changed");

        RunOn(syncContext, () => engine.Fire(FlatTrigger.Go, TriggerArgs.Empty));

        ctx.Log.Should().Equal("exit:A", "invoke", "enter:B", "changed");
        syncContext.Drain();
        ctx.Log.Should().Equal("exit:A", "invoke", "enter:B", "changed", "react");
    }

    [Fact]
    public void ReactionFailed_is_raised_when_background_reaction_throws()
    {
        var syncContext = new RecordingSynchronizationContext();
        var cfg = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>();
        cfg.On(FlatTrigger.Go, TestTransition.ToTarget<TestContext, FlatState>(
            FlatState.B,
            reactionAsync: async (_, _) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("boom");
            }));

        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger>>
        {
            [FlatState.A] = cfg,
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>(),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
        };

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(
            new StateMachineDefinition<TestContext, FlatState, FlatTrigger>(map),
            FlatState.A,
            new TestContext());
        (FlatState from, FlatState to, FlatTrigger trigger, object?[] args, Exception exception)? failure = null;
        engine.ReactionFailed += (from, to, trigger, args, exception) => failure = (from, to, trigger, args, exception);

        RunOn(syncContext, () => engine.Fire(FlatTrigger.Go, TriggerArgs.From(5)));
        syncContext.Drain();

        engine.CurrentState.Should().Be(FlatState.B);
        failure.Should().NotBeNull();
        failure!.Value.from.Should().Be(FlatState.A);
        failure.Value.to.Should().Be(FlatState.B);
        failure.Value.trigger.Should().Be(FlatTrigger.Go);
        failure.Value.args.Should().Equal(5);
        failure.Value.exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("boom");
    }

    [Fact]
    public void Fire_unhandled_trigger_invokes_callback()
    {
        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger>>
        {
            [FlatState.A] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>(),
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>(),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
        };

        var definition = new StateMachineDefinition<TestContext, FlatState, FlatTrigger>(map);
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext());
        (FlatState state, FlatTrigger trigger, object?[] args)? captured = null;
        engine.OnUnhandled = (state, trigger, args) => captured = (state, trigger, args);

        engine.Fire(FlatTrigger.NoMatch, TriggerArgs.From(5));

        captured.Should().NotBeNull();
        captured!.Value.state.Should().Be(FlatState.A);
        captured.Value.trigger.Should().Be(FlatTrigger.NoMatch);
        captured.Value.args.Should().Equal(5);
    }

    [Fact]
    public void Fire_unhandled_with_default_callback_throws()
    {
        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger>>
        {
            [FlatState.A] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>(),
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>(),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
        };

        var definition = new StateMachineDefinition<TestContext, FlatState, FlatTrigger>(map);
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext());

        var act = () => engine.Fire(FlatTrigger.NoMatch, TriggerArgs.Empty);

        act.Should().Throw<NotSupportedException>();
    }

    private static void RunOn(RecordingSynchronizationContext synchronizationContext, Action action)
    {
        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(synchronizationContext);
        try
        {
            action();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback Callback, object? State)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Enqueue((d, state));

        public void Drain()
        {
            while (_queue.Count > 0)
            {
                var (callback, state) = _queue.Dequeue();
                var previous = Current;
                SetSynchronizationContext(this);
                try
                {
                    callback(state);
                }
                finally
                {
                    SetSynchronizationContext(previous);
                }
            }
        }
    }
}
