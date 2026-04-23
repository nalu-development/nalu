using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class AsyncTests
{
    [Fact]
    public void Fire_schedules_reaction_after_transition_finishes()
    {
        var syncContext = new RecordingSynchronizationContext();
        var actor = new TestActor();
        TestActor? observedActor = null;
        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger, TestActor>>
        {
            [FlatState.A] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>()
                .WhenExiting(ctx => ctx.Log.Add("exit:A"))
                .On(FlatTrigger.Go, TestTransition.ToTarget<TestContext, FlatState, TestActor>(
                    FlatState.B,
                    syncAction: (ctx, _) => ctx.Log.Add("invoke"),
                    reactionAsync: async (reactionActor, ctx, _) =>
                    {
                        observedActor = reactionActor;
                        await Task.Yield();
                        ctx.Log.Add("react");
                    })),
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>()
                .WhenEntering(ctx => ctx.Log.Add("enter:B")),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>()
        };

        var ctx = new TestContext();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(
            new StateMachineDefinition<TestContext, FlatState, FlatTrigger, TestActor>(map),
            FlatState.A,
            ctx,
            actor);
        engine.StateChanged += (_, _, _, _) => ctx.Log.Add("changed");

        RunOn(syncContext, () => engine.Fire(FlatTrigger.Go, TriggerArgs.Empty));

        ctx.Log.Should().Equal("exit:A", "invoke", "enter:B", "changed");
        syncContext.Drain();
        observedActor.Should().BeSameAs(actor);
        ctx.Log.Should().Equal("exit:A", "invoke", "enter:B", "changed", "react");
    }

    [Fact]
    public void ReactionFailed_is_raised_when_background_reaction_throws()
    {
        var syncContext = new RecordingSynchronizationContext();
        var cfg = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>();
        cfg.On(FlatTrigger.Go, TestTransition.ToTarget<TestContext, FlatState, TestActor>(
            FlatState.B,
            reactionAsync: async (_, _, _) =>
            {
                await Task.Yield();
                throw new InvalidOperationException("boom");
            }));

        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger, TestActor>>
        {
            [FlatState.A] = cfg,
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>(),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>()
        };

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(
            new StateMachineDefinition<TestContext, FlatState, FlatTrigger, TestActor>(map),
            FlatState.A,
            new TestContext(),
            new TestActor());
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
        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger, TestActor>>
        {
            [FlatState.A] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>(),
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>(),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>()
        };

        var definition = new StateMachineDefinition<TestContext, FlatState, FlatTrigger, TestActor>(map);
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext(), new TestActor());
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
        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger, TestActor>>
        {
            [FlatState.A] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>(),
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>(),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>()
        };

        var definition = new StateMachineDefinition<TestContext, FlatState, FlatTrigger, TestActor>(map);
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext(), new TestActor());

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
