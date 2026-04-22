using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class HierarchyTests
{
    private static StateMachineDefinition<TestContext, HierState, HierTrigger> BuildHier(
        Action<IDictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger>>> setup)
    {
        var map = new Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger>>
        {
            [HierState.Idle] = new(),
            [HierState.Connected] = new(),
            [HierState.Authenticating] = new(),
            [HierState.Authenticated] = new(),
            [HierState.Outside] = new()
        };
        setup(map);
        var readonlyMap = map.ToDictionary(
            kv => kv.Key,
            kv => (IStateConfiguration<TestContext, HierState, HierTrigger>) kv.Value);
        return new StateMachineDefinition<TestContext, HierState, HierTrigger>(readonlyMap);
    }

    private static StateMachineDefinition<TestContext, HierState, HierTrigger> StandardHierarchy()
        => BuildHier(map =>
        {
            map[HierState.Idle].On(
                HierTrigger.Connect,
                TestTransition.ToTarget<TestContext, HierState>(HierState.Connected));
            map[HierState.Connected]
                .AsStateMachine(HierState.Authenticating)
                .On(HierTrigger.Disconnect, TestTransition.ToTarget<TestContext, HierState>(HierState.Idle));
            map[HierState.Authenticating]
                .Parent(HierState.Connected)
                .On(HierTrigger.AuthOk, TestTransition.ToTarget<TestContext, HierState>(HierState.Authenticated));
            map[HierState.Authenticated]
                .Parent(HierState.Connected)
                .On(HierTrigger.Message, TestTransition.Stay<TestContext, HierState>(syncAction: (ctx, args) => ctx.Log.Add((string) args[0]!)));
            map[HierState.Outside].On(
                HierTrigger.GoOutside,
                TestTransition.ToTarget<TestContext, HierState>(HierState.Outside));
        });

    [Fact]
    public void Targeting_composite_drills_to_initial_child()
    {
        var definition = StandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger>(definition, HierState.Idle, new TestContext());

        engine.Fire(HierTrigger.Connect, []);

        engine.CurrentState.Should().Be(HierState.Authenticating);
    }

    [Fact]
    public void Composite_is_entered_as_initial_child_when_used_as_starting_state()
    {
        var definition = StandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger>(definition, HierState.Connected, new TestContext());
        engine.CurrentState.Should().Be(HierState.Authenticating);
    }

    [Fact]
    public void Child_inherits_parent_transition_when_not_overridden()
    {
        var definition = StandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger>(definition, HierState.Authenticated, new TestContext());

        engine.Fire(HierTrigger.Disconnect, []);

        engine.CurrentState.Should().Be(HierState.Idle);
    }

    [Fact]
    public void Child_overrides_parent_transition()
    {
        var definition = BuildHier(map =>
        {
            map[HierState.Idle].On(HierTrigger.Connect, TestTransition.ToTarget<TestContext, HierState>(HierState.Connected));
            map[HierState.Connected]
                .AsStateMachine(HierState.Authenticating)
                .On(HierTrigger.Disconnect, TestTransition.ToTarget<TestContext, HierState>(HierState.Idle));
            map[HierState.Authenticating]
                .Parent(HierState.Connected)
                .On(HierTrigger.Disconnect, TestTransition.ToTarget<TestContext, HierState>(HierState.Outside));
            map[HierState.Authenticated].Parent(HierState.Connected);
            map[HierState.Outside].On(HierTrigger.GoOutside, TestTransition.ToTarget<TestContext, HierState>(HierState.Outside));
        });

        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger>(definition, HierState.Authenticating, new TestContext());
        engine.Fire(HierTrigger.Disconnect, []);
        engine.CurrentState.Should().Be(HierState.Outside);
    }

    [Fact]
    public void IsIn_true_for_composite_ancestor_and_leaf()
    {
        var definition = StandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger>(definition, HierState.Authenticated, new TestContext());

        engine.IsIn(HierState.Authenticated).Should().BeTrue();
        engine.IsIn(HierState.Connected).Should().BeTrue();
        engine.IsIn(HierState.Idle).Should().BeFalse();
    }

    [Fact]
    public void Stay_inside_composite_does_not_change_state()
    {
        var definition = StandardHierarchy();
        var ctx = new TestContext();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger>(definition, HierState.Authenticated, ctx);

        engine.Fire(HierTrigger.Message, ["hi"]);

        engine.CurrentState.Should().Be(HierState.Authenticated);
        ctx.Log.Should().Equal("hi");
    }

    [Fact]
    public void Cross_hierarchy_transition_resets_to_leaf()
    {
        var definition = StandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger>(definition, HierState.Authenticated, new TestContext());

        engine.Fire(HierTrigger.Disconnect, []);

        engine.CurrentState.Should().Be(HierState.Idle);
        engine.IsIn(HierState.Connected).Should().BeFalse();
    }
}
