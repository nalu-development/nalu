namespace Nalu.SharpState.Tests.Runtime;

internal sealed class TestContext
{
    public int Counter { get; set; }

    public string? LastArg { get; set; }

    public List<string> Log { get; } = [];
}

internal enum FlatState
{
    A,
    B,
    C
}

internal enum FlatTrigger
{
    Go,
    Alt,
    NoMatch
}

internal enum HierState
{
    Idle,
    Connected,
    Authenticating,
    Authenticated,
    Outside
}

internal enum HierTrigger
{
    Connect,
    Disconnect,
    AuthOk,
    Message,
    GoOutside
}
