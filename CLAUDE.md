# Nalu.Maui — AI agent guide

Nalu.Maui is a set of .NET MAUI libraries: `Core`, `Navigation`, `Layouts`, `Controls`, `VirtualScroll` (see `Source/`).
Solution file: `Nalu.slnx` (XML solution format). Packable subset: `Nalu.Pack.slnf`. Unit tests: `Tests/Nalu.Maui.Test`.

## UI testing architecture (DevFlow)

UI tests live in `UITests/UITests.DevFlow` (xUnit v3, `net10.0`) and drive the **Nalu.Maui.TestApp**
(`Samples/Nalu.Maui.TestApp`) through the **DevFlow** in-app agent (`Microsoft.Maui.DevFlow.Agent`,
activated in DEBUG builds in `MauiProgram.cs`, default port **9223**).

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
   - iOS simulator: `dotnet build Samples/Nalu.Maui.TestApp -f net10.0-ios "-t:Build;Run"`
   - Mac Catalyst: `dotnet build Samples/Nalu.Maui.TestApp -f net10.0-maccatalyst "-t:Build;Run"`
   - Android emulator: `dotnet build Samples/Nalu.Maui.TestApp -f net10.0-android "-t:Build;Run"`, then **`adb reverse tcp:9223 tcp:9223`**

   Quote the argument: an unquoted `;` is a command separator in bash/zsh.
   And note `-t:Run` alone *replaces* the default `Build` target, so on a clean `bin/` it fails with
   `MSB3073 ... Nalu.Maui.TestApp.app couldn't be opened because there is no such file`.
   Use `"-t:Build;Run"` (or build once without `-t`, then `-t:Run` to relaunch an unchanged app).
   (the `maui` CLI from dotnet tools also offers device/emulator management: `dotnet tool run maui -- --help`)
2. Use the DevFlow MCP tools (or `dotnet tool run maui -- devflow ...` CLI) to explore the running app:
   screenshot, visual tree, tap, assert. Verify manually that the scenario you are about to encode actually works.
3. Add/extend the test page in the TestApp if needed; keep pages minimal and deterministic.
4. Write the test in `UITests/UITests.DevFlow/Tests/` using the `NaluApp` wrapper (extend the wrapper rather than
   calling `AgentClient` from tests).
5. Run `dotnet test UITests/UITests.DevFlow` (the app must already be running; `DEVFLOW_HOST`/`DEVFLOW_PORT`
   override the default `localhost:9223`).
6. On failure: take a screenshot + visual tree via MCP, diagnose, fix (test, page, or library), repeat.

### Current status / open points

- Windows support in DevFlow is still partial; Windows UI tests are postponed.
- Tests currently assume a single app instance per platform at a time (fixed port 9223).
- CI integration is deliberately postponed; tests run locally only.
- Old Appium-based UITests were removed (July 2026) in favor of this setup.

## General conventions

- `LangVersion=preview`, nullable enabled, warnings as errors in `Source/` (relaxed in Samples/UITests).
- Library MAUI floor versions stay LOW on purpose (`MauiVersion9`/`MauiVersion10` in root `Directory.Build.props`) —
  do not bump them for consumers; apps/tests may use newer MAUI patch versions.
- Unit tests: `dotnet test Tests/Nalu.Maui.Test` (or `dotnet cake --target=Test`).
- Docs are built with docfx from `conceptual_docs/`.
