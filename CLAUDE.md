# Nalu.Maui — AI agent guide

Nalu.Maui is a set of .NET MAUI libraries: `Core`, `Navigation`, `Layouts`, `Controls`, `VirtualScroll` (see `Source/`).
Solution file: `Nalu.slnx` (XML solution format). Packable subset: `Nalu.Pack.slnf`. Unit tests: `Tests/Nalu.Maui.Test`.

> **Skill available**: the `maui-devflow-uitests` skill (`.claude/skills/maui-devflow-uitests/`)
> contains the full DevFlow reference (AgentClient API, CLI/MCP usage, testing patterns).
> Consult it whenever writing or debugging UI tests.

## UI testing architecture (DevFlow)

UI tests live in `UITests/UITests.DevFlow` (xUnit v3, `net10.0`) and drive the **Nalu.Maui.TestApp**
(`Samples/Nalu.Maui.TestApp`) through the **DevFlow** in-app agent (`Microsoft.Maui.DevFlow.Agent`,
activated in DEBUG builds in `MauiProgram.cs`, per-platform ports: Android **9223**
(via `adb forward`), iOS simulator **9224**, Mac Catalyst **9225**).

- DevFlow is an **experimental preview** (dotnet/maui-labs). Versions are pinned in the csproj/tools files; bump them deliberately.
- `Infrastructure/NaluApp.cs` is the **only** file allowed to use the `AgentClient` Driver API directly.
  Tests use the `NaluApp` wrapper (WaitForElementAsync / TapAsync / FillAsync / OpenTestPageAsync / ResetAsync…).
  When a DevFlow preview breaks the API, fix `NaluApp.cs` only.
- The MCP server is configured in `.mcp.json` (`maui devflow mcp` via the local `microsoft.maui.cli` tool),
  giving AI agents screenshots, visual-tree queries, taps and assertions against the running app.
  Restore it with `dotnet tool restore`.

### TestApp harness conventions

- Each test page is a `Page` subclass in `Samples/Nalu.Maui.TestApp/Tests/` marked with `[TestPage("Some Name")]`.
- The app starts on `MainPage`: fill the `TestName` entry, tap `RunTestButton` to open a test page
  (this is what `NaluApp.OpenTestPageAsync` does).
- Every `ContentPage`-based test page (including pages pushed inside a `NavigationPage`) automatically gets a
  red `ResetButton` overlay (see `TestPageDecorator` in `MainPage.cs`) which returns to `MainPage`.
  `NaluApp.ResetAsync` relies on it — do NOT reuse the `ResetButton` / `TestPageRoot` AutomationIds.
- Give every element a test needs a unique `AutomationId`.

### Dev loop: writing and verifying a test autonomously

1. Build & launch the TestApp (DEBUG) on the target platform:
   - iOS simulator: `dotnet build Samples/Nalu.Maui.TestApp -f net10.0-ios -t:Run`
   - Mac Catalyst: `dotnet build Samples/Nalu.Maui.TestApp -f net10.0-maccatalyst -t:Run`
   - Android emulator: `dotnet build Samples/Nalu.Maui.TestApp -f net10.0-android -t:Run`, then **`adb forward tcp:9223 tcp:9223`**
   (the `maui` CLI from dotnet tools also offers device/emulator management: `dotnet tool run maui -- --help`)
2. Use the DevFlow MCP tools (or `dotnet tool run maui -- devflow ...` CLI) to explore the running app:
   screenshot, visual tree, tap, assert. Verify manually that the scenario you are about to encode actually works.
3. Add/extend the test page in the TestApp if needed; keep pages minimal and deterministic.
4. Write the test in `UITests/UITests.DevFlow/Tests/` using the `NaluApp` wrapper (extend the wrapper rather than
   calling `AgentClient` from tests).
5. Run `dotnet test UITests/UITests.DevFlow` (the app must already be running; `NaluApp`
   self-discovers 9223/9224/9225 (+1000 fallbacks) — set `DEVFLOW_PORT` to target one
   platform when apps on several platforms are running at once).
6. On failure: take a screenshot + visual tree via MCP, diagnose, fix (test, page, or library), repeat.

### Background-HTTP fault harness (iOS NSUrlSession)

Error handling of `NSUrlBackgroundSessionHttpMessageHandler` is covered by a three-part harness:

- **ChaosServer** (`Tools/Nalu.ChaosServer`): raw-socket HTTP server producing wire faults by path
  (`/truncate`, `/reset`, `/garbage`, `/stall`, `/drip`, `/redirect-loop`…). Hosted in-process by
  `ChaosServerFixture` for the UI tests (device reaches the Mac's LAN IP over shared Wi-Fi), or
  standalone: `dotnet run --project Tools/Nalu.ChaosServer`.
- **"Background Http Chaos"** TestApp page + `BackgroundHttpChaosUiTests`: the network-fault matrix.
  Encodes REAL background-session semantics: connection-level faults are silently RETRIED by
  nsurlsessiond until the 24h resource timeout (tests assert cancellable-never-succeeds + server-side
  retry hits); garbage bytes are delivered as an HTTP/0.9 200 body; only redirect loops fail fast (-1007).
- **"Background Http Callbacks"** TestApp page + `BackgroundHttpCallbackUiTests`: self-asserting
  callback-injection suite — invokes the delegate's session callbacks directly with fake
  `NSUrlSessionDownloadTask` subclasses (`NSObjectFlag.Empty`, overriding State/Error/Response/…)
  to deterministically reach every error branch: unexpected states, null descriptions, missing files,
  throwing getters, duplicates, lost-request flow, background-completion handler. Requires
  `InternalsVisibleTo("Nalu.Maui.TestApp")` on Core. Runs on simulator AND device.
  CAUTION: never synthesize `DidBecomeInvalid` on the live delegate — recreating the session while
  the old one is alive gives two sessions with one identifier, and on a device nsurlsessiond then
  wedges every later request into eternal "pending". Invalidate the REAL session
  (`InvalidateAndCancel`) so the callback arrives naturally (see the session-invalid-recovery scenario).
  Also covers the FILE-REMOVAL family (download file gone before staging, staged file racing the
  deferred processing, whole tmp-dir purge → recreate-and-recover, `.nsresponse` unlinked while the
  consumer reads — POSIX keeps the open stream alive, orphan sweep at delegate init, 8-way burst with
  one missing file) and duplicate-identifier ATTACH semantics (same identifier while in flight →
  returns the in-flight response; the duplicate's token cancels only its own wait).
- **"Background Http Lifecycle"** TestApp page + `BackgroundHttpLifecycleUiTests`: the same use
  cases across APP-LIFECYCLE transitions — the host backgrounds the app (foregrounds Settings),
  SIGKILLs it (crash-like: background tasks survive, unlike a user swipe-kill) and relaunches it
  via simctl/devicectl (`NaluApp.BackgroundAppAsync/ForegroundAppAsync/KillAppAsync/RelaunchAppAsync`).
  Covers: delayed responses and uploads completing across backgrounding, fail-fast faults surfacing
  after foreground, retrying faults persisting across backgrounding + cancel, kill+relaunch delivery
  through the lost-message flow (including a mid-flight download nsurlsessiond finishes for the dead
  app), and error completions after death being absorbed silently. Outcomes are read from the page's
  invariant labels; `BackgroundHttpLostResults` (static, per-process) accumulates lost deliveries in
  the relaunched process. Kill tests assert on the PER-KIND label (`LostByKindLabel`,
  "kind=ok/err/bytes"): kills make nsurlsessiond re-deliver a previous test's not-fully-acknowledged
  event into the next relaunch ("ghost" deliveries), so global lost counters are not stable.
  VERIFIED findings: an 8MB upload killed MID-BODY still completes and delivers (nsurlsessiond owns
  the serialized body); iOS IGNORES the per-request native timeout on background sessions, so
  `DefaultTimeout` is now enforced MANAGED-side (linked CTS; surfaces as TaskCanceledException
  wrapping TimeoutException, the HttpClient.Timeout convention).

**Physical-device loop** (background sessions are only meaningful on a real iPhone): build with
`dotnet build Samples/Nalu.Maui.TestApp -f net10.0-ios -p:RuntimeIdentifier=ios-arm64`, install/launch
via `xcrun devicectl device install app|process launch`, then reach the device agent over USB with
`iproxy 9224 9224 -u <device-udid>` (the agent binds loopback on devices) and run tests with
`DEVFLOW_HOST=localhost DEVFLOW_PORT=9224`. The lifecycle suite kills/relaunches the app, which can
land the agent on the +1000 fallback port — keep a second forward running: `iproxy 10224 10224`.
Expired team provisioning can be regenerated headlessly by building any stub Xcode project with the
same bundle id and `-allowProvisioningUpdates`.

### Current status / open points

- Windows support in DevFlow is still partial; Windows UI tests are postponed.
- Tests assume a single app instance per PLATFORM at a time (Android 9223 / iOS 9224 /
  Catalyst 9225); different platforms can run simultaneously.
- CI integration is deliberately postponed; tests run locally only.
- Old Appium-based UITests were removed (July 2026) in favor of this setup.

## General conventions

- `LangVersion=preview`, nullable enabled, warnings as errors in `Source/` (relaxed in Samples/UITests).
- Library MAUI floor versions live in root `Directory.Build.props`: `MauiVersion9` (9.0.80) and a single
  `MauiVersion10` (10.0.90) shared by EVERY net10 library — per-project floors only bought a lower number
  in exchange for NU1605 downgrade errors when two Nalu packages met in one app. Do not bump them casually
  for consumers; apps/tests use `MauiVersion` in `Samples/Directory.Build.props` (10.0.100).
- Unit tests: `dotnet test Tests/Nalu.Maui.Test` (or `dotnet cake --target=Test`).
- Docs are built with docfx from `conceptual_docs/`.

## Building on Linux / agent containers (no Apple workloads)

Apple workloads don't exist on Linux, so multi-TFM restore fails out of the box. Every hardcoded
TFM list is overridable; single-target everything to plain net10.0 with:

```
dotnet test Tests/Nalu.Maui.Test -p:AllTargetFrameworks=net10.0 -p:ScaffoldTargetFrameworks=net10.0
dotnet build Samples/Nalu.Maui.TestApp -f net10.0 [same -p overrides] -p:TestAppTargetFrameworks=net10.0
```

The TestApp additionally needs `dotnet workload install maui-tizen android` (nuget.org serves the
packs). This compiles and runs the whole unit suite — including the headless MauiReactor adapter
tests — but NOT the DevFlow UI tests, which need a real app on a device/simulator.

## MauiReactor component pages

Nalu ships NO MauiReactor package: the bridge is an app-side `IComponentPageFactory`
(canonical copy: `Samples/Nalu.Maui.TestApp/MauiReactorComponentPageFactory.cs`, registered
with `UseComponentPageFactory<T>()`; keep it in sync with conceptual_docs/navigation-mauireactor.md
and the copy in `Tests/Nalu.Maui.Test/NavigationTests/MauiReactorAdapterTests.cs`).
Components register via `AddPage<TComponent>()`; the component is the lifecycle target.
Page-rendering components opt into the source-generated `AddPages()` by decorating with
`[AutoNavigationPage]` (on non-Page classes the attribute is an opt-IN, on ContentPages it stays
the opt-OUT via `Enabled = false`); undecorated components are never auto-registered.
Harness: "Scaffold Reactor Tests" page (`Samples/Nalu.Maui.TestApp/Tests/ScaffoldReactorTests.cs`),
UI suite `UITests/UITests.DevFlow/Tests/ScaffoldReactorChromeTests.cs`.
