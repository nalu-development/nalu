using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class StateMachineEngineTests
{
    private static StateMachineDefinition<TestContext, FlatState, FlatTrigger> BuildFlat(
        Action<IDictionary<FlatState, TestStateConfigurator<TestContext, FlatState, FlatTrigger>>>? setup = null)
    {
        var map = new Dictionary<FlatState, TestStateConfigurator<TestContext, FlatState, FlatTrigger>>
        {
            [FlatState.A] = new(),
            [FlatState.B] = new(),
            [FlatState.C] = new()
        };
        setup?.Invoke(map);

        var readonlyMap = map.ToDictionary(
            kv => kv.Key,
            kv => (IStateConfiguration<TestContext, FlatState, FlatTrigger>) kv.Value);
        return new StateMachineDefinition<TestContext, FlatState, FlatTrigger>(readonlyMap);
    }

    [Fact]
    public void Fire_flat_transition_moves_current_state_and_raises_StateChanged()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].On(
                FlatTrigger.Go,
                TestTransition.ToTarget<TestContext, FlatState>(FlatState.B));
        });

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext());
        (FlatState from, FlatState to, FlatTrigger trigger, object?[] args)? captured = null;
        engine.StateChanged += (from, to, trig, args) => captured = (from, to, trig, args);

        engine.Fire(FlatTrigger.Go, [42, "payload"]);

        engine.CurrentState.Should().Be(FlatState.B);
        captured.Should().NotBeNull();
        captured!.Value.from.Should().Be(FlatState.A);
        captured.Value.to.Should().Be(FlatState.B);
        captured.Value.trigger.Should().Be(FlatTrigger.Go);
        captured.Value.args.Should().Equal(42, "payload");
    }

    [Fact]
    public void Fire_unhandled_trigger_invokes_OnUnhandled()
    {
        var definition = BuildFlat();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext());
        (FlatState state, FlatTrigger trigger, object?[] args)? captured = null;
        engine.OnUnhandled = (s, t, a) => captured = (s, t, a);

        engine.Fire(FlatTrigger.NoMatch, [123]);

        captured.Should().NotBeNull();
        captured!.Value.state.Should().Be(FlatState.A);
        captured.Value.trigger.Should().Be(FlatTrigger.NoMatch);
        captured.Value.args.Should().Equal(123);
        engine.CurrentState.Should().Be(FlatState.A);
    }

    [Fact]
    public void Fire_unhandled_with_default_callback_throws()
    {
        var definition = BuildFlat();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext());

        var act = () => engine.Fire(FlatTrigger.NoMatch, []);

        act.Should().Throw<NotSupportedException>();
        engine.CurrentState.Should().Be(FlatState.A);
    }

    [Fact]
    public void Fire_unhandled_with_null_callback_is_silent_noop()
    {
        var definition = BuildFlat();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext())
        {
            OnUnhandled = null,
        };

        var act = () => engine.Fire(FlatTrigger.NoMatch, []);

        act.Should().NotThrow();
        engine.CurrentState.Should().Be(FlatState.A);
    }

    [Fact]
    public void Fire_first_guarded_transition_that_passes_wins()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].AddAllFor(FlatTrigger.Go, [
                new Transition<TestContext, FlatState>(FlatState.B, false, (ctx, _) => ctx.Counter > 10, null, null),
                new Transition<TestContext, FlatState>(FlatState.C, false, null, null, null)
            ]);
        });

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext { Counter = 5 });
        engine.Fire(FlatTrigger.Go, []);
        engine.CurrentState.Should().Be(FlatState.C);
    }

    [Fact]
    public void Fire_guard_receives_args_and_context()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].On(
                FlatTrigger.Go,
                TestTransition.ToTarget<TestContext, FlatState>(
                    FlatState.B,
                    guard: (ctx, args) => args[0] is int i && i == ctx.Counter));
        });

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext { Counter = 42 })
        {
            OnUnhandled = null,
        };
        engine.Fire(FlatTrigger.Go, [41]);
        engine.CurrentState.Should().Be(FlatState.A);

        engine.Fire(FlatTrigger.Go, [42]);
        engine.CurrentState.Should().Be(FlatState.B);
    }

    [Fact]
    public void Fire_runs_action_before_state_change_and_event()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].On(
                FlatTrigger.Go,
                TestTransition.ToTarget<TestContext, FlatState>(
                    FlatState.B,
                    syncAction: (ctx, _) => ctx.Log.Add("action:" + ctx.Counter)));
        });

        var ctx = new TestContext { Counter = 1 };
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, ctx);
        engine.StateChanged += (_, _, _, _) => ctx.Log.Add("changed:" + ctx.Counter);

        engine.Fire(FlatTrigger.Go, []);

        ctx.Log.Should().BeEquivalentTo("action:1", "changed:1");
    }
}

internal static class TestConfiguratorExtensions
{
    public static TestStateConfigurator<TContext, TState, TTrigger> AddAllFor<TContext, TState, TTrigger>(
        this TestStateConfigurator<TContext, TState, TTrigger> self,
        TTrigger trigger,
        IReadOnlyList<Transition<TContext, TState>> transitions)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        foreach (var t in transitions)
        {
            self.On(trigger, t);
        }

        return self;
    }
}
