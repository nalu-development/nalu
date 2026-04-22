namespace Nalu.SharpState.Tests.EndToEnd;

public class NetworkContext
{
    public List<string> Log { get; } = [];
}

[StateMachineDefinition(typeof(NetworkContext))]
public partial class NetworkMachine
{
    [StateTriggerDefinition] static partial void Connect();

    [StateTriggerDefinition] static partial void Disconnect();

    [StateTriggerDefinition] static partial void AuthOk();

    [StateTriggerDefinition] static partial void Message(string text);

    [StateTriggerDefinition] static partial void StartEdit();

    [StateTriggerDefinition] static partial void Save();

    [StateDefinition]
    private static IStateConfiguration Idle => ConfigureState()
        .OnConnect(t => t.Target(State.Connected));

    [StateDefinition]
    private static IStateConfiguration Connected => ConfigureState()
        .OnDisconnect(t => t.Target(State.Idle));

    [SubStateMachine(parent: State.Connected, initial: State.Authenticating)]
    private partial class ConnectedRegion
    {
        [StateDefinition]
        private static IStateConfiguration Authenticating => ConfigureState()
            .OnAuthOk(t => t.Target(State.Authenticated));

        [StateDefinition]
        private static IStateConfiguration Authenticated => ConfigureState()
            .OnMessage(t => t.Stay().Invoke((ctx, text) => ctx.Log.Add(text)));

        [SubStateMachine(parent: State.Authenticated, initial: State.Browsing)]
        private partial class AuthenticatedRegion
        {
            [StateDefinition]
            private static IStateConfiguration Browsing => ConfigureState()
                .OnStartEdit(t => t.Target(State.Editing));

            [StateDefinition]
            private static IStateConfiguration Editing => ConfigureState()
                .OnSave(t => t.Target(State.Browsing));
        }
    }
}
