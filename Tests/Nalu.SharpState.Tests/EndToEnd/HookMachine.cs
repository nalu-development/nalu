namespace Nalu.SharpState.Tests.EndToEnd;

public class HookContext
{
    public List<string> Log { get; } = [];
}

[StateMachineDefinition(typeof(HookContext))]
public partial class HookMachine
{
    [StateTriggerDefinition] static partial void Start();

    [StateTriggerDefinition] static partial void Ping();

    [StateDefinition]
    private static IStateConfiguration Idle => ConfigureState()
        .OnExit(ctx => ctx.Log.Add("exit:Idle"))
        .OnStart(t => t.Target(State.Running));

    [StateDefinition]
    private static IStateConfiguration Running => ConfigureState()
        .OnEntry(ctx => ctx.Log.Add("enter:Running"))
        .OnPing(t => t.Ignore());
}
