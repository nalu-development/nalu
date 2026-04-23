using BenchmarkDotNet.Attributes;

namespace Nalu.SharpState.Benchmarks;

[MemoryDiagnoser]
public class FlatActorBenchmarks
{
    private NoArgsMachine.IActor _noArgsActor = null!;
    private OneArgMachine.IActor _oneArgActor = null!;

    [GlobalSetup]
    public void Setup()
    {
        _noArgsActor = NoArgsMachine.CreateActor(NoArgsMachine.State.Idle, new CounterContext());
        _oneArgActor = OneArgMachine.CreateActor(OneArgMachine.State.Idle, new CounterContext());
    }

    [Benchmark]
    public void Fire_parameterless_ignore() => _noArgsActor.Ping();

    [Benchmark]
    public void Fire_single_argument_stay() => _oneArgActor.Record(42);
}

[MemoryDiagnoser]
public class HierarchicalActorBenchmarks
{
    private HierMachine.IActor _actor = null!;

    [GlobalSetup]
    public void Setup() => _actor = HierMachine.CreateActor(HierMachine.State.Running, new CounterContext());

    [Benchmark]
    public void Fire_parent_fallback_transition() => _actor.Reset();
}

public class CounterContext
{
    public int Counter { get; set; }
}

[StateMachineDefinition(typeof(CounterContext))]
public partial class NoArgsMachine
{
    [StateTriggerDefinition] static partial void Ping();

    [StateDefinition]
    private static IStateConfiguration Idle => ConfigureState()
        .OnPing(t => t.Ignore());
}

[StateMachineDefinition(typeof(CounterContext))]
public partial class OneArgMachine
{
    [StateTriggerDefinition] static partial void Record(int _);

    [StateDefinition]
    private static IStateConfiguration Idle => ConfigureState()
        .OnRecord(t => t.Stay().Invoke((ctx, recordedValue) => ctx.Counter = recordedValue));
}

[StateMachineDefinition(typeof(CounterContext))]
public partial class HierMachine
{
    [StateTriggerDefinition] static partial void Reset();

    [StateDefinition]
    private static IStateConfiguration Idle => ConfigureState();

    [StateDefinition]
    private static IStateConfiguration Running => ConfigureState()
        .OnReset(t => t.Target(State.Idle));

    [SubStateMachine(parent: State.Running, initial: State.Active)]
    private partial class RunningRegion
    {
        [StateDefinition]
        private static IStateConfiguration Active => ConfigureState();
    }
}
