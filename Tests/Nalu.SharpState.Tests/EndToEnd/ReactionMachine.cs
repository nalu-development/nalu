namespace Nalu.SharpState.Tests.EndToEnd;

public class InspectContext
{
    public int Inspections { get; set; }
}

[StateMachineDefinition(typeof(InspectContext))]
public partial class ReactionMachine
{
    [StateTriggerDefinition] static partial void Inspect();

    [StateTriggerDefinition] static partial void Finish();

    [StateDefinition(Initial = true)]
    private static IStateConfiguration Idle => ConfigureState()
        .OnInspect(t => t
            .Stay()
            .ReactAsync((actor, ctx) =>
            {
                ctx.Inspections++;
                actor.Finish();
                return default;
            }))
        .OnFinish(t => t
            .Target(State.Done)
            .ReactAsync((_, ctx) =>
            {
                ctx.Inspections += 10;
                return default;
            }));

    [StateDefinition]
    private static IStateConfiguration Done => ConfigureState();
}
