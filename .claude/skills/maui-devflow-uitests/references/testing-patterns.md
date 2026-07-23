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
- **Delta-scroll does NOT work on VirtualScroll** (its platform root is a container view, not the
  native scroll view; DevFlow reports success but nothing moves). Scroll with `SwipeAsync` or a
  page-side `ScrollTo` control instead.
- **Swipe direction semantics** (iOS, preview.12): vertical `"up"` scrolls FORWARD (reveals content
  below); horizontal `"right"` scrolls FORWARD (reveals content to the right).
- **Synthetic swipes have no touch physics**: they move the offset and raise `Scrolled`, but can
  NEVER trigger pull-to-refresh, carousel paging snap, drag&drop, or the native dragging
  callbacks behind `OnScrollStarted`/`OnScrollEnded`. Test those surfaces through page-side
  helpers (e.g. a button invoking `IVirtualScrollController.Refresh`, carousel `CurrentRange`
  buttons) and document the gesture path as not harness-testable.
- **Shell tab bars cannot be tapped natively**: DevFlow's synthetic `Tab` element "tap" sets
  `Shell.CurrentItem.CurrentItem = section` directly, bypassing the cancelable OnNavigating
  pipeline (Nalu never sees it — no lifecycle events, desynced state). For tab-switch tests attach
  **NaluTabBar** (`UseNaluTabBar()` + `NaluShell.SetTabBarView(tabBar, new NaluTabBar())`): its
  buttons are real MAUI views navigating via `Shell.GoToAsync` — the same cancelable path as a
  native tap. `NaluApp.TapByTextAsync` prefers visible matches and hit-tests ancestors when the
  matched Label's TapGestureRecognizer lives on an ancestor Border.
- **Nalu navigation tests must wait for `+A` log entries between navigations**: Nalu silently
  ignores a navigation requested while another is committing (`NavigationIgnored`), and page
  elements appear in the tree at `+E` (before commit) — tapping the next nav button that early is
  a silent no-op. Wait for the target page's appearing entry (sent post-commit) first.
- **Empty visual tree in every remaining test = the app crashed**: pull the managed stack with
  `xcrun simctl spawn <UDID> log show --last 10m --predicate 'processImagePath contains "<AppName>"'`
  and look for "Unhandled managed exception" (DEBUG FireAndForget rethrows dispatched-navigation
  failures and kills the app).
- **Removed/replaced cells LINGER in the visual tree** (recycler keeps detached views around).
  Never assert "element gone" after a removal — assert the layout shift of the survivors instead
  (e.g. `WaitForBoundsAsync("Item 2", b => b.Y == oldItem1.Y)`).
- `ScrollTo` to a **section header** settles asynchronously on iOS (two-phase scroll + offset
  fix-up ~100-300ms after the tap): assert with `WaitForBoundsAsync`, not an immediate read.
- After a swipe, the scroll may still be settling: before reading scroll-dependent state
  (visible range, counters), wait with `WaitForStableBoundsAsync`/`WaitForStableTextAsync`.
- The fading edge only fades edges that have scrollable content beyond them (no leading fade at
  offset 0). Pixel-assert it with `GetPixelColorAsync` near the trailing edge; element screenshots
  do capture the mask.
- Mutation-during-navigation regression harness: see `VirtualScrollPushMutationTests`
  (TestApp page pushes a VirtualScroll page while a UI-thread timer mutates the bound
  ObservableCollection through the push/pop animation; "Done N" label = survived).
  It includes a DynamicData variant (SourceCache → Group → inner Binds into
  ReadOnlyObservableCollections) whose >25-change Reset escalation reproduced a real
  `NSInternalInconsistencyException` ("invalid number of sections") — fixed by applying
  each adapter change set inside a single `PerformBatchUpdates` in
  `VirtualScrollPlatformDataSourceNotifier`. When testing collection sources, always
  include a scenario where a single changeset carries MANY changes (bursts + Reset).

## Android specifics (verified July 2026)

- Element screenshots re-draw the view offscreen and MISS composited effects (fading edges):
  `GetPixelColorAsync` samples the FULL screenshot at window coordinates for this reason.
- Swipes travel a shorter distance per gesture than on iOS: always use bounded
  swipe-until-visible loops, never a single swipe + assert.
- RecyclerView item animators (~250ms) race instantaneous bounds reads after add/move/insert:
  wait for the settled layout (e.g. `WaitForBoundsAsync("Item 2", b => b.Y == oldFirstY)`).
- Pull-to-refresh: `SwipeRefreshLayout` disables rather than detaches, and its OnRefreshListener
  cannot be invoked publicly — the refresh page's NativePull emulates the gesture
  (spinner + controller pipeline); "attached" in NativeStateLabel means "pull possible".
- After Android work, `adb forward --remove-all` before returning to the iOS simulator,
  or host 9223 keeps routing to the Android app.

## Layout assertions (Magnet, ViewBox, ExpanderViewBox…)

`ElementInfo.Bounds` (X/Y/Width/Height in MAUI coordinates) enables real layout tests:
constraint resolution, clipping sizes, expansion deltas. Compare with tolerance (±1 unit)
— platforms round differently. This largely replaces screenshot-based visual testing; if pixel
comparison ever returns, use `ScreenshotAsync` + SkiaSharp (Driver already ships SkiaSharp),
not Magick.NET.

## Flakiness checklist

Symptom → likely cause:
- Fails only on Android → forgot `adb forward tcp:9223 tcp:9223` (re-run after emulator restart).
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
- Readiness after (re)launching the app: do NOT curl-probe ports. The MCP server auto-discovers
  the agent — just issue a no-op MCP call (e.g. `maui_query` for `TestName`) and retry until it
  responds; no `agentPort` parameter needed. `NaluApp` likewise self-discovers 9223 → 10223
  (relaunch fallback when 9223 lingers in TIME_WAIT), so `dotnet test` needs no `DEVFLOW_PORT`
  unless the port is truly custom. If a raw probe is ever needed:
  `curl http://localhost:9223/api/v1/agent/capabilities` (the root `/` and `/json` return 404
  BY DESIGN and are not valid probes).
- The red dot bottom-right on test pages is the harness ResetButton, not a DevFlow indicator.

## Platform matrix discipline

Write tests platform-agnostic (the visual tree abstracts platforms). When behavior legitimately
differs per platform, branch on data from `GetPlatformInfoAsync` (expose via wrapper) rather than
compiling per-platform test assemblies — one test project serves all platforms; the platform is
chosen by which app you launch.
