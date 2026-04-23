using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class DefinitionValidationTests
{
    private static StateMachineDefinition<TestContext, HierState, HierTrigger, TestActor> Build(
        Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>> map)
    {
        var readonlyMap = map.ToDictionary(
            kv => kv.Key,
            kv => (IStateConfiguration<TestContext, HierState, HierTrigger, TestActor>) kv.Value);
        return new StateMachineDefinition<TestContext, HierState, HierTrigger, TestActor>(readonlyMap);
    }

    [Fact]
    public void Parent_without_matching_AsStateMachine_throws()
    {
        var map = new Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>>
        {
            [HierState.Idle] = new(),
            [HierState.Connected] = new(),
            [HierState.Authenticating] = new TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>().Parent(HierState.Connected),
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
        var map = new Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>>
        {
            [HierState.Idle] = new(),
            [HierState.Connected] = new TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>().AsStateMachine(HierState.Authenticating),
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
        var act = () => new TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>()
            .Parent(HierState.Connected)
            .Parent(HierState.Outside);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Double_AsStateMachine_throws()
    {
        var act = () => new TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>()
            .AsStateMachine(HierState.Authenticating)
            .AsStateMachine(HierState.Authenticated);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Parent_referring_to_unknown_state_throws()
    {
        var map = new Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>>
        {
            [HierState.Idle] = new TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>().Parent(HierState.Outside),
            [HierState.Connected] = new()
        };

        var act = () => Build(map);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cycle_in_state_hierarchy_throws()
    {
        var map = new Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>>
        {
            [HierState.Idle] = new TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>()
                .Parent(HierState.Outside)
                .AsStateMachine(HierState.Connected),
            [HierState.Connected] = new TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>()
                .Parent(HierState.Idle)
                .AsStateMachine(HierState.Authenticated),
            [HierState.Authenticating] = new(),
            [HierState.Authenticated] = new TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>()
                .Parent(HierState.Connected)
                .AsStateMachine(HierState.Outside),
            [HierState.Outside] = new TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>()
                .Parent(HierState.Authenticated)
                .AsStateMachine(HierState.Idle)
        };

        var act = () => Build(map);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cycle detected*");
    }

    [Fact]
    public void AncestorsOf_returns_immediate_parent_first()
    {
        var definition = HierarchyTests.CreateStandardHierarchy();

        definition.AncestorsOf(HierState.Authenticated)
            .Should()
            .Equal(HierState.Connected);
    }

    [Fact]
    public void LowestCommonAncestor_returns_nearest_common_composite()
    {
        var definition = HierarchyTests.CreateStandardHierarchy();

        definition.LowestCommonAncestor(HierState.Authenticating, HierState.Authenticated)
            .Should()
            .Be(HierState.Connected);
    }
}
