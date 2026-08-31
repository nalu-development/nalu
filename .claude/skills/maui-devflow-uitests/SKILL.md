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
   exposing the real MAUI visual tree, screenshots, interactions, logs. Activated in DEBUG only,
   in `Samples/Nalu.Maui.TestApp/MauiProgram.cs`, with **per-platform ports** (also in the
   DailyHelper): **Android 9223** (reached via `adb forward tcp:9223 tcp:9223`),
   **iOS simulator 9224**, **Mac Catalyst 9225**. The simulator and Catalyst bind the HOST
   loopback directly, so distinct ports let emulator + simulator sessions run SIMULTANEOUSLY
   with no forward/terminate dance.
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
   `references/devflow-overview.md`. Android needs `adb forward tcp:9223 tcp:9223`.

   **iOS simulator rebuild/relaunch loop (the ONLY reliable sequence):**

   1. `xcrun simctl terminate booted com.nalu.maui.testapp` — ALWAYS kill the app first.
      Deploying over a *running* app silently keeps the STALE bundle (the trimmer's
      `obj/.../linked/` cache + install skip): your code changes never reach the device and
      nothing errors. When in doubt, verify the deployed dll timestamp:
      `ls -la "$(xcrun simctl get_app_container booted com.nalu.maui.testapp app)"/<Library>.dll`.
   2. `dotnet build Samples/Nalu.Maui.TestApp -f net10.0-ios` — plain build first. Running
      `-t:Run` in the same invocation right after wiping `bin/` fails with
      "The app must be built before the arguments to launch the app using mlaunch can be computed".
   3. `dotnet build Samples/Nalu.Maui.TestApp -f net10.0-ios -t:Run` — **run in background**
      (it can block while the app runs). Alternative that avoids the blocking `-t:Run`
      entirely: plain build, then `xcrun simctl install booted <path-to>.app` +
      `xcrun simctl launch booted com.nalu.maui.testapp`.
      The agent then comes up on its platform port (iOS 9224) — or **+1000** (10224) if the
      previous instance's port lingers in TIME_WAIT; probe with a no-op MCP call, never curl.

   **Physical iOS device loop** (needed for background-NSUrlSession suites): build with
   `-p:RuntimeIdentifier=ios-arm64`, deploy with `xcrun devicectl device install app --device <udid> <.app>`
   and `xcrun devicectl device process launch --terminate-existing --device <udid> com.nalu.maui.testapp`.
   The agent binds LOOPBACK on devices, so forward it over USB: `iproxy 9224 9224 -u <device-udid>`
   (libimobiledevice), then `DEVFLOW_HOST=localhost DEVFLOW_PORT=9224 dotnet test …`. Expired Xcode-managed
   provisioning: build any stub Xcode project with the same bundle id using
   `xcodebuild … -allowProvisioningUpdates` to regenerate the profile headlessly.

   **Android rebuild/deploy trap**: Debug builds use FAST DEPLOYMENT — assemblies are NOT in
   the APK. `adb install <apk>` runs the app with STALE assemblies (or breaks launch after an
   uninstall); always deploy with `dotnet build -f net10.0-android -t:Install` (or `-t:Run`).

   **Multiple agents / MCP targeting**: with apps on several platforms running at once, MCP
   tools error with "Multiple MAUI DevFlow agents are connected" and list the ports — pass
   `agentPort` (or terminate the apps you are not driving). The MCP broker also latches onto
   whatever app is up: force-stop other agent-enabled apps (e.g. the DailyHelper) when it
   grabs the wrong one.
2. **Explore the running app** via MCP tools or `maui devflow` CLI: take a screenshot, dump the
   visual tree, tap around. Confirm the scenario works manually before encoding it in a test.
3. **Add/extend a test page** in `Samples/Nalu.Maui.TestApp/Tests/` (`[TestPage("Name")]`),
   minimal and deterministic, unique `AutomationId` on everything a test touches.
4. **Write the test** in `UITests/UITests.DevFlow/Tests/` using the `NaluApp` wrapper —
   never call `AgentClient` from a test; extend the wrapper instead.
5. **Run** `dotnet test UITests/UITests.DevFlow` (app must be running; `NaluApp` self-discovers
   the agent on 9223/9224/9225 then the +1000 fallbacks). With BOTH platforms running, target
   deterministically: `DEVFLOW_PORT=9223 dotnet test …` (Android) / `DEVFLOW_PORT=9224 …` (iOS)
   — back-to-back cross-platform runs need no relaunching. After a relaunch, wait for
   readiness with a no-op MCP call (e.g. `maui_query`) retried until it responds —
   never curl-probe ports.
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
- One app instance per PLATFORM at a time (Android 9223 / iOS 9224 / Catalyst 9225); different
  platforms can run simultaneously thanks to the per-platform ports.
- **Agent taps are in-process and reach UNPRESENTED elements**: overlay/flyout content exists
  as a logical child even while closed (query shows `windowBounds` width/height −1), and
  `maui_tap`/`TapAsync` still fires its handlers. Never use the overlay's own content as the
  presented-state witness — wait on a presented-only element (e.g. the scrim automation id,
  `ScaffoldFlyoutScrim`) instead.
- Scaffold drawer modes default to **Disabled**: a harness scaffold must call
  `SetFlyoutStartMode(this, ScaffoldFlyoutMode.Flyout)` or `OpenAsync` silently no-ops.
- For pixel-truth verification the agent cannot see (system bar icons, animation smoothness):
  read platform state in-app into a probe Label (e.g. iOS `StatusBarManager.StatusBarStyle`,
  Android `WindowInsetsControllerCompat.AppearanceLightStatusBars`), or record the screen
  (`xcrun simctl io booted recordVideo` / `adb shell screenrecord`) and extract frames with
  `ffmpeg -vf fps=60`; measure regions with `signalstats` YAVG instead of eyeballing.
