using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class AsyncTests
{
    [Fact]
    public async Task FireAsync_awaits_async_action_and_transitions()
    {
        var cfg = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>();
        cfg.On(FlatTrigger.Go, new Transition<TestContext, FlatState>(
            FlatState.B,
            false,
            null,
            null,
            async (ctx, _) =>
            {
                await Task.Yield();
                ctx.Counter++;
            }));

        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger>>
        {
            [FlatState.A] = cfg,
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>(),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
        };
        var definition = new StateMachineDefinition<TestContext, FlatState, FlatTrigger>(map);
        var ctx = new TestContext();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, ctx);

        await engine.FireAsync(FlatTrigger.Go, TriggerArgs.Empty);

        engine.CurrentState.Should().Be(FlatState.B);
        ctx.Counter.Should().Be(1);
    }

    [Fact]
    public async Task FireAsync_propagates_exception_from_async_action()
    {
        var cfg = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>();
        cfg.On(FlatTrigger.Go, new Transition<TestContext, FlatState>(
            FlatState.B,
            false,
            null,
            null,
            (_, _) => throw new InvalidOperationException("boom")));

        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger>>
        {
            [FlatState.A] = cfg,
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>(),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
        };
        var definition = new StateMachineDefinition<TestContext, FlatState, FlatTrigger>(map);
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext());

        var act = async () => await engine.FireAsync(FlatTrigger.Go, TriggerArgs.Empty);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        engine.CurrentState.Should().Be(FlatState.A);
    }

    [Fact]
    public void Fire_on_async_only_transition_skips_action_but_still_commits()
    {
        var sideEffect = 0;
        var cfg = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>();
        cfg.On(FlatTrigger.Go, new Transition<TestContext, FlatState>(
            FlatState.B,
            false,
            null,
            null,
            (_, _) =>
            {
                sideEffect++;
                return default;
            }));

        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger>>
        {
            [FlatState.A] = cfg,
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>(),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
        };
        var definition = new StateMachineDefinition<TestContext, FlatState, FlatTrigger>(map);
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext());

        engine.Fire(FlatTrigger.Go, TriggerArgs.Empty);

        engine.CurrentState.Should().Be(FlatState.B);
        sideEffect.Should().Be(0);
    }

    [Fact]
    public async Task FireAsync_unhandled_trigger_invokes_callback()
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

        await engine.FireAsync(FlatTrigger.NoMatch, TriggerArgs.From(5));

        captured.Should().NotBeNull();
        captured!.Value.state.Should().Be(FlatState.A);
        captured.Value.trigger.Should().Be(FlatTrigger.NoMatch);
        captured.Value.args.Should().Equal(5);
    }

    [Fact]
    public async Task FireAsync_unhandled_with_default_callback_throws()
    {
        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger>>
        {
            [FlatState.A] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>(),
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>(),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
        };

        var definition = new StateMachineDefinition<TestContext, FlatState, FlatTrigger>(map);
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, new TestContext());

        var act = async () => await engine.FireAsync(FlatTrigger.NoMatch, TriggerArgs.Empty);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task FireAsync_runs_async_exit_then_entry_actions()
    {
        var map = new Dictionary<FlatState, IStateConfiguration<TestContext, FlatState, FlatTrigger>>
        {
            [FlatState.A] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
                .OnExitAsync(ctx =>
                {
                    ctx.Log.Add("exit:A");
                    return default;
                })
                .On(FlatTrigger.Go, TestTransition.ToTarget<TestContext, FlatState>(FlatState.B)),
            [FlatState.B] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
                .OnEntryAsync(ctx =>
                {
                    ctx.Log.Add("enter:B");
                    return default;
                }),
            [FlatState.C] = new TestStateConfigurator<TestContext, FlatState, FlatTrigger>()
        };

        var definition = new StateMachineDefinition<TestContext, FlatState, FlatTrigger>(map);
        var ctx = new TestContext();
        var engine = new StateMachineEngine<TestContext, FlatState, FlatTrigger>(definition, FlatState.A, ctx);

        await engine.FireAsync(FlatTrigger.Go, TriggerArgs.Empty);

        ctx.Log.Should().Equal("exit:A", "enter:B");
    }
}
