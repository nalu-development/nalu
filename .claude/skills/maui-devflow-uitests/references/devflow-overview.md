# DevFlow overview: setup, platforms, versions

## History & sources (as of July 2026)

- Born as **Redth/MauiDevFlow** (archived) → moved into **dotnet/maui-labs** (`src/DevFlow`)
  as "DevFlow", packages renamed `Microsoft.Maui.DevFlow.*`.
- Official docs: Microsoft Learn → .NET MAUI 10 → Developer tools → DevFlow
  (https://learn.microsoft.com/en-us/dotnet/maui/developer-tools/devflow/?view=net-maui-10.0).
- Protocol spec: `docs/DevFlow/spec` in dotnet/maui-labs (REST `/api/v1/*`, WebSocket `/ws/v1/*`,
  app extensions at `/api/v1/ext/{namespace}/...`, discovery via `GET /api/v1/agent/capabilities`).
- Status: **experimental**, "will change between releases". Stable releases on nuget.org,
  nightlies on the `dotnet10` Azure DevOps feed.

## Packages & pinned versions

| Package | Role | Version used here |
|---|---|---|
| `Microsoft.Maui.DevFlow.Agent` | In-app HTTP agent (referenced by TestApp) | `0.1.0-preview.12.26358.3` |
| `Microsoft.Maui.DevFlow.Agent.Core` | Platform-agnostic core (transitive) | — |
| `Microsoft.Maui.DevFlow.Driver` | .NET client (`AgentClient`) used by UITests.DevFlow | `0.1.0-preview.12.26358.3` |
| `Microsoft.Maui.DevFlow.Blazor` | CDP bridge for Blazor Hybrid (not used here) | — |
| `Microsoft.Maui.DevFlow.Logging` | JSONL buffered logger (not used here) | — |
| `Microsoft.Maui.Cli` (dotnet tool, command `maui`) | CLI + MCP server | `0.1.0-preview.12.26358.3` (dotnet-tools.json) |

Requirements: **.NET MAUI 10 only** (Agent preview.12 depends on `Microsoft.Maui.Controls >= 10.0.20`,
`Microsoft.Maui.Core >= 10.0.41`; TestApp uses **10.0.80**). Agent TFMs: net10.0-android36.0,
net10.0-ios26.0, net10.0-maccatalyst26.0, net10.0-macos26.0, net10.0-windows10.0.19041.
Driver targets net9.0+ (plain .NET — our test project is `net10.0`).

When bumping the preview: change the version in `Samples/Nalu.Maui.TestApp/Nalu.Maui.TestApp.csproj`,
`UITests/UITests.DevFlow/UITests.DevFlow.csproj` and `dotnet-tools.json` together, then compile
`UITests.DevFlow` and fix `NaluApp.cs` if the Driver surface changed.

## Platform support (July 2026)

| Platform | Agent support | Notes |
|---|---|---|
| Mac Catalyst | ✅ full | Direct localhost. Requires `com.apple.security.network.server` entitlement (already added to TestApp) |
| iOS Simulator | ✅ full | Simulator shares the host network — direct localhost |
| Android emulator | ✅ full | Requires `adb forward tcp:9223 tcp:9223` after each app deploy/emulator boot |
| Linux/GTK | ✅ | Not relevant for Nalu today |
| Windows | ⚠️ partial / in progress | Windows UI tests postponed in this repo |

## App-side setup (already done in TestApp — template for other apps)

```xml
<PackageReference Include="Microsoft.Maui.DevFlow.Agent" Version="0.1.0-preview.12.26358.3" />
```

```csharp
#if DEBUG
using Microsoft.Maui.DevFlow.Agent;
#endif
// in CreateMauiApp():
#if DEBUG
builder.AddMauiDevFlowAgent();
#endif
```

The package reference is unconditional (NuGet restore is configuration-independent); the
`#if DEBUG` guard on the registration keeps the agent out of Release behavior.

## Running the TestApp per platform

```bash
# Mac Catalyst — fastest local loop
dotnet build Samples/Nalu.Maui.TestApp -f net10.0-maccatalyst -t:Run

# iOS Simulator
dotnet build Samples/Nalu.Maui.TestApp -f net10.0-ios -t:Run

# Android emulator (start emulator first; `maui` CLI can manage devices)
dotnet build Samples/Nalu.Maui.TestApp -f net10.0-android -t:Run
adb forward tcp:9223 tcp:9223
```

The `maui` CLI also offers environment/device management: `dotnet tool run maui -- --help`.

## Known caveats

- **API churn**: previews break source compatibility; that's why every Driver usage is confined
  to `NaluApp.cs` and versions are pinned.
- **Port model**: agent defaults to 9223; a broker daemon coordinates ports when multiple
  agent-enabled apps run at once. Our tests assume a single TestApp on 9223
  (`DEVFLOW_HOST` / `DEVFLOW_PORT` env vars override).
- **CI**: not officially documented for DevFlow yet. This repo runs UI tests locally only
  (deliberate choice, July 2026). The agent is plain HTTP, so CI is feasible later
  (macOS runner + simulator, or Linux runner + Android emulator + adb reverse).
- **In-process semantics**: taps/scrolls are dispatched by the agent inside the app, not
  synthesized by the OS. For OS-level input fidelity (keyboard chrome, system gestures),
  verify manually or consider platform-specific checks.
