---
name: maui-devflow-uitests
description: >
  How to write, run and debug automated UI tests for the Nalu.Maui component libraries using
  .NET MAUI DevFlow (in-app agent + Driver + CLI + MCP). Use this skill whenever the task involves
  UI tests, UITests.DevFlow, the Nalu.Maui.TestApp, DevFlow, AgentClient, the `maui devflow` CLI,
  MCP-driven app automation (screenshots, visual tree, tap/fill/scroll), adding a test page for a
  component (VirtualScroll, Magnet, ExpanderViewBox, DurationWheel, tab bar…), or diagnosing a
  failing/flaky UI test — even if the user just says "add tests for component X" or "the UI test
  is red" without naming DevFlow.
---

# MAUI DevFlow + UI tests (Nalu.Maui)

## What DevFlow is (30 seconds)

DevFlow is the experimental testing/automation toolkit for .NET MAUI 10, born as Redth's
**MauiDevFlow** and now maintained in **dotnet/maui-labs** (`Microsoft.Maui.DevFlow.*` packages).
Three pieces cooperate:

1. **In-app agent** (`Microsoft.Maui.DevFlow.Agent`) — an HTTP server *inside* the app process
   (default port **9223**) exposing the real MAUI visual tree, screenshots, interactions, logs.
   Activated in DEBUG only, in `Samples/Nalu.Maui.TestApp/MauiProgram.cs`.
2. **Driver** (`Microsoft.Maui.DevFlow.Driver`, class `AgentClient`) — .NET client used by our
   xUnit tests in `UITests/UITests.DevFlow`.
3. **CLI + MCP** (`Microsoft.Maui.Cli`, command `maui`) — `maui devflow …` commands and
   `maui devflow mcp` (~67 tools: `maui_screenshot`, `maui_tree`, `maui_tap`, `maui_assert`, …)
   configured in `.mcp.json`, giving AI agents eyes and hands on the running app.

Key advantage over Appium (which this repo abandoned): tests assert on **real MAUI elements and
properties** (e.g. which VirtualScroll items are materialized), not on the native accessibility
tree — and there is no external server/driver stack to babysit.

**It is an experimental preview.** Versions are pinned (csproj + dotnet-tools.json); API breaks
are expected between previews and must be absorbed ONLY in `UITests/UITests.DevFlow/Infrastructure/NaluApp.cs`.

## The dev loop (write → verify → repeat)

1. **Launch the TestApp** (DEBUG) on the target platform — see commands in
   `references/devflow-overview.md`. Android needs `adb reverse tcp:9223 tcp:9223`.
2. **Explore the running app** via MCP tools or `maui devflow` CLI: take a screenshot, dump the
   visual tree, tap around. Confirm the scenario works manually before encoding it in a test.
3. **Add/extend a test page** in `Samples/Nalu.Maui.TestApp/Tests/` (`[TestPage("Name")]`),
   minimal and deterministic, unique `AutomationId` on everything a test touches.
4. **Write the test** in `UITests/UITests.DevFlow/Tests/` using the `NaluApp` wrapper —
   never call `AgentClient` from a test; extend the wrapper instead.
5. **Run** `dotnet test UITests/UITests.DevFlow` (app must be running;
   `DEVFLOW_HOST`/`DEVFLOW_PORT` override `localhost:9223`).
6. **On failure**: screenshot + visual tree via MCP, read the wrapper's TimeoutException (it lists
   the AutomationIds actually present), fix test/page/library, repeat.

## Reference files — read the one you need

| File | Read it when |
|------|--------------|
| `references/devflow-overview.md` | Setting up DevFlow in an app, platform support/caveats, run commands, package/version matrix, history & links |
| `references/agentclient-api.md` | Touching `NaluApp.cs`, a Driver API broke, or you need a capability the wrapper doesn't expose yet (full `AgentClient` surface + `ElementInfo` model) |
| `references/cli-mcp.md` | Driving the app interactively via `maui devflow` CLI or MCP tools; configuring `.mcp.json` |
| `references/testing-patterns.md` | Writing tests or test pages: harness conventions, wrapper recipes, virtualization/scroll patterns, flakiness pitfalls |

## Non-negotiable conventions

- `NaluApp.cs` is the **only** file that uses `AgentClient` directly (preview-churn firewall).
- Every element a test needs gets a unique `AutomationId`; `ResetButton` and `TestPageRoot`
  are reserved by the harness (`TestPageDecorator` in the TestApp's `MainPage.cs`).
- Tests must pass on **iOS Simulator and Android emulator** (Mac Catalyst is the fast local
  loop; Windows is postponed — DevFlow support there is still partial).
- MAUI 10 only (DevFlow requires it); the TestApp uses MAUI 10.0.80. Never raise the *library*
  MAUI floor versions in the root `Directory.Build.props` for this.
- One app instance per port: fixed 9223 assumes a single running TestApp at a time.
