using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class StateTriggerBuilderTests
{
    [Fact]
    public void Validate_requires_Target_or_Stay()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState>();
        var act = () => builder.Validate();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Target*Stay*");
    }

    [Fact]
    public void Validate_rejects_Target_and_Stay_together()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState>();
        ISyncStateTriggerBuilder<TestContext, FlatState> sync = builder;
        sync.Target(FlatState.B).Stay();
        var act = () => builder.Validate();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuildTransitions_produces_single_transition_with_target()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState>();
        ISyncStateTriggerBuilder<TestContext, FlatState> sync = builder;
        sync.Target(FlatState.B);
        builder.Validate();

        var list = builder.BuildTransitions();

        list.Should().ContainSingle();
        var transition = list[0];
        transition.IsInternal.Should().BeFalse();
        transition.Target.Should().Be(FlatState.B);
    }

    [Fact]
    public void BuildTransitions_produces_internal_transition_on_Stay()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState>();
        ISyncStateTriggerBuilder<TestContext, FlatState> sync = builder;
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
        var builder = new StateTriggerBuilder<TestContext, FlatState, string>();
        ISyncStateTriggerBuilder<TestContext, FlatState, string> sync = builder;
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
        var builder = new StateTriggerBuilder<TestContext, FlatState, string, int>();
        ISyncStateTriggerBuilder<TestContext, FlatState, string, int> sync = builder;
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
    public async Task InvokeAsync_stores_async_action()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState, int>();
        IAsyncStateTriggerBuilder<TestContext, FlatState, int> asyncBuilder = builder;
        asyncBuilder
            .Target(FlatState.B)
            .InvokeAsync(async (ctx, i) =>
            {
                await Task.Yield();
                ctx.Counter += i;
            });
        builder.Validate();

        var transition = builder.BuildTransitions()[0];
        transition.AsyncAction.Should().NotBeNull();
        var ctx = new TestContext { Counter = 10 };
        await transition.AsyncAction!(ctx, TriggerArgs.From(5));
        ctx.Counter.Should().Be(15);
    }

    [Fact]
    public void Ignore_is_syntax_sugar_for_internal_transition()
    {
        var builder = new StateTriggerBuilder<TestContext, FlatState>();
        ISyncStateTriggerBuilder<TestContext, FlatState> sync = builder;
        sync.Ignore();
        builder.Validate();

        builder.BuildTransitions()[0].IsInternal.Should().BeTrue();
    }
}
