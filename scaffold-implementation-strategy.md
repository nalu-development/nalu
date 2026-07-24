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

**Two levels, not Shell's three** (decided): Shell's `ShellSection`/`ShellContent` split exists
solely to nest top tabs inside bottom tabs — a feature the Scaffold does not adopt (if a top-tabs
pattern is ever wanted, it's Nalu-drawn chrome inside a page, not navigation structure). A
`ScaffoldRoot` hosts its navigation stack directly; the Scaffold host synthesizes the
single content-level proxy per root internally, so the engine's three-level contracts
(`IShellItemProxy` → `IShellSectionProxy` → `IShellContentProxy`) work unchanged.
**No developer-facing routes or segment names** (decided): Nalu navigation is type-based; the
proxies' segment identifiers stay internal, derived from root page-type registrations (positional
disambiguation when the same page type roots multiple roots' stacks).

| Scaffold type | Shell analogue | Proxy contract | Role |
|---|---|---|---|
| `Scaffold` | `Shell` | `IShellProxy` (renamed, §4) | Root component, set as `Window.Page`. Owns flyouts, modal layer, transition engine. |
| `ScaffoldArea` | `ShellItem` | `IShellItemProxy` | Destination group holding 1..N `ScaffoldRoot`s; no visible root switcher (with one root it's a plain page host). `Scaffold.Areas` / read-only `Scaffold.CurrentArea`. |
| `ScaffoldTabBar : ScaffoldArea` | `TabBar` | — | Area specialization rendering a tab UI switching between its roots. |
| `ScaffoldRoot` | `ShellSection`+`ShellContent` | `IShellSectionProxy` (+ synthesized `IShellContentProxy`) | A root destination hosting an independent navigation stack rooted on `PageType` (lazy created / destroyable), preserved when switching away. `IsVisible` hides it from chrome only — the route stays navigable. |

Design rules:

- **No runtime tree rewriting** — but terse forms are supported (decided, implemented in the API
  skeleton): implicit conversion operators (the same mechanism Shell's terse XAML relies on, honored
  by XamlC and the runtime parser) compose the real wrapper once at parse time:
  `<ScaffoldRoot>` directly under `Scaffold` → single-root `ScaffoldArea`. Unlike Shell there is
  never post-parse mutation: the composed structure is what you get.
- `ScaffoldRoot` exposes a first-class `PageType` (registered via `AddPage`/`AddPages`,
  DI-scoped creation); its identity derives from that registration. `ScaffoldArea` and
  `ScaffoldRoot` carry `Title`/`Icon` — the metadata the default flyout / tab bar templates
  render from.

Illustrative shape (API sketch, not final):

```xml
<nalu:Scaffold>
    <nalu:ScaffoldTabBar>
        <nalu:ScaffoldRoot Title="Home" Icon="{StaticResource HomeIcon}" PageType="pages:FeedPage" />
        <nalu:ScaffoldRoot Title="Search" Icon="{StaticResource SearchIcon}" PageType="pages:SearchPage" />
    </nalu:ScaffoldTabBar>
    <!-- Terse form: wrapped into a single-root area at parse time -->
    <nalu:ScaffoldRoot PageType="pages:SettingsPage" />
</nalu:Scaffold>
```

---

## 4. Integration with Nalu.Maui.Navigation

The navigation engine (`NavigationService`) already talks exclusively to `IShellProxy` and friends.
Required engine-side changes are small and contained:

1. **Host contracts stay `internal`** (decided): `Nalu.Maui.Navigation` grants `InternalsVisibleTo`
   to `Nalu.Maui.Scaffold` (the repo's established pattern) and the Scaffold implements
   `IShellProxy` / `IShellItemProxy` / `IShellSectionProxy` / `IShellContentProxy` +
   `NavigationStackPage` as-is. No public surface is frozen, the contracts stay freely reshapeable
   (host-neutral renames optional, any time), and third-party custom hosts are deliberately not
   a feature — going public later remains a non-breaking option.
2. **The two suspected `NaluShell` couplings turned out to be non-issues** (verified July 2026,
   engine untouched):
   - The engine is initialized **by the host**: `NavigationService.InitializeAsync(IShellProxy, …)`
     is what `NaluShell` calls; the Scaffold calls the same method with its own proxy. The
     "You must use NaluShell" getter is only an uninitialized-state error message.
   - `Navigation.PageType` (and its `NaluShell` parent-walk) is inherently Shell-only — it attaches
     to `ShellContent`. The Scaffold never uses it: `ScaffoldRoot.PageType` is first-class and the
     Scaffold's proxies create pages via the engine's internal page factory directly.
   The Scaffold's `IShell*` implementations are **written from scratch in the Scaffold library** —
   zero code shared with the Shell adapters, no host type-switching anywhere.
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
- **Deliberately minimal API in P1** (title, back button, toolbar items, title view). Search boxes
  etc. are explicitly post-v1 — this is where Shell replacements die.
- **Scroll-linked chrome** (the AppBarLayout / iOS large-title replacement): a `ScrollChrome`-style
  primitive observing the content's NATIVE scroll offset (iOS: KVO on `contentOffset`; Android:
  `NestedScrollView.ScrollChange` / RecyclerView listener) and publishing `(progress, offsetDp)`
  per frame on the UI thread. Built-in behaviors (collapse, title cross-fade, parallax factor,
  hide-on-scroll) + user-custom handlers setting plain MAUI properties. **PoC'd (spike D)**:
  MAUI-property-driven chrome holds display-refresh cadence (Android fling: ~65 events/s, worst
  gap 20ms), so custom parallax is ~10 lines of user code. Rules: collapse is transform-only
  (content reserves expanded height statically — no per-frame relayout); negative offsets (iOS
  bounce) feed stretch effects; VirtualScroll needs its own offset source (its root isn't a
  UIScrollView on iOS — see DevFlow notes).
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

### 5.4 Safe area & edge-to-edge

Owning the chrome means owning **inset distribution** — Shell/NavigationPage did this invisibly.
Baseline is edge-to-edge on both platforms (Android 15+ enforces it; iOS always was).

**Model (proven in production by NaluTabBar — adopt it Scaffold-wide): chrome AUGMENTS the safe
area, it never pads the page.** Pages always span the full container, edge-to-edge, *behind* the
bars. Each bar contributes its footprint to the safe-area insets the page sees, and the
developer's own safe-area-aware views handle the rest natively — a ScrollView scrolls its content
behind a translucent/blurred bar and gains the correct end padding automatically, while a view
that deliberately ignores the safe area draws behind the chrome (parallax backdrops, full-bleed
media). Reference implementations:

- iOS: the page host controller gets `AdditionalSafeAreaInsets` = bar footprint above the system
  inset ([NaluShellItemRenderer.cs](Source/Nalu.Maui.Navigation/Platforms/Apple/NaluShellItemRenderer.cs)
  `ViewDidLayoutSubviews`) — UIKit merges it with the system insets and every `UIScrollView`
  adjusts content insets natively.
- Android: intercept `OnApplyWindowInsets` on the page container and **rewrite the system-bars
  insets** (bottom = tab bar height, which already covers the system inset) before they propagate
  down ([NaluShellItemRendererNavigationLayout.cs](Source/Nalu.Maui.Navigation/Platforms/Android/NaluShellItemRendererNavigationLayout.cs))
  — the page treats the bar exactly like a system bar.

Consequences and requirements:

- The Scaffold generalizes the same mechanism to **all** chrome — and explicitly to the **nav bar
  (top edge)**: iOS `AdditionalSafeAreaInsets.Top` = nav-bar footprint above the status-bar inset;
  Android rewrites the top system-bars inset to the nav-bar height (which covers the status bar).
  The page then behaves exactly as it would under a translucent `UINavigationBar`: content scrolls
  behind the bar, scroll-indicator insets follow, full-bleed headers opt out of the safe area.
  Contributions are per-item and per-page — a page hiding its nav bar sees the raw system inset
  again. Changing bar visibility is an **inset change, not a page relayout** — animatable, no jumps.
- Scroll-linked chrome (§5.2) composes on top of this: the natural mapping is that the bar
  contributes its **collapsed** height as the top inset while the expanded portion lives in the
  scroll interaction (replacing the PoC's static-padding trick); whether/how the contribution
  animates with collapse progress is a design-review decision.
- Bars still handle their own internals: background extends under status bar / cutout / home
  indicator, bar *content* stays inside the safe area (the existing renderers' scrim/blur and
  `NeedsMeasure` logic transfers). Landscape notch insets affect bars and drawers too.
- **Keyboard (IME)**: bottom chrome vs keyboard interplay (tab bar hide-or-stay) and IME insets
  flowing through the same augmented-insets channel.
- **Android fragment hosting**: the fragment container must participate in the insets chain
  (dispatch + rewrite) for hosted page platform views — the PoC skipped this entirely.
- Integrate with MAUI's per-page safe-area API (`SafeAreaEdges` in .NET 10): since the Scaffold
  augments *platform* insets, MAUI's own safe-area machinery keeps working unmodified — that is
  the main virtue of this model.
- **Transitions**: flight overlays and shared-element geometry are computed in container space and
  must span the full edge-to-edge container, not the safe area. Sheets/detents respect the home
  indicator.
- Also mine: Core's `ScrollView` safe-area rendering fix (`Nalu_ScrollSafeAreaRenderingFix`).

### 5.5 Flyout(s) — "it's just a drawer"

- **Two drawers: `Start` and `End`** (logical directions, RTL-aware), independently configurable.
- Content model:
  - **Default template**: auto-renders the Scaffold's `ScaffoldArea`s (title/icon) as navigation entries;
    selection routes through `NavigationService`.
  - **Custom content**: any virtual view.
- **Resolution order for drawer content** (most specific wins):
  1. `Scaffold.FlyoutStart` / `Scaffold.FlyoutEnd` attached property on the **current Page**
  2. same attached property on the current **`ScaffoldArea`**
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
| Android hardware/system back | Intercepted (`OnBackPressedDispatcher` — the only channel under predictive-back enforcement, targetSdk 36+; works on all Android versions), routed through Pop → guards run. IMPLEMENTED. |
| Android back at stack root | Always the platform default: callback disabled → app backgrounds with the native predictive back-to-home preview intact. No interception hook by design. IMPLEMENTED. |
| `Page.OnBackButtonPressed` | **Deliberately unsupported** (decided): it only fires for hardware back, so confirmation logic written there is silently bypassed by on-screen pops — a bug factory. `ILeavingGuard` is the one confirmation mechanism, covering every leave path uniformly (back, buttons, absolute navigations, tab switches). Root exit-confirmation is a non-goal (modern-Android anti-pattern; no iOS equivalent). |
| Android predictive back (preview animation) | **Enabled only when the current page has no guard** (`HasGuard` is known synchronously). Guarded page ⇒ gesture registered as non-predictive back → guard runs on commit. |
| iOS interactive edge-swipe | Implemented by our transition engine (percent-driven). **Disabled when the current page has a guard** (decided). |
| iOS long-press back menu | Does not exist (no UINavigationBar). Not reimplemented (decided). |

Notes:

- "Guarded ⇒ no interactive gesture" is v1 policy; a later option is drag-then-confirm-at-release,
  the engine design (seekable animations, §8) keeps that door open.
- Tab/flyout selection also routes through the engine, so cross-stack guards keep working exactly as today.

---

## 7. Modals vs popups & sheets — two different families (decided)

**Modal pages are navigation. Popups and sheets are NOT** — they are presented UI, offered by a
separate mechanism the navigation engine never sees.

### 7.1 Modal pages (navigation)

- Full-screen modal pages stay in the navigation model: stack entries (`NavigationStackPage.IsModal`
  already models this), routes, guards, lifecycle events, snapshot/restore all apply.
- The Scaffold owns their presentation natively (today's Shell adapter piggybacks on
  `Shell.GetPresentationMode` + `ShellSection.Navigation.ModalStack` — reimplemented directly).
- MAUI's own `Navigation.PushModalAsync` on the page: out of scope / unsupported inside Scaffold
  (document it; all page navigation goes through Nalu).

### 7.2 Popups & sheets (presentation, separate from navigation — decided; **ships in v2**)

- **v2 feature — not part of the initial Scaffold release.** v1 ships modal pages only (§7.1);
  v1's only obligation is that the overlay-layer design doesn't preclude this.
- Reference model: [uxd-popups](https://github.com/UXDivers/uxd-popups)-style API — imperative
  show/await-result (the pattern Nalu popups already use in `Nalu.Maui.Layouts` —
  `PopupPageBase`/`PopupContainer`): **no route, no stack entry, no navigation lifecycle,
  no guards, excluded from snapshot/restore.**
- The Scaffold provides the **overlay layer** they render into (above page + chrome, below nothing),
  with scrim and safe-area handling per §5.4; back handling policy: hardware/system back and
  back gestures dismiss the topmost overlay before the navigation engine is ever consulted.
- A sheet is a popup with detent/drag behavior — implementation choice (native
  `UISheetPresentationController` / Material bottom sheet vs Nalu-drawn for cross-platform
  consistency) is part of THIS feature's design review, not the navigation engine's. The library's
  iOS 15.0 floor is only needed by the native-sheet option.
- Packaging (inside `Nalu.Maui.Scaffold` vs evolving the existing Layouts popup family on top of
  the Scaffold overlay layer) — design review.
- Win: `NaluShell.OnNavigating`'s CommunityToolkit-popup regex special-casing disappears — with a
  custom host, popups never enter the navigation pipeline in the first place.

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
- **Customizable push/pop page transitions are a first-class deliverable** (PoC'd, spike C):
  a declarative `PageTransition` spec — `Enter` motion for the incoming page, `Behind` motion for
  the covered page (fractional translate / scale / opacity), duration — with built-ins
  (SlideUpFade, SlideFromRight, ZoomFade, None). Declarative on purpose: both engines interpret it
  (iOS animator block; Android `Fragment.OnCreateAnimator` — NOT a managed Transition subclass,
  which the fragment framework's `Transition.clone()` breaks), keeping every transition
  seekable/reversible. Pop plays the push spec reversed.
- Resolution order: Scaffold default → per-page → per-navigation via the fluent builder
  (`Navigation.Relative().Push<DetailPageModel>().WithTransition(...)`, carried in `INavigationInfo`).
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

> **PoC outcome (July 2026, `poc/` — gitignored)**: all four spikes green.
> A: iOS custom engine 60fps, seek/reverse proven. B: Android FragmentManager + native SET 60fps,
> predictive back pops cleanly (mid-gesture SET scrubbing still to verify against androidx versions).
> C: declarative push/pop specs 60fps on both engines. D: scroll-linked collapsing bar/parallax at
> frame cadence from plain MAUI property setters.
> ⚠️ **The PoC API shapes (`PageTransition`, `ScrollChrome.Attach`, `TransitionTag`, `TransitionHost`)
> are feasibility proofs, NOT API proposals** — the developer-facing API will be designed separately
> (owner: Alberto) at the relevant phase's design review before implementation starts.

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
- **Startup destination precedence** (decided): valid snapshot → `Scaffold.InitialRootPageType` →
  first root of the first area. The property stays a dumb configuration value; the precedence
  lives entirely in the host's startup wiring.
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

> **Status (July 2026): substantially complete.** Contracts stay internal (IVT), engine untouched
> (531 pre-existing unit tests green). Real `ScaffoldProxy` + area/root proxies implemented and
> unit-tested against the real engine (7 tests: multi-push single-sync, pop, cross-area, tab
> preservation, dispose). P0 presenters implemented: iOS child-UIViewController containment,
> Android fragment hosting — both verified live on simulator/emulator via the `NavScaffold`
> TestApp harness with **identical lifecycle logs to the Shell host** (push/pop/dispose, guard
> deny+allow, cross-area teardown). Remaining: point the DevFlow `NavigationTests` UI suite at
> `NavScaffold` (NaluApp wrapper support) for the formal exit gate.
- Contracts promoted/renamed in Nalu.Maui.Navigation; the two `NaluShell` couplings removed; Shell host still green.
- Bare `Scaffold`: one `ScaffoldArea`/one stack, push/pop with a simple slide, modal push.
- Correct Appearing/Disappearing, DI scopes, handler disconnect, leak detector.
- **Exit**: existing DevFlow `NavigationTests` pass on the Scaffold-hosted TestApp.

### P1 — structure & chrome
- Full hierarchy: `ScaffoldArea` base, `ScaffoldTabBar` (default template + custom view replacement,
  stack preservation, active-tab-pops-to-root), `ScaffoldArea`, cross-item/stack navigation.
- Start/End flyouts with resolution order (Page → Item → global), default template + custom content.
- Minimal nav bar (title, TitleView, back button, toolbar items, visibility), safe-area/edge-to-edge.
- Back policy per §6 (system back interception; no gestures yet).
- **Exit**: a real-world sample app shape (tabs + drawer + modals) fully navigable with guards.

### P2 — transitions & gestures
- PoC spikes A + B → engine decision → implement engine + `TransitionTag` API + `WithTransition(...)`.
- Platform-parity push/pop animations, then shared elements, then interactive pop (iOS) and
  predictive back (Android), honoring the guard policy.
- (Popups/sheets are §7.2 — v2, out of the initial release entirely.)
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
| Safe-area regressions (inset handling is easy to get subtly wrong per device/orientation) | §5.4 augmented-insets model (proven in NaluTabBar renderers — reuse that code); TestApp pages on notch/cutout devices + landscape. |
| Lifecycle fidelity bugs (appearing order, handler disconnect, keyboard/insets) | P0 exit tied to existing test suite + leak detector; DevFlow tests per behavior. |
| Seekable-animation requirement discovered late | Baked into engine design from PoC gates (retrofit ≈ rewrite). |
| `androidx.transition` seeking insufficient for predictive back | PoC B explicitly verifies; fallback = custom `ValueAnimator` orchestration (same technique as iOS spike). |
| STJ under trimming/NativeAOT | `IIntentSerializer` injection point + source-gen context option from day one. |
| Contract promotion breaks Shell host subtly | Shell host kept green in CI/unit tests throughout P0. |
| Two tab bars to maintain (Shell NaluTabBar + Scaffold) | Accepted short-term; Shell variant is feature-frozen once Scaffold ships. |

---

## 14. Open questions (settle during design review)

1. ~~Final public names for the promoted contracts~~ — RESOLVED: contracts stay `internal`
   (`InternalsVisibleTo` to the Scaffold, see §4); naming is now an internal concern.
2. ~~Terse XAML forms for single-stack areas~~ — RESOLVED: implicit conversion operators
   (parse-time composition of real elements, see §3).
3. Flyout items targeting a specific stack inside an item — v1 proposal is **no** (item-level only).
4. Modal presentation config: per-page attached property vs push-time builder option (or both).
   Popups/sheets are settled as a separate non-navigation family shipping in v2 (§7.2, uxd-popups
   reference); its sub-questions (native vs Nalu-drawn sheets, packaging) belong to the v2 review.
5. Drawer "locked/side-by-side" mode — post-v1, but does the v1 API shape need to reserve room for it?
6. Snapshot storage location & retention policy (cache dir, single slot vs per-build slot).
7. Does `Scaffold` need a Shell-style "current page changed" public event surface beyond the existing
   `NavigationEvent` telemetry? (Consumers may want it for analytics.)
