using FluentAssertions;

namespace Nalu.SharpState.Tests.EndToEnd;

public class EndToEndTests
{
    [Fact]
    public void Trigger_advances_state_and_runs_action_with_args()
    {
        var ctx = new DoorContext();
        var door = DoorMachine.CreateActor(ctx);

        door.Open("delivery");

        door.CurrentState.Should().Be(DoorMachine.State.Opened);
        ctx.OpenCount.Should().Be(1);
        ctx.LastReason.Should().Be("delivery");
    }

    [Fact]
    public void GetInitialState_returns_generated_root_initial_state()
    {
        DoorMachine.GetInitialState().Should().Be(DoorMachine.State.Closed);
        NetworkMachine.GetInitialState().Should().Be(NetworkMachine.State.Idle);
    }

    [Fact]
    public void ToDot_renders_guard_conditions_inside_trigger_labels()
    {
        var dot = DoorMachine.ToDot();

        dot.Should().Contain("label = \"DoorMachine\";");
        dot.Should().Contain("start [shape=Mdiamond,label=\"Closed\"];");
        dot.Should().NotContain("shape=rectangle,label=\"Closed\"");
        dot.Should().Contain("shape=rectangle,label=\"Opened\"");
        dot.Should().Contain(@"shape=ellipse,label=""Open\n[Not spying]""");
        dot.Should().Contain("start -> trigger_");
        dot.Should().Contain("-> start;");
    }

    [Fact]
    public void ToDot_renders_terminal_states_as_msquares()
    {
        var dot = ReactionMachine.ToDot();

        dot.Should().Contain("shape=Msquare,label=\"Done\"");
        dot.Should().NotContain("end [shape=Msquare]");
        dot.Should().NotContain("-> end;");
    }

    [Fact]
    public void ToDot_renders_hierarchical_regions_as_clusters()
    {
        var dot = NetworkMachine.ToDot();

        dot.Should().Contain("compound = true;");
        dot.Should().Contain("start [shape=Mdiamond,label=\"Idle\"];");
        dot.Should().Contain("subgraph cluster_");
        dot.Should().Contain("label = \"Connected\";");
        dot.Should().Contain("label = \"Authenticated\";");
        dot.Should().NotContain("shape=rectangle,label=\"Idle\"");
        dot.Should().NotContain("shape=rectangle,label=\"Connected\"");
        dot.Should().NotContain("shape=rectangle,label=\"Authenticated\"");
        dot.Should().Contain("shape=Mdiamond,label=\"Authenticating\"");
        dot.Should().Contain("shape=Mdiamond,label=\"Browsing\"");
        dot.Should().Contain("-> start;");
        dot.Should().NotContain("label=\"Message\"");
        dot.Should().NotContain("end [shape=Msquare]");
    }

    [Fact]
    public void CanTrigger_methods_reflect_current_generated_actor_state()
    {
        var door = DoorMachine.CreateActor(DoorMachine.State.Closed, new DoorContext());

        door.CanOpen("delivery").Should().BeTrue();
        door.CanClose().Should().BeFalse();

        door.Open("delivery");

        door.CanOpen("again").Should().BeFalse();
        door.CanClose().Should().BeTrue();
    }

    [Fact]
    public void Close_transitions_back_and_StateChanged_fires()
    {
        var door = DoorMachine.CreateActor(DoorMachine.State.Opened, new DoorContext());
        (DoorMachine.State from, DoorMachine.State to, DoorMachine.Trigger trigger, object?[] args)? captured = null;
        door.StateChanged += (f, t, tr, args) => captured = (f, t, tr, args);

        door.Close();

        door.CurrentState.Should().Be(DoorMachine.State.Closed);
        captured.Should().NotBeNull();
        captured!.Value.from.Should().Be(DoorMachine.State.Opened);
        captured.Value.to.Should().Be(DoorMachine.State.Closed);
        captured.Value.trigger.Should().Be(DoorMachine.Trigger.Close);
        captured.Value.args.Should().BeEmpty();
    }

    [Fact]
    public void ReactAsync_runs_after_trigger_returns()
    {
        var ctx = new InspectContext();
        var machine = ReactionMachine.CreateActor(ReactionMachine.State.Idle, ctx);
        var syncContext = new RecordingSynchronizationContext();

        RunOn(syncContext, machine.Inspect);

        machine.CurrentState.Should().Be(ReactionMachine.State.Idle);
        ctx.Inspections.Should().Be(0);
        syncContext.Drain();
        machine.CurrentState.Should().Be(ReactionMachine.State.Done);
        ctx.Inspections.Should().Be(11);
    }

    [Fact]
    public void ReactAsync_target_transition_commits_before_reaction()
    {
        var ctx = new InspectContext();
        var machine = ReactionMachine.CreateActor(ReactionMachine.State.Idle, ctx);
        var syncContext = new RecordingSynchronizationContext();

        RunOn(syncContext, machine.Finish);

        machine.CurrentState.Should().Be(ReactionMachine.State.Done);
        ctx.Inspections.Should().Be(0);
        syncContext.Drain();
        ctx.Inspections.Should().Be(10);
    }

    [Fact]
    public void OnUnhandled_override_captures_unhandled_triggers()
    {
        var door = DoorMachine.CreateActor(DoorMachine.State.Opened, new DoorContext());
        (DoorMachine.State state, DoorMachine.Trigger trigger, object?[] args)? captured = null;
        door.OnUnhandled = (s, t, a) => captured = (s, t, a);

        door.Open("ignored");

        captured.Should().NotBeNull();
        captured!.Value.state.Should().Be(DoorMachine.State.Opened);
        captured.Value.trigger.Should().Be(DoorMachine.Trigger.Open);
        captured.Value.args.Should().Equal("ignored");
        door.CurrentState.Should().Be(DoorMachine.State.Opened);
    }

    [Fact]
    public void Hierarchical_targeting_composite_resolves_initial_child()
    {
        var machine = NetworkMachine.CreateActor(NetworkMachine.State.Idle, new NetworkContext());

        machine.Connect();

        machine.CurrentState.Should().Be(NetworkMachine.State.Authenticating);
        machine.IsIn(NetworkMachine.State.Connected).Should().BeTrue();
    }

    [Fact]
    public void Hierarchical_child_inherits_parent_transitions()
    {
        var machine = NetworkMachine.CreateActor(NetworkMachine.State.Authenticated, new NetworkContext());

        machine.Disconnect();

        machine.CurrentState.Should().Be(NetworkMachine.State.Idle);
        machine.IsIn(NetworkMachine.State.Connected).Should().BeFalse();
    }

    [Fact]
    public void IsIn_returns_true_for_ancestor_states()
    {
        var machine = NetworkMachine.CreateActor(NetworkMachine.State.Authenticated, new NetworkContext());

        machine.IsIn(NetworkMachine.State.Authenticated).Should().BeTrue();
        machine.IsIn(NetworkMachine.State.Connected).Should().BeTrue();
        machine.IsIn(NetworkMachine.State.Idle).Should().BeFalse();
    }

    [Fact]
    public void Internal_transition_runs_action_without_changing_state()
    {
        var ctx = new NetworkContext();
        // Entering Authenticated resolves to its initial leaf (Browsing).
        var machine = NetworkMachine.CreateActor(NetworkMachine.State.Authenticated, ctx);

        machine.Message("hello");

        machine.CurrentState.Should().Be(NetworkMachine.State.Browsing);
        machine.IsIn(NetworkMachine.State.Authenticated).Should().BeTrue();
        ctx.Log.Should().Equal("hello");
    }

    [Fact]
    public void Targeting_outer_composite_lands_on_deepest_initial_leaf()
    {
        var machine = NetworkMachine.CreateActor(NetworkMachine.State.Idle, new NetworkContext());

        machine.Connect();

        // Connect targets Connected -> resolves to Authenticating (no deeper initial).
        machine.CurrentState.Should().Be(NetworkMachine.State.Authenticating);
        machine.IsIn(NetworkMachine.State.Connected).Should().BeTrue();
    }

    [Fact]
    public void Targeting_composite_with_nested_initial_resolves_to_deepest_leaf()
    {
        var machine = NetworkMachine.CreateActor(NetworkMachine.State.Authenticating, new NetworkContext());

        machine.AuthOk();

        // AuthOk targets Authenticated -> which has initial Browsing.
        machine.CurrentState.Should().Be(NetworkMachine.State.Browsing);
        machine.IsIn(NetworkMachine.State.Authenticated).Should().BeTrue();
        machine.IsIn(NetworkMachine.State.Connected).Should().BeTrue();
    }

    [Fact]
    public void Deep_leaf_inherits_outermost_ancestor_transitions()
    {
        var machine = NetworkMachine.CreateActor(NetworkMachine.State.Editing, new NetworkContext());

        machine.Disconnect();

        machine.CurrentState.Should().Be(NetworkMachine.State.Idle);
        machine.IsIn(NetworkMachine.State.Connected).Should().BeFalse();
    }

    [Fact]
    public void Entry_and_exit_hooks_run_for_generated_machine()
    {
        var ctx = new HookContext();
        var machine = HookMachine.CreateActor(HookMachine.State.Idle, ctx);

        machine.Start();

        machine.CurrentState.Should().Be(HookMachine.State.Running);
        ctx.Log.Should().Equal("exit:Idle", "enter:Running");
    }

    [Fact]
    public void Ignore_syntax_sugar_keeps_current_state_without_unhandled()
    {
        var ctx = new HookContext();
        var machine = HookMachine.CreateActor(HookMachine.State.Running, ctx);

        var act = () => machine.Ping();

        act.Should().NotThrow();
        machine.CurrentState.Should().Be(HookMachine.State.Running);
        ctx.Log.Should().BeEmpty();
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
