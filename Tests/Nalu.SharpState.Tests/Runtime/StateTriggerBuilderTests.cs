using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class StateTriggerBuilderTests
{
    [Fact]
    public void Validate_requires_Target_or_Stay()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor>();
        var act = builder.Validate;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Target*Stay*");
    }

    [Fact]
    public void Validate_rejects_Target_and_Stay_together()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor> targetPhase = builder;
        targetPhase.Target(FlatState.B);
        ((ISyncStateTriggerBuilder<TestContext, FlatState, TestActor>)builder).Stay();
        var act = () => builder.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuildTransitions_produces_single_transition_with_target()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor> sync = builder;
        sync.Target(FlatState.B);
        builder.Validate();

        var list = builder.BuildTransitions();

        list.Should().ContainSingle();
        var transition = list[0];
        transition.IsInternal.Should().BeFalse();
        transition.Target.Should().Be(FlatState.B);
    }

    [Fact]
    public void BuildTransitions_produces_single_transition_with_dynamic_target()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor, int>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor, int> targetPhase = builder;
        targetPhase.Target((ctx, step) => ctx.Counter == step ? FlatState.B : FlatState.C);
        builder.Validate();

        var transition = builder.BuildTransitions()[0];

        transition.TargetSelector.Should().NotBeNull();
        transition.TargetSelector!(new TestContext { Counter = 3 }, TriggerArgs.From(3)).Should().Be(FlatState.B);
        transition.TargetSelector!(new TestContext { Counter = 1 }, TriggerArgs.From(3)).Should().Be(FlatState.C);
        var act = () => _ = transition.Target;
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*dispatch time*");
    }

    [Fact]
    public void BuildTransitions_produces_internal_transition_on_Stay()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor> sync = builder;
        sync.Stay();
        builder.Validate();

        var transition = builder.BuildTransitions()[0];
        transition.IsInternal.Should().BeTrue();
        var act = () => _ = transition.Target;
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void One_arg_builder_unpacks_argument()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor, string>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor, string> sync = builder;
        sync
            .Target(FlatState.B)
            .When((_, arg) => arg == "ok")
            .Invoke((ctx, arg) => ctx.LastArg = arg);
        builder.Validate();

        var transition = builder.BuildTransitions()[0];
        var ctx = new TestContext();
        transition.Guard!(ctx, TriggerArgs.From("bad")).Should().BeFalse();
        transition.Guard!(ctx, TriggerArgs.From("ok")).Should().BeTrue();
        transition.SyncAction!(ctx, TriggerArgs.From("ok"));
        ctx.LastArg.Should().Be("ok");
    }

    [Fact]
    public void Two_arg_builder_unpacks_both_arguments()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor, string, int>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor, string, int> sync = builder;
        sync
            .Target(FlatState.B)
            .When((_, s, i) => s.Length == i)
            .Invoke((ctx, s, i) => ctx.Log.Add($"{s}:{i}"));
        builder.Validate();

        var transition = builder.BuildTransitions()[0];
        var ctx = new TestContext();
        transition.Guard!(ctx, TriggerArgs.From("hi", 2)).Should().BeTrue();
        transition.Guard!(ctx, TriggerArgs.From("hi", 3)).Should().BeFalse();
        transition.SyncAction!(ctx, TriggerArgs.From("hi", 2));
        ctx.Log.Should().Equal("hi:2");
    }

    [Fact]
    public void Repeated_When_and_Invoke_registrations_compose_in_definition_order()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor, string>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor, string> targetPhase = builder;
        targetPhase
            .Target(FlatState.B)
            .When((ctx, arg) =>
            {
                ctx.Log.Add("guard:1");
                return arg.StartsWith("o", StringComparison.Ordinal);
            })
            .When((ctx, arg) =>
            {
                ctx.Log.Add("guard:2");
                return arg.EndsWith("k", StringComparison.Ordinal);
            })
            .Invoke((ctx, arg) => ctx.Log.Add("action:1:" + arg))
            .Invoke((ctx, arg) => ctx.Log.Add("action:2:" + arg));
        builder.Validate();

        var transition = builder.BuildTransitions()[0];
        var ctx = new TestContext();

        transition.Guard!(ctx, TriggerArgs.From("ok")).Should().BeTrue();
        ctx.Log.Should().Equal("guard:1", "guard:2");

        ctx.Log.Clear();
        transition.Guard!(ctx, TriggerArgs.From("no")).Should().BeFalse();
        ctx.Log.Should().Equal("guard:1");

        ctx.Log.Clear();
        transition.SyncAction!(ctx, TriggerArgs.From("ok"));
        ctx.Log.Should().Equal("action:1:ok", "action:2:ok");
    }

    [Fact]
    public void Repeated_When_registrations_preserve_guard_labels_in_definition_order()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor, string>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor, string> targetPhase = builder;
        targetPhase
            .Target(FlatState.B)
            .When((_, arg) => arg.StartsWith("o", StringComparison.Ordinal), "Starts with o")
            .When((_, arg) => arg.EndsWith("k", StringComparison.Ordinal));
        builder.Validate();

        var transition = builder.BuildTransitions()[0];

        transition.GuardConditions.Select(condition => condition.Label)
            .Should()
            .Equal("Starts with o", null);
    }

    [Fact]
    public async Task ReactAsync_stores_background_reaction()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor, int>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor, int> sync = builder;
        sync
            .Target(FlatState.B)
            .ReactAsync(async (_, ctx, i) =>
            {
                await Task.Yield();
                ctx.Counter += i;
            });
        builder.Validate();

        var actor = new TestActor();
        var transition = builder.BuildTransitions()[0];
        transition.ReactionAsync.Should().NotBeNull();
        var ctx = new TestContext { Counter = 10 };
        await transition.ReactionAsync!(actor, ctx, TriggerArgs.From(5));
        ctx.Counter.Should().Be(15);
    }

    [Fact]
    public async Task Repeated_ReactAsync_registrations_compose_in_definition_order()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor, int>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor, int> targetPhase = builder;
        targetPhase
            .Target(FlatState.B)
            .ReactAsync(async (_, ctx, value) =>
            {
                await Task.Yield();
                ctx.Log.Add("reaction:1:" + value);
            })
            .ReactAsync(async (_, ctx, value) =>
            {
                await Task.Yield();
                ctx.Log.Add("reaction:2:" + value);
            });
        builder.Validate();

        var transition = builder.BuildTransitions()[0];
        var ctx = new TestContext();

        await transition.ReactionAsync!(new TestActor(), ctx, TriggerArgs.From(7));
        ctx.Log.Should().Equal("reaction:1:7", "reaction:2:7");
    }

    [Fact]
    public void Ignore_is_syntax_sugar_for_internal_transition()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, TestActor>();
        ISyncStateTriggerBuilder<TestContext, FlatState, TestActor> sync = builder;
        sync.Ignore();
        builder.Validate();

        builder.BuildTransitions()[0].IsInternal.Should().BeTrue();
    }
}
