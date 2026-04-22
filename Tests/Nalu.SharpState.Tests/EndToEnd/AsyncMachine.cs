namespace Nalu.SharpState.Tests.EndToEnd;

public class InspectContext
{
    public int Inspections { get; set; }
}

[StateMachineDefinition(typeof(InspectContext), Async = true)]
public partial class AsyncMachine
{
    [StateTriggerDefinition] static partial void Inspect();

    [StateTriggerDefinition] static partial void Finish();

    [StateDefinition]
    private static IStateConfiguration Idle => ConfigureState()
                                               .OnInspect(t => t
                                                               .Stay()
                                                               .InvokeAsync(ctx =>
                                                               {
                                                                   ctx.Inspections++;
                                                                   return default;
                                                               }))
                                               .OnFinish(t => t.Target(State.Done));

    [StateDefinition]
    private static IStateConfiguration Done => ConfigureState();
}
