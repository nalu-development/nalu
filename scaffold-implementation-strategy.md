# Nalu.Maui.Scaffold — implementation strategy

> Status: **draft for review** — planning document, no implementation started.
> Targets: **Android + iOS only** (Windows/Mac Catalyst out of scope).
> Date: 2026-07-23

## 1. Goals

Build a complete replacement for MAUI `Shell` on mobile platforms that:

1. **Plugs into Nalu.Maui.Navigation unchanged** — same fluent API (`Navigation.Relative()/.Absolute()`),
   same lifecycle interfaces, guards, awaitable intents, DI scoping, and leak detection.
2. **Owns 100% of the chrome** — nav bar, tab bar, flyout are Nalu-drawn MAUI views (virtual views),
   fully customizable, consistent across platforms. No `UINavigationBar`, no `MaterialToolbar`.
3. **Ships modern transitions** — shared-element transitions with a cross-platform tag API,
   interactive/interruptible push-pop animations.
4. **Supports navigation-state snapshot & restore** — land exactly where you were after an app
   restart (DevEx first, Android process-death restoration later).
5. Users keep writing plain **`ContentPage`s**. The Scaffold draws chrome *around* pages;
   pages configure it via attached properties.

### Non-goals

- Replacing or wrapping MAUI Shell (Shell support in Nalu.Maui.Navigation stays as-is; Scaffold is an *alternative host*).
- Windows / Mac Catalyst support.
- View-state snapshotting (restore replays *navigation*, it never deserializes page UI state).
- URI-based routing as the primary API (deep links are a mapping layer on top of Nalu absolute navigation, P3).

---

## 2. Packaging & positioning

- New package **`Nalu.Maui.Scaffold`**, depends on `Nalu.Maui.Navigation` (and `Nalu.Maui.Core`).
- `Nalu.Maui.Navigation` keeps working with MAUI Shell exactly as today — existing users unaffected.
- The host-abstraction contracts (today's `IShellProxy` family) get promoted so both hosts implement them (see §4).
- Registration mirrors the existing pattern: `.UseNaluScaffold(...)` alongside `.UseNaluNavigation(...)`.

---

## 3. Object model

Hierarchy is congruent with the existing proxy tree (`IShellItemProxy` → `IShellSectionProxy` → `IShellContentProxy`),
so absolute routes keep their `//item/stack/root` three-segment semantics.

| Scaffold type | Shell analogue | Proxy contract | Role |
|---|---|---|---|
| `Scaffold` | `Shell` | `IShellProxy` (renamed, §4) | Root component, set as `Window.Page`. Owns flyouts, modal layer, transition engine. |
| `ScaffoldItem` *(abstract)* | `ShellItem` | `IShellItemProxy` | Base class for root destinations. Holds 1..N `ScaffoldStack`s. |
| `ScaffoldArea : ScaffoldItem` | plain `ShellItem` | — | No visible stack switcher. With one stack it's a plain page host. |
| `ScaffoldTabBar : ScaffoldItem` | `TabBar` | — | Renders a tab UI switching between its stacks. |
| `ScaffoldStack` | `ShellSection` | `IShellSectionProxy` | An independent navigation stack (preserved when switching away). |
| `ScaffoldRoot` | `ShellContent` | `IShellContentProxy` | Holds the root page of a stack (lazy created / destroyable). |

Design rules:

- **No implicit wrapping.** Unlike Shell (which silently wraps a `ContentPage` in
  `ShellContent`→`ShellSection`→`ShellItem`), the hierarchy is always explicit in XAML/C#.
  Ease-of-use comes from good defaults, not from magic tree rewriting.
  We may allow *terse* forms (e.g. a `ScaffoldArea` with a single inline `ScaffoldRoot` auto-creating
  its `ScaffoldStack`) only if implemented as **constructor-time composition, never runtime tree mutation** —
  to be validated in P1; if it complicates the mental model, drop it.
- `ScaffoldRoot` reuses the existing `Navigation.PageType` attached-property mechanism for page
  registration (DataTemplate + DI-scoped creation), minus the `NaluShell` parent-walk (see §4).
- `ScaffoldItem`/`ScaffoldStack`/`ScaffoldRoot` carry `SegmentName`, `Title`, `Icon` — the metadata
  the default tab bar / flyout templates render from.

Illustrative shape (API sketch, not final):

```xml
<nalu:Scaffold>
    <nalu:ScaffoldTabBar SegmentName="main">
        <nalu:ScaffoldStack SegmentName="home" Title="Home" Icon="{StaticResource HomeIcon}">
            <nalu:ScaffoldRoot SegmentName="feed" nalu:Navigation.PageType="pages:FeedPage" />
        </nalu:ScaffoldStack>
        <nalu:ScaffoldStack SegmentName="search" Title="Search" Icon="{StaticResource SearchIcon}">
            <nalu:ScaffoldRoot SegmentName="search" nalu:Navigation.PageType="pages:SearchPage" />
        </nalu:ScaffoldStack>
    </nalu:ScaffoldTabBar>
    <nalu:ScaffoldArea SegmentName="settings">
        <nalu:ScaffoldStack SegmentName="settings">
            <nalu:ScaffoldRoot SegmentName="settings" nalu:Navigation.PageType="pages:SettingsPage" />
        </nalu:ScaffoldStack>
    </nalu:ScaffoldArea>
</nalu:Scaffold>
```

---

## 4. Integration with Nalu.Maui.Navigation

The navigation engine (`NavigationService`) already talks exclusively to `IShellProxy` and friends.
Required engine-side changes are small and contained:

1. **Promote & rename the host contracts.** `IShellProxy` / `IShellItemProxy` / `IShellSectionProxy` /
   `IShellContentProxy` + `NavigationStackPage` move from `internal` to `public` under host-neutral names
   (proposal: `INavigationHost`, `INavigationHostItem`, `INavigationHostStack`, `INavigationHostRoot`).
   The Shell implementations keep implementing them; naming stays Shell-flavored only in the Shell adapter.
2. **Remove the two `NaluShell` assumptions:**
   - `NavigationService.ShellProxy` getter (throws "You must use NaluShell") → resolve the host via an
     interface lookup on `Window.Page` / an ambient registration.
   - `Navigation.PageType` DataTemplate walking `Parent.Parent.Parent` expecting `NaluShell` →
     walk to the nearest `INavigationHost`-providing ancestor.
3. **Extend `INavigationInfo` with transition metadata** (see §8) — ignored by the Shell host.
4. **What the Scaffold host deletes** (Shell-adapter pain that must NOT leak into the contracts):
   `GoToAsync` URI marshalling + `?nalu` marker, `OnNavigating` cancel-and-redispatch,
   `Routing.RegisterRoute` global table, reflection into `ShellContent.ContentCache`,
   the `Task.Delay(500)` animation-settling hack. The Scaffold implements the contracts by
   **direct stack manipulation** and awaits its own animations deterministically.

Contract obligations the Scaffold must honor (the engine relies on these):

- `BeginNavigation` / `ProposeNavigation` / `CommitNavigationAsync` batching semantics.
- `GetNavigationStack` / `RemoveStackPages` including **modal stack** representation.
- `GetOrCreateContent` / `DestroyContent` lazy lifecycle (feeds the leak detector).
- `SendNavigationLifecycleEvent` telemetry passthrough.
- Change notification when current item/stack/root changes (engine watches structure).

---

## 5. Chrome (all Nalu-drawn)

### 5.1 Why fully owned (decided)

- Android/iOS native nav bars have incompatible height/content constraints (iOS is severely limited).
- iOS long-press-back multi-pop menu bypasses navigation guards → **will not exist** (decided: not reimplemented either).
- Native swipe/predictive back is hard to reconcile with async guards (§6).
- Owned chrome = virtual views = trivially customizable, testable via DevFlow, consistent cross-platform.

### 5.2 Nav bar

- Drawn by the Scaffold above the page content area. Per-page configuration via attached properties
  (proposal): `Scaffold.Title`, `Scaffold.TitleView`, `Scaffold.NavBarVisible`, `Scaffold.ToolbarItems`,
  `Scaffold.BackButtonBehavior` (visibility/icon/text).
- **Deliberately minimal API in P1** (title, back button, toolbar items, title view). Search boxes,
  large-title collapsing behaviors etc. are explicitly post-v1 — this is where Shell replacements die.
- Safe-area / edge-to-edge handling reuses the patterns already built for the NaluTabBar renderers
  (scrim views, `AdditionalSafeAreaInsets` on iOS, insets layouts on Android), re-authored for the
  Scaffold container.

### 5.3 Tab bar

- `ScaffoldTabBar` ships with a **default Nalu template** that auto-renders its `ScaffoldStack`s from
  `Title`/`Icon` (NaluTabBar's visual featureset is the starting point: shapes, blur, shadow, scroll padding…).
- **Full replacement supported**: user provides their own virtual view (DataTemplate or direct view);
  the Scaffold supplies a binding context exposing the stacks, selected index, and a select command.
  Tab selection routes through `NavigationService` (guards respected) — never a direct view swap.
- Tapping the active tab pops that stack to root (existing NaluTabBar behavior, preserved).
- Current `NaluTabBar` + its Shell renderers stay in `Nalu.Maui.Navigation` for Shell users;
  the Scaffold version is a fresh implementation without renderer gymnastics (it's just a view in the
  Scaffold's own layout).

### 5.4 Flyout(s) — "it's just a drawer"

- **Two drawers: `Start` and `End`** (logical directions, RTL-aware), independently configurable.
- Content model:
  - **Default template**: auto-renders the Scaffold's `ScaffoldItem`s (title/icon) as navigation entries;
    selection routes through `NavigationService`.
  - **Custom content**: any virtual view.
- **Resolution order for drawer content** (most specific wins):
  1. `Scaffold.FlyoutStart` / `Scaffold.FlyoutEnd` attached property on the **current Page**
  2. same attached property on the current **`ScaffoldItem`**
  3. **global** `Scaffold.FlyoutStart` / `FlyoutEnd` property on the Scaffold itself
  - `null` at all levels ⇒ that drawer doesn't exist; an explicit "none" sentinel lets a page suppress
    a globally-configured drawer.
- Behavior properties per drawer: mode (overlay for v1; locked/side-by-side is a tablet concern, post-v1),
  width, scrim, edge-swipe enable. Programmatic open/close via a small `IScaffoldDrawerController`
  (resolvable from page DI scope).
- Open question to settle in design review: can a flyout item target a specific *stack* inside an item
  (Shell allows it, complicates selection semantics) — **proposal: item-level targets only for v1**.

---

## 6. Back handling, gestures & guards

Single policy, enforced because we own every entry point:

| Back trigger | Behavior |
|---|---|
| Nav-bar back button | Routes through `Navigation.Relative().Pop()` → guards run normally. |
| Android hardware/system back | Intercepted (`OnBackPressedDispatcher`), routed through Pop → guards run. |
| Android predictive back (preview animation) | **Enabled only when the current page has no guard** (`HasGuard` is known synchronously). Guarded page ⇒ gesture registered as non-predictive back → guard runs on commit. |
| iOS interactive edge-swipe | Implemented by our transition engine (percent-driven). **Disabled when the current page has a guard** (decided). |
| iOS long-press back menu | Does not exist (no UINavigationBar). Not reimplemented (decided). |

Notes:

- "Guarded ⇒ no interactive gesture" is v1 policy; a later option is drag-then-confirm-at-release,
  the engine design (seekable animations, §8) keeps that door open.
- Tab/flyout selection also routes through the engine, so cross-stack guards keep working exactly as today.

---

## 7. Modals & sheets

- The Scaffold owns modal presentation (today's Shell adapter piggybacks on `Shell.GetPresentationMode`
  + `ShellSection.Navigation.ModalStack` — that contract surface must be reimplemented natively).
- Presentation modes: full-screen modal, **sheet with detents** (`UISheetPresentationController` on iOS,
  bottom-sheet behavior on Android) as a first-class mode Shell never offered.
- Modal pages are part of the navigation stack model (engine already handles modal popping);
  presentation mode is a per-page attached property or a push-time option — decide in design review.
- MAUI's own `Navigation.PushModalAsync` on the page: out of scope / unsupported inside Scaffold
  (document it; all navigation goes through Nalu).

---

## 8. Transition engine

### 8.1 Decision status

Custom Nalu-owned engine vs native bindings ⇒ **decided by PoC** (see 8.4). Hero binding is **dropped**
(unmaintained upstream, Swift→ObjC→C# binding chain to own forever). `matchedGeometryEffect` was ruled
out earlier (SwiftUI-only, cannot apply to UIKit-rendered MAUI content).

Key facts anchoring the decision:

- Snapshot-clone-and-animate is what Hero and Android SET do internally; per-frame interpolation runs
  on the native layer in *all* candidate approaches (`UIViewPropertyAnimator`/Core Animation,
  `ViewOverlay` + native animators). There is **no performance tier we lose** by owning orchestration;
  C# only computes start/end geometry once per transition.
- On Android, because the Scaffold owns the container and both pages are views in the same tree,
  we can drive **`androidx.transition` directly** (`ChangeBounds`/`ChangeTransform`/`ChangeImageTransform`)
  — the native SET machinery **without adopting Fragments**.
- On iOS there is no such shortcut ⇒ custom snapshot engine is the candidate
  (with iOS 18 `UIViewController.Transition.zoom` as a possible opportunistic extra, post-v1).

### 8.2 Cross-platform API (independent of engine choice)

- `Scaffold.TransitionTag="photo-{id}"` attached property on any `View` (the `transitionName` analogue).
  Matching tags on outgoing/incoming pages animate automatically.
- Per-navigation configuration in the fluent builder — transition choice is a navigation concern,
  carried in `INavigationInfo`: `Navigation.Relative().Push<DetailPageModel>().WithTransition(...)`.
- Page/Scaffold-level defaults: platform-default push/pop, slide, fade, none; hook for fully custom transitions.
- Restore (§9) and programmatic bulk navigations run with transitions suppressed.

### 8.3 Known-hard problems (what the PoC must exercise)

1. **Incoming-page readiness**: end-frames don't exist until the target page is measured/laid out —
   engine needs a "wait for layout of tagged views" phase with a timeout fallback (cross-fade).
2. **Image morphing** where aspect/clipping differs between pages (`ChangeImageTransform` territory).
3. **Text size changes** — industry answer is cross-fade, not glyph morphing; confirm it looks right.
4. **Interruption/reversal** — animations must be seekable/reversible from day one
   (prerequisite for interactive pop and Android predictive back; retrofit ≈ rewrite).

### 8.4 PoC plan (next concrete step)

Fixed scenario, implemented once per spike: **photo grid → detail page**; one image morph (aspect
change included) + one title element; push and interactive (percent-driven) pop.

| Spike | Approach | Success gate |
|---|---|---|
| **A — iOS custom** | Snapshot + `UIViewPropertyAnimator`, orchestrated from C#, zero dependencies | 60fps; correct image morph; animation seekable & reversible |
| **B — Android `androidx.transition`** | `ChangeBounds`/`ChangeTransform`/`ChangeImageTransform` on plain views in a shared container, no Fragments | Same gates + verify seekability (androidx `Transition` seeking exists since transition 1.5/predictive-back APIs — verify it fits, else fall back to custom `ValueAnimator` orchestration with the same technique as iOS) |

Evaluation criteria: frame rate, interruptibility/seekability, image-morph fidelity, LOC owned, API fit
with §8.2. Build both spikes inside **Nalu.Maui.TestApp** as test pages; capture runs with DevFlow
recording (`maui_recording_start`) for side-by-side comparison.

---

## 9. Navigation-state snapshot & restore

### 9.1 Mechanism

- **Capture**: on every successful `CommitNavigationAsync`, serialize:
  current item/stack/root segments + the ordered push stack (segment names) + per-page intent payloads
  (+ modal stack). Written async, cheap JSON, to app cache.
- **Invalidation key**: app version + hash of the registered route table (page renames/removals
  invalidate the snapshot instead of crashing the replay).
- **Restore**: at startup (opt-in), replay as absolute navigation with **animations suppressed** and
  **`IgnoreGuards`**. `OnEnteringAsync` re-runs naturally — pages re-fetch data; we restore *location*,
  not stale state.
- **Fail-open, always**: any exception during replay ⇒ discard snapshot, boot to default root.
  Restore must never be able to brick startup.
- **Truncation**: a page whose intent can't round-trip breaks the chain at that point —
  restore lands N−1 levels deep rather than failing entirely.
- **Scoping**: DEBUG-only by default (DevEx: restart and land where you were). Production use
  (Android process-death restoration) is the same mechanism, enabled deliberately later.

### 9.2 Intent serializability design

Question raised: `ISerializableIntent` with `string Serialize()` + default interface method using
System.Text.Json?

**Serialization is the easy half — the design constraint is *deserialization*:** the framework must
reconstruct a **concrete type** from a payload, so it needs (a) a durable type identity in the snapshot
and (b) a way to construct the instance. A `Serialize()` instance method alone can't provide either.

Proposed design:

```
// Opt-in marker. Default path: System.Text.Json round-trip of the concrete type.
public interface ISerializableIntent;

// Escape hatch for custom wire formats / non-STJ-friendly types.
public interface ICustomSerializableIntent : ISerializableIntent
{
    string Serialize();
    static abstract object Deserialize(string payload);   // C# 11 static abstract
}
```

- Snapshot stores `{ typeId, payload }` per intent. `typeId` is a **registered stable name**
  (registration derived from the existing `AddPage<,>()` configuration or an explicit
  `AddIntent<T>()`), *not* an assembly-qualified type name — renames/refactors then only invalidate,
  never deserialize the wrong thing.
- Default path (`ISerializableIntent` only): STJ serialize/deserialize of the concrete type.
  Records with init-only/positional properties work out of the box.
- Custom path: `ICustomSerializableIntent` for full control (invoked via a generic-constrained
  helper so the static abstract resolves without reflection).
- **Why not a DIM `Serialize()` on the base interface**: a default interface method body can do the STJ
  call, but it buys nothing — the framework can call STJ itself when no custom implementation exists,
  and DIMs complicate the AOT/trimming story for zero gain. Keep the marker empty.
- **AOT/trimming caveat**: STJ reflection-based serialization works under iOS Mono AOT today but is
  hostile to trimming/NativeAOT. Design the pipeline around an injectable `IIntentSerializer`
  (default = STJ reflection; overridable with an STJ **source-gen `JsonSerializerContext`**) so
  trimming-safe operation is a configuration, not a redesign.
- Non-serializable intents (no marker) are simply not captured ⇒ truncation rule above applies.

---

## 10. Lifecycle fidelity (invisible plumbing Shell did for us)

The Scaffold must own, with exact ordering:

- `Page.SendAppearing` / `SendDisappearing` for stack navigation, tab/area switches, modal present/dismiss,
  app sleep/resume — consistent with what the engine's `IAppearingAware`/`IDisappearingAware` dispatch expects
  (Nalu lifecycle events remain the primary API; MAUI page events must simply not lie).
- Handler connect/disconnect on push/pop/destroy (`DisconnectHandlerHelper` path) — **leak-detector
  compatibility is the acceptance test**: `DestroyContent`/pop must actually free pages.
- DI scope disposal ordering (`PageNavigationContext`) unchanged.
- Window/host integration: `Scaffold` sits as `Window.Page`; app backgrounding, theme change, safe-area
  change, and keyboard insets must propagate to the visible page and chrome.

---

## 11. Testing strategy

- Every behavior lands with a **TestApp page** (`Samples/Nalu.Maui.TestApp/Tests/`, `[TestPage]`)
  and a **DevFlow UI test** (`UITests/UITests.DevFlow`, via the `NaluApp` wrapper — extend the wrapper,
  never call `AgentClient` from tests). See the `maui-devflow-uitests` skill.
- **P0 exit criterion**: the existing `NavigationTests` suite passes against a Scaffold-hosted TestApp
  variant (proves the host seam holds).
- Transitions: DevFlow recording for visual verification; assertions on end-state + lifecycle event
  sequences (the `NavigationEvent` telemetry hook gives deterministic assertions where pixels can't).
- Leak detector runs in TestApp (existing `LeakTracker`) — every pop/destroy path asserted.
- Restore: kill-and-relaunch test flow (DevFlow can restart the app; assert landing location + intents).

---

## 12. Phasing

### P0 — seam spike (de-risk, throwaway-quality allowed)
- Contracts promoted/renamed in Nalu.Maui.Navigation; the two `NaluShell` couplings removed; Shell host still green.
- Bare `Scaffold`: one `ScaffoldArea`/one stack, push/pop with a simple slide, modal push.
- Correct Appearing/Disappearing, DI scopes, handler disconnect, leak detector.
- **Exit**: existing DevFlow `NavigationTests` pass on the Scaffold-hosted TestApp.

### P1 — structure & chrome
- Full hierarchy: `ScaffoldItem` base, `ScaffoldTabBar` (default template + custom view replacement,
  stack preservation, active-tab-pops-to-root), `ScaffoldArea`, cross-item/stack navigation.
- Start/End flyouts with resolution order (Page → Item → global), default template + custom content.
- Minimal nav bar (title, TitleView, back button, toolbar items, visibility), safe-area/edge-to-edge.
- Back policy per §6 (system back interception; no gestures yet).
- **Exit**: a real-world sample app shape (tabs + drawer + modals) fully navigable with guards.

### P2 — transitions & gestures
- PoC spikes A + B → engine decision → implement engine + `TransitionTag` API + `WithTransition(...)`.
- Platform-parity push/pop animations, then shared elements, then interactive pop (iOS) and
  predictive back (Android), honoring the guard policy.
- Sheets with detents.
- **Exit**: photo-grid→detail scenario shipping quality on both platforms, interruptible.

### P3 — restore, deep links, polish
- Snapshot/restore per §9 (DEBUG DevEx first).
- Deep-link mapping layer (URI → `INavigationInfo`).
- iOS 18 zoom-transition opportunism (optional), docs (docfx conceptual), migration guide from NaluShell.
- **Exit**: docs published; TestApp/UITest coverage for every §5–§9 behavior.

---

## 13. Risks

| Risk | Mitigation |
|---|---|
| Nav-bar scope creep (where Shell replacements die) | Hard-minimal P1 API; everything else post-v1 by decree. |
| Lifecycle fidelity bugs (appearing order, handler disconnect, keyboard/insets) | P0 exit tied to existing test suite + leak detector; DevFlow tests per behavior. |
| Seekable-animation requirement discovered late | Baked into engine design from PoC gates (retrofit ≈ rewrite). |
| `androidx.transition` seeking insufficient for predictive back | PoC B explicitly verifies; fallback = custom `ValueAnimator` orchestration (same technique as iOS spike). |
| STJ under trimming/NativeAOT | `IIntentSerializer` injection point + source-gen context option from day one. |
| Contract promotion breaks Shell host subtly | Shell host kept green in CI/unit tests throughout P0. |
| Two tab bars to maintain (Shell NaluTabBar + Scaffold) | Accepted short-term; Shell variant is feature-frozen once Scaffold ships. |

---

## 14. Open questions (settle during design review)

1. Final public names for the promoted contracts (`INavigationHost*` proposal in §4).
2. Terse XAML forms for single-stack areas — allowed (constructor-time composition only) or rejected?
3. Flyout items targeting a specific stack inside an item — v1 proposal is **no** (item-level only).
4. Modal/sheet presentation: per-page attached property vs push-time builder option (or both).
5. Drawer "locked/side-by-side" mode — post-v1, but does the v1 API shape need to reserve room for it?
6. Snapshot storage location & retention policy (cache dir, single slot vs per-build slot).
7. Does `Scaffold` need a Shell-style "current page changed" public event surface beyond the existing
   `NavigationEvent` telemetry? (Consumers may want it for analytics.)
