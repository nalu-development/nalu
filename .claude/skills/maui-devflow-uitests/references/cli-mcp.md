# DevFlow CLI & MCP — driving the app interactively

Use the CLI/MCP for the *exploration and verification* half of the loop: see the app, poke it,
confirm behavior — then encode what you learned as an xUnit test. Repeatable tests belong in
`UITests/UITests.DevFlow`, not in CLI scripts.

## CLI (`maui devflow …`)

Installed as a repo-local dotnet tool (`dotnet tool restore`, then `dotnet tool run maui -- …`).
Command groups (from the DevFlow README/Learn docs):

| Group | Purpose |
|---|---|
| `ui` | visual tree inspection, screenshots, element interaction (tap/fill/scroll) |
| `recording` | screen recordings (start/stop) |
| `webview` | Blazor/WebView automation via CDP |
| `logs` | stream/read structured app logs |
| `network` | captured HTTP requests |
| `storage` | preferences / secure storage / app files |
| `agent` | agent discovery & status |
| `extensions` | app-registered custom tools |
| `broker` | port-broker daemon management (multiple apps) |
| `batch` | batched operations |
| `mcp` | start the MCP server |
| `init` | scaffold agent skills/config in a repo |

Examples seen in the docs (verify with `--help`, the CLI evolves):

```bash
dotnet tool run maui -- devflow --help
dotnet tool run maui -- devflow agent interact tap --automationid "RunTestButton"
dotnet tool run maui -- devflow MAUI screenshot -o shot.png
dotnet tool run maui -- devflow MAUI tree
```

## MCP server

`.mcp.json` (repo root) starts it for Claude Code automatically:

```json
{ "mcpServers": { "maui-devflow": {
    "type": "stdio", "command": "dotnet",
    "args": ["tool", "run", "maui", "--", "devflow", "mcp"] } } }
```

~67 tools, named `maui_*`. The ones that matter most for the test loop:

- `maui_screenshot` — PNG rendered inline; first thing to call when anything is unclear or red.
- `maui_tree` — visual tree as JSON with element ids, AutomationIds and bounds.
- `maui_tap`, `maui_fill`, `maui_scroll`, `maui_focus` — interactions by element id.
- `maui_assert` — verify an element property matches an expected value (quick checks without
  writing a test yet).
- log/network/storage/device tools mirror the CLI groups.

## Recommended interactive workflow

1. App running? `maui_screenshot`. Not reachable → is the TestApp actually launched (DEBUG)?
   Android: did you run `adb reverse tcp:9223 tcp:9223`?
2. Orient with `maui_tree`; find AutomationIds (harness ids: `TestName`, `RunTestButton`,
   `ResetButton`, plus the page's own ids).
3. Navigate: fill `TestName` with the `[TestPage]` name, tap `RunTestButton`.
4. Interact + `maui_assert` until the scenario is confirmed working.
5. Now write the xUnit test reproducing exactly those steps through `NaluApp`.
6. Screenshot again on each failure — never guess at UI state.

## Multiple apps / non-default port

The broker assigns ports when several agent-enabled apps run simultaneously
(`maui devflow broker …`, agent discovery via `maui devflow agent …`). MCP tools auto-discover
the running agent (omit `agentPort`; use a retried no-op `maui_query` as the readiness probe
after relaunching the app). The test suite's `NaluApp` self-discovers 9223 → 10223 (the
relaunch-fallback port); export `DEVFLOW_PORT` before `dotnet test` only for custom ports.
