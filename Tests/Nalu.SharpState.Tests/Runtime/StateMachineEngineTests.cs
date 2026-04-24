using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class StateMachineEngineTests
{
    private static StateMachineDefinition<TestContext, FlatState, FlatTrigger, TestActor> BuildFlat(
        Action<IDictionary<FlatState, TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>>>? setup = null)
    {
        var map = new Dictionary<FlatState, TestStateConfigurator<TestContext, FlatState, FlatTrigger, TestActor>>
        {
            [FlatState.A] = new(),
            [FlatState.B] = new(),
            [FlatState.C] = new()
        };
        setup?.Invoke(map);

        var readonlyMap = map.ToDictionary(
            kv => kv.Key,
            kv => (IStateConfiguration<TestContext, FlatState, FlatTrigger, TestActor>) kv.Value);
        return new StateMachineDefinition<TestContext, FlatState, FlatTrigger, TestActor>(readonlyMap);
    }

    [Fact]
    public void Fire_flat_transition_moves_current_state_and_raises_StateChanged()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].On(
                FlatTrigger.Go,
                TestTransition.ToTarget<TestContext, FlatState, TestActor>(FlatState.B));
        });

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext(), new TestActor());
        (FlatState from, FlatState to, FlatTrigger trigger, object?[] args)? captured = null;
        engine.StateChanged += (from, to, trig, args) => captured = (from, to, trig, args);

        engine.Fire(FlatTrigger.Go, TriggerArgs.From(42, "payload"));

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
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext(), new TestActor());
        (FlatState state, FlatTrigger trigger, object?[] args)? captured = null;
        engine.OnUnhandled = (s, t, a) => captured = (s, t, a);

        engine.Fire(FlatTrigger.NoMatch, TriggerArgs.From(123));

        captured.Should().NotBeNull();
        captured!.Value.state.Should().Be(FlatState.A);
        captured.Value.trigger.Should().Be(FlatTrigger.NoMatch);
        captured.Value.args.Should().Equal(123);
        engine.CurrentState.Should().Be(FlatState.A);
    }

    [Fact]
    public void CanFire_returns_true_when_transition_matches_current_state()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].On(
                FlatTrigger.Go,
                TestTransition.ToTarget<TestContext, FlatState, TestActor>(FlatState.B));
        });

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext(), new TestActor());

        engine.CanFire(FlatTrigger.Go, TriggerArgs.Empty).Should().BeTrue();
        engine.CanFire(FlatTrigger.NoMatch, TriggerArgs.Empty).Should().BeFalse();
    }

    [Fact]
    public void CanFire_respects_guards_without_mutating_state()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].On(
                FlatTrigger.Go,
                TestTransition.ToTarget<TestContext, FlatState, TestActor>(
                    FlatState.B,
                    guard: (ctx, args) => ctx.Counter == (int)args[0]!));
        });

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(
            definition,
            FlatState.A,
            new TestContext { Counter = 4 },
            new TestActor());

        engine.CanFire(FlatTrigger.Go, TriggerArgs.From(3)).Should().BeFalse();
        engine.CanFire(FlatTrigger.Go, TriggerArgs.From(4)).Should().BeTrue();
        engine.CurrentState.Should().Be(FlatState.A);
    }

    [Fact]
    public void Fire_unhandled_with_default_callback_throws()
    {
        var definition = BuildFlat();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext(), new TestActor());

        var act = () => engine.Fire(FlatTrigger.NoMatch, TriggerArgs.Empty);

        act.Should().Throw<NotSupportedException>();
        engine.CurrentState.Should().Be(FlatState.A);
    }

    [Fact]
    public void Fire_unhandled_with_null_callback_is_silent_noop()
    {
        var definition = BuildFlat();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext(), new TestActor())
        {
            OnUnhandled = null,
        };

        var act = () => engine.Fire(FlatTrigger.NoMatch, TriggerArgs.Empty);

        act.Should().NotThrow();
        engine.CurrentState.Should().Be(FlatState.A);
    }

    [Fact]
    public void Fire_first_guarded_transition_that_passes_wins()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].AddAllFor(FlatTrigger.Go, [
                new Transition<TestContext, FlatState, TestActor>(FlatState.B, null, false, (ctx, _) => ctx.Counter > 10, null, null),
                new Transition<TestContext, FlatState, TestActor>(FlatState.C, null, false, null, null, null)
            ]);
        });

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext { Counter = 5 }, new TestActor());
        engine.Fire(FlatTrigger.Go, TriggerArgs.Empty);
        engine.CurrentState.Should().Be(FlatState.C);
    }

    [Fact]
    public void Fire_guard_receives_args_and_context()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].On(
                FlatTrigger.Go,
                TestTransition.ToTarget<TestContext, FlatState, TestActor>(
                    FlatState.B,
                    guard: (ctx, args) => args[0] is int i && i == ctx.Counter));
        });

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext { Counter = 42 }, new TestActor())
        {
            OnUnhandled = null,
        };
        engine.Fire(FlatTrigger.Go, TriggerArgs.From(41));
        engine.CurrentState.Should().Be(FlatState.A);

        engine.Fire(FlatTrigger.Go, TriggerArgs.From(42));
        engine.CurrentState.Should().Be(FlatState.B);
    }

    [Fact]
    public void Fire_runs_action_before_state_change_and_event()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].On(
                FlatTrigger.Go,
                TestTransition.ToTarget<TestContext, FlatState, TestActor>(
                    FlatState.B,
                    syncAction: (ctx, _) => ctx.Log.Add("action:" + ctx.Counter)));
        });

        var ctx = new TestContext { Counter = 1 };
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, ctx, new TestActor());
        engine.StateChanged += (_, _, _, _) => ctx.Log.Add("changed:" + ctx.Counter);

        engine.Fire(FlatTrigger.Go, TriggerArgs.Empty);

        ctx.Log.Should().BeEquivalentTo("action:1", "changed:1");
    }

    [Fact]
    public void Fire_dynamic_target_uses_context_and_args()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].On(
                FlatTrigger.Go,
                TestTransition.ToDynamicTarget<TestContext, FlatState, TestActor>(
                    (ctx, args) => (int)args[0]! == ctx.Counter ? FlatState.B : FlatState.C));
        });

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(
            definition,
            FlatState.A,
            new TestContext { Counter = 7 },
            new TestActor());

        engine.Fire(FlatTrigger.Go, TriggerArgs.From(7));
        engine.CurrentState.Should().Be(FlatState.B);
    }

    [Fact]
    public void Fire_dynamic_target_to_current_state_behaves_like_internal_transition()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A]
                .WhenEntering(ctx => ctx.Log.Add("enter:A"))
                .WhenExiting(ctx => ctx.Log.Add("exit:A"))
                .On(
                    FlatTrigger.Go,
                    TestTransition.ToDynamicTarget<TestContext, FlatState, TestActor>(
                        (_, args) => (bool)args[0]! ? FlatState.A : FlatState.B,
                        syncAction: (ctx, _) => ctx.Log.Add("invoke")));
            map[FlatState.B]
                .WhenEntering(ctx => ctx.Log.Add("enter:B"));
        });

        var ctx = new TestContext();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, ctx, new TestActor());
        var changed = false;
        engine.StateChanged += (_, _, _, _) => changed = true;

        engine.Fire(FlatTrigger.Go, TriggerArgs.From(true));

        engine.CurrentState.Should().Be(FlatState.A);
        changed.Should().BeFalse();
        ctx.Log.Should().Equal("invoke");
    }

    [Fact]
    public void Fire_when_all_guards_fail_invokes_unhandled()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A].AddAllFor(FlatTrigger.Go, [
                new Transition<TestContext, FlatState, TestActor>(FlatState.B, null, false, (_, _) => false, null, null),
                new Transition<TestContext, FlatState, TestActor>(FlatState.C, null, false, (_, _) => false, null, null)
            ]);
        });

        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext(), new TestActor());
        FlatState? capturedState = null;
        engine.OnUnhandled = (state, _, _) => capturedState = state;

        engine.Fire(FlatTrigger.Go, TriggerArgs.Empty);

        capturedState.Should().Be(FlatState.A);
        engine.CurrentState.Should().Be(FlatState.A);
    }

    [Fact]
    public void Fire_walks_to_parent_when_leaf_has_no_matching_transition()
    {
        var definition = HierarchyTests.CreateStandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger, TestActor>(definition, HierState.Authenticated, new TestContext(), new TestActor());

        engine.Fire(HierTrigger.Disconnect, TriggerArgs.Empty);

        engine.CurrentState.Should().Be(HierState.Idle);
    }

    [Fact]
    public void Fire_runs_exit_then_entry_actions_around_external_transition()
    {
        var definition = BuildFlat(map =>
        {
            map[FlatState.A]
                .WhenExiting(ctx => ctx.Log.Add("exit:A"))
                .On(FlatTrigger.Go, TestTransition.ToTarget<TestContext, FlatState, TestActor>(FlatState.B));
            map[FlatState.B]
                .WhenEntering(ctx => ctx.Log.Add("enter:B"));
        });

        var ctx = new TestContext();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, ctx, new TestActor());

        engine.Fire(FlatTrigger.Go, TriggerArgs.Empty);

        ctx.Log.Should().Equal("exit:A", "enter:B");
    }

    [Fact]
    public void Fire_throws_when_reentered_from_callback()
    {
        StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>? engine = null;
        var definition = BuildFlat(map =>
        {
            map[FlatState.A]
                .On(FlatTrigger.Go, TestTransition.ToTarget<TestContext, FlatState, TestActor>(FlatState.B));
            map[FlatState.B]
                .WhenEntering(_ => engine!.Fire(FlatTrigger.NoMatch, TriggerArgs.Empty));
        });

        engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger, TestActor>(definition, FlatState.A, new TestContext(), new TestActor())
        {
            OnUnhandled = null,
        };

        var act = () => engine.Fire(FlatTrigger.Go, TriggerArgs.Empty);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*cannot be fired while another trigger is still being processed*");
        engine.CurrentState.Should().Be(FlatState.B);
    }
}

internal static class TestConfiguratorExtensions
{
    public static TestStateConfigurator<TContext, TState, TTrigger, TActor> AddAllFor<TContext, TState, TTrigger, TActor>(
        this TestStateConfigurator<TContext, TState, TTrigger, TActor> self,
        TTrigger trigger,
        IReadOnlyList<Transition<TContext, TState, TActor>> transitions)
        where TContext : class
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
