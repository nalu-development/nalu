# Testing patterns for Nalu.Maui UI tests

## Harness anatomy (Nalu.Maui.TestApp)

- The app boots into `MainPage`: a title, a `TestName` entry, a `RunTestButton`, and a
  VirtualScroll listing every page marked `[TestPage("Some Name")]` (discovered via reflection,
  instantiated through DI with `ActivatorUtilities` — constructor-inject services freely).
- `TestPageItem.Open` swaps `Windows[0].Page` and runs `TestPageDecorator.Decorate`, which wraps
  every `ContentPage` (including pages pushed inside a `NavigationPage`) in a Grid overlay with a
  small red `ResetButton` (bottom-right) that returns to `MainPage`.
- Reserved AutomationIds: `TestName`, `RunTestButton`, `ResetButton`, `TestPageRoot`,
  `AppTitleLabel`. Never reuse them.
- Shell-based test pages are NOT decorated (no ResetButton): until an app-side reset action
  exists (see `InvokeActionAsync` in agentclient-api.md), such tests need an app restart per run
  — prefer ContentPage/NavigationPage harness pages when possible.

## Test project anatomy (UITests/UITests.DevFlow)

- xUnit v3, plain `net10.0`, Microsoft Testing Platform (`dotnet test UITests/UITests.DevFlow`).
- `NaluApp` is an **assembly fixture** (`[assembly: AssemblyFixture(typeof(NaluApp))]`):
  one connection per test run, injected into test class constructors. Test classes extend
  `BaseUiTest`.
- `NaluApp` is the ONLY place that touches `AgentClient`. Missing capability? Add a wrapper
  method, keep tests wrapper-only. This is the firewall against preview API churn.

## Recipe: add coverage for a component

1. **Test page** (`Samples/Nalu.Maui.TestApp/Tests/<Component>Tests.cs`):
   - `[TestPage("<Component> Tests")]` on a `Page` subclass (ContentPage or NavigationPage).
   - Deterministic content: fixed data, no timers/random/network. Expose knobs as UI controls
     with AutomationIds (see the VirtualScroll page: Position/Extra entries + Add/Remove/Move/
     ScrollTo buttons) so tests AND humans AND MCP agents can drive every scenario.
2. **Test class** (`UITests/UITests.DevFlow/Tests/<Component>Tests.cs`):

```csharp
public class ExpanderTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Expander Tests";

    [Fact]
    public async Task ExpandsWhenTapped()
    {
        await App.OpenTestPageAsync(PageName);          // reset + navigate, always first line
        await App.TapAsync("ExpandToggle");
        var content = await App.WaitForElementAsync("ExpandedContent");
        content.IsVisible.Should().BeTrue();
    }
}
```

3. Verify live via MCP before/while writing; run `dotnet test UITests/UITests.DevFlow`.

## Waiting: the golden rules

- Never `Task.Delay` for UI state — always `WaitForElementAsync` (polls the visual tree,
  and on timeout throws listing the AutomationIds actually present: read that list!).
- `WaitForElementOrDefaultAsync` when absence is a legal outcome (returns null, no throw).
- Asserting something does NOT exist: use a short timeout (1–2 s), and only after an anchor
  element of the same screen has appeared (otherwise you're asserting on the previous page).
- Property polling: element snapshots are point-in-time. Re-query (`FindElementAsync`) after an
  interaction rather than asserting on a stale `ElementInfo`.

## Virtualization patterns (VirtualScroll)

- Offscreen items/header/footer may not exist in the tree at all — that's virtualization working.
  Treat "not in tree" as "not materialized", not as a bug.
- Reaching an offscreen element: loop `ScrollAsync(deltaY: …)` + `WaitForElementOrDefaultAsync`
  with a bounded attempt count (see `FooterIsVisibleAfterScrollingToEnd`). Prefer
  `ScrollAsync(itemIndex: …)` once its semantics are verified on the installed preview.
- Materialization assertions (recycling checks) can count matching elements via the wrapper
  (e.g. query by type) — a capability worth adding to `NaluApp` when needed.

## Layout assertions (Magnet, ViewBox, ExpanderViewBox…)

`ElementInfo.Bounds` (X/Y/Width/Height in MAUI coordinates) enables real layout tests:
constraint resolution, clipping sizes, expansion deltas. Compare with tolerance (±1 unit)
— platforms round differently. This largely replaces screenshot-based visual testing; if pixel
comparison ever returns, use `ScreenshotAsync` + SkiaSharp (Driver already ships SkiaSharp),
not Magick.NET.

## Flakiness checklist

Symptom → likely cause:
- Fails only on Android → forgot `adb reverse tcp:9223 tcp:9223` (re-run after emulator restart).
- `InvalidOperationException: Cannot reach the DevFlow agent` → app not running / wrong port /
  Release build (agent is DEBUG-only).
- Element never appears but page looks right in screenshot → missing/duplicated `AutomationId`,
  or element virtualized out — check `maui_tree`.
- First test in a class flaky, rest fine → animation still running after navigation; wait for a
  stable anchor element instead of adding delays.
- Everything times out after a preview bump → Driver API changed; fix `NaluApp.cs` (compiler
  errors) and re-check endpoint behavior (runtime nulls/falses).
- Agent unreachable AND the red ResetButton overlay is missing on test pages → you are
  running a STALE binary (predates the DevFlow commits) or a Release build: `pkill -f
  Nalu.Maui.TestApp`, then rebuild with `"-t:Build;Run"` (plain `-t:Run` does NOT rebuild).
- Healthcheck: use `curl http://localhost:9223/api/v1/agent/capabilities` — the root `/`
  and `/json` return 404 BY DESIGN and are not valid probes.
- The red dot bottom-right on test pages is the harness ResetButton, not a DevFlow indicator.

## Platform matrix discipline

Write tests platform-agnostic (the visual tree abstracts platforms). When behavior legitimately
differs per platform, branch on data from `GetPlatformInfoAsync` (expose via wrapper) rather than
compiling per-platform test assemblies — one test project serves all platforms; the platform is
chosen by which app you launch.
