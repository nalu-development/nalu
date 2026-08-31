using System.Net;
using Nalu.Chaos;
using Xunit;

namespace Nalu.Maui.UITests.Infrastructure;

/// <summary>
/// Hosts the <see cref="ChaosServer" /> IN the test process: the device on the same Wi-Fi
/// reaches it at <see cref="BaseUrl" />, and tests assert on both sides of the wire — what the
/// app displayed and what the server actually received (<see cref="Server" />'s request log).
/// </summary>
/// <remarks>
/// The port is ephemeral (no TIME_WAIT clashes between test classes); the base URL uses this
/// machine's LAN IPv4, so the suite requires the Mac and the device to share a network —
/// <see cref="LanAddress" /> is null when there is none, and tests skip on it.
/// </remarks>
public sealed class ChaosServerFixture : IAsyncLifetime
{
    public ChaosServer Server { get; private set; } = null!;

    /// <summary>This machine's LAN IPv4, or null when the machine has no usable one.</summary>
    public IPAddress? LanAddress { get; private set; }

    /// <summary>Base URL the DEVICE uses to reach the server (LAN IP, http).</summary>
    public string BaseUrl => $"http://{LanAddress}:{Server.Port}";

    public ValueTask InitializeAsync()
    {
        Server = new ChaosServer();
        LanAddress = ChaosServer.GetLanAddress();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
        => await Server.DisposeAsync();
}
