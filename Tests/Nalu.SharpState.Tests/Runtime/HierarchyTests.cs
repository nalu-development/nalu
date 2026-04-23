using FluentAssertions;

namespace Nalu.SharpState.Tests.Runtime;

public class HierarchyTests
{
    private static StateMachineDefinition<TestContext, HierState, HierTrigger, TestActor> BuildHier(
        Action<IDictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>>> setup)
    {
        var map = new Dictionary<HierState, TestStateConfigurator<TestContext, HierState, HierTrigger, TestActor>>
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
            kv => (IStateConfiguration<TestContext, HierState, HierTrigger, TestActor>) kv.Value);
        return new StateMachineDefinition<TestContext, HierState, HierTrigger, TestActor>(readonlyMap);
    }

    internal static StateMachineDefinition<TestContext, HierState, HierTrigger, TestActor> CreateStandardHierarchy()
        => BuildHier(map =>
        {
            map[HierState.Idle].On(
                HierTrigger.Connect,
                TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Connected));
            map[HierState.Connected]
                .AsStateMachine(HierState.Authenticating)
                .On(HierTrigger.Disconnect, TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Idle));
            map[HierState.Authenticating]
                .Parent(HierState.Connected)
                .On(HierTrigger.AuthOk, TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Authenticated));
            map[HierState.Authenticated]
                .Parent(HierState.Connected)
                .On(HierTrigger.Message, TestTransition.Stay<TestContext, HierState, TestActor>(syncAction: (ctx, args) => ctx.Log.Add((string) args[0]!)));
            map[HierState.Outside].On(
                HierTrigger.GoOutside,
                TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Outside));
        });

    [Fact]
    public void Targeting_composite_drills_to_initial_child()
    {
        var definition = CreateStandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger, TestActor>(definition, HierState.Idle, new TestContext(), new TestActor());

        engine.Fire(HierTrigger.Connect, TriggerArgs.Empty);

        engine.CurrentState.Should().Be(HierState.Authenticating);
    }

    [Fact]
    public void Composite_is_entered_as_initial_child_when_used_as_starting_state()
    {
        var definition = CreateStandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger, TestActor>(definition, HierState.Connected, new TestContext(), new TestActor());
        engine.CurrentState.Should().Be(HierState.Authenticating);
    }

    [Fact]
    public void Child_inherits_parent_transition_when_not_overridden()
    {
        var definition = CreateStandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger, TestActor>(definition, HierState.Authenticated, new TestContext(), new TestActor());

        engine.Fire(HierTrigger.Disconnect, TriggerArgs.Empty);

        engine.CurrentState.Should().Be(HierState.Idle);
    }

    [Fact]
    public void Child_overrides_parent_transition()
    {
        var definition = BuildHier(map =>
        {
            map[HierState.Idle].On(HierTrigger.Connect, TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Connected));
            map[HierState.Connected]
                .AsStateMachine(HierState.Authenticating)
                .On(HierTrigger.Disconnect, TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Idle));
            map[HierState.Authenticating]
                .Parent(HierState.Connected)
                .On(HierTrigger.Disconnect, TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Outside));
            map[HierState.Authenticated].Parent(HierState.Connected);
            map[HierState.Outside].On(HierTrigger.GoOutside, TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Outside));
        });

        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger, TestActor>(definition, HierState.Authenticating, new TestContext(), new TestActor());
        engine.Fire(HierTrigger.Disconnect, TriggerArgs.Empty);
        engine.CurrentState.Should().Be(HierState.Outside);
    }

    [Fact]
    public void IsIn_true_for_composite_ancestor_and_leaf()
    {
        var definition = CreateStandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger, TestActor>(definition, HierState.Authenticated, new TestContext(), new TestActor());

        engine.IsIn(HierState.Authenticated).Should().BeTrue();
        engine.IsIn(HierState.Connected).Should().BeTrue();
        engine.IsIn(HierState.Idle).Should().BeFalse();
    }

    [Fact]
    public void Stay_inside_composite_does_not_change_state()
    {
        var definition = CreateStandardHierarchy();
        var ctx = new TestContext();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger, TestActor>(definition, HierState.Authenticated, ctx, new TestActor());

        engine.Fire(HierTrigger.Message, TriggerArgs.From("hi"));

        engine.CurrentState.Should().Be(HierState.Authenticated);
        ctx.Log.Should().Equal("hi");
    }

    [Fact]
    public void Cross_hierarchy_transition_resets_to_leaf()
    {
        var definition = CreateStandardHierarchy();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger, TestActor>(definition, HierState.Authenticated, new TestContext(), new TestActor());

        engine.Fire(HierTrigger.Disconnect, TriggerArgs.Empty);

        engine.CurrentState.Should().Be(HierState.Idle);
        engine.IsIn(HierState.Connected).Should().BeFalse();
    }

    [Fact]
    public void Exit_and_entry_actions_follow_hierarchy_path()
    {
        var definition = BuildHier(map =>
        {
            map[HierState.Idle].On(HierTrigger.Connect, TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Connected));
            map[HierState.Connected]
                .AsStateMachine(HierState.Authenticating)
                .WhenExiting(ctx => ctx.Log.Add("exit:Connected"))
                .On(HierTrigger.Disconnect, TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Idle));
            map[HierState.Authenticating]
                .Parent(HierState.Connected)
                .WhenExiting(ctx => ctx.Log.Add("exit:Authenticating"))
                .On(HierTrigger.AuthOk, TestTransition.ToTarget<TestContext, HierState, TestActor>(HierState.Authenticated));
            map[HierState.Authenticated]
                .Parent(HierState.Connected)
                .WhenEntering(ctx => ctx.Log.Add("enter:Authenticated"));
            map[HierState.Outside].WhenEntering(ctx => ctx.Log.Add("enter:Outside"));
        });

        var ctx = new TestContext();
        var engine = new StateMachineEngine<TestContext, HierState, HierTrigger, TestActor>(definition, HierState.Authenticating, ctx, new TestActor());

        engine.Fire(HierTrigger.AuthOk, TriggerArgs.Empty);

        ctx.Log.Should().Equal("exit:Authenticating", "enter:Authenticated");
    }
}
