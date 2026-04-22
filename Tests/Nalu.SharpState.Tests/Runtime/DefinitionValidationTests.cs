using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class DefinitionValidationTests
{
    private static StateMachineDefinition<TestContext, HierState, HierTrigger> Build(
        Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger>> map)
    {
        var readonlyMap = map.ToDictionary(
            kv => kv.Key,
            kv => (IStateConfiguration<TestContext, HierState, HierTrigger>) kv.Value);
        return new StateMachineDefinition<TestContext, HierState, HierTrigger>(readonlyMap);
    }

    [Fact]
    public void Parent_without_matching_AsStateMachine_throws()
    {
        var map = new Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger>>
        {
            [HierState.Idle] = new(),
            [HierState.Connected] = new(),
            [HierState.Authenticating] = new TestStateConfigurator<TestContext, HierState, HierTrigger>().Parent(HierState.Connected),
            [HierState.Authenticated] = new(),
            [HierState.Outside] = new()
        };

        var act = () => Build(map);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SubStateMachine*");
    }

    [Fact]
    public void AsStateMachine_whose_initial_child_does_not_claim_parent_throws()
    {
        var map = new Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger>>
        {
            [HierState.Idle] = new(),
            [HierState.Connected] = new TestStateConfigurator<TestContext, HierState, HierTrigger>().AsStateMachine(HierState.Authenticating),
            [HierState.Authenticating] = new(),
            [HierState.Authenticated] = new(),
            [HierState.Outside] = new()
        };

        var act = () => Build(map);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SubStateMachine*");
    }

    [Fact]
    public void Multi_parent_single_configurator_throws_on_second_Parent_call()
    {
        var act = () => new TestStateConfigurator<TestContext, HierState, HierTrigger>()
            .Parent(HierState.Connected)
            .Parent(HierState.Outside);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Double_AsStateMachine_throws()
    {
        var act = () => new TestStateConfigurator<TestContext, HierState, HierTrigger>()
            .AsStateMachine(HierState.Authenticating)
            .AsStateMachine(HierState.Authenticated);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parent_referring_to_unknown_state_throws()
    {
        var map = new Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger>>
        {
            [HierState.Idle] = new TestStateConfigurator<TestContext, HierState, HierTrigger>().Parent(HierState.Outside),
            [HierState.Connected] = new()
        };

        var act = () => Build(map);
        act.Should().Throw<InvalidOperationException>();
    }
}
