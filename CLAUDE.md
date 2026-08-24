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
dotnet test Tests/Nalu.Maui.Test -p:AllTargetFrameworks=net10.0 -p:ScaffoldTargetFrameworks=net10.0 \
  -p:NaluMauiReactorTargetFrameworks=net10.0
dotnet build Samples/Nalu.Maui.TestApp -f net10.0 [same -p overrides] -p:TestAppTargetFrameworks=net10.0
```

The TestApp additionally needs `dotnet workload install maui-tizen android` (nuget.org serves the
packs). This compiles and runs the whole unit suite — including the headless MauiReactor adapter
tests — but NOT the DevFlow UI tests, which need a real app on a device/simulator.

## MauiReactor component pages

`Nalu.Maui.Navigation.MauiReactor` renders MauiReactor components into Nalu-navigable pages
(`UseMauiReactorComponents()` + `AddPage<TComponent>()`; the component is the lifecycle target).
Components are NOT auto-registered by the source-generated `AddPages()` — register them manually.
Harness: "Scaffold Reactor Tests" page (`Samples/Nalu.Maui.TestApp/Tests/ScaffoldReactorTests.cs`),
UI suite `UITests/UITests.DevFlow/Tests/ScaffoldReactorChromeTests.cs`.
