#pragma warning disable IDE0060
namespace Nalu.SharpState.Tests.EndToEnd;

public class DoorContext
{
    public int OpenCount { get; set; }

    public string? LastReason { get; set; }
}

[StateMachineDefinition(typeof(DoorContext))]
public partial class DoorMachine
{
    [StateTriggerDefinition] static partial void Open(string reason);

    [StateTriggerDefinition] static partial void Close();

    [StateDefinition]
    private static IStateConfiguration Closed => ConfigureState()
        .OnOpen(t => t
                     .Target(State.Opened)
                     .Invoke((ctx, reason) =>
                         {
                             ctx.OpenCount++;
                             ctx.LastReason = reason;
                         }
                     )
        );

    [StateDefinition]
    private static IStateConfiguration Opened => ConfigureState()
        .OnClose(t => t.Target(State.Closed));
}
