# Nalu.Maui.Scaffold — implementation strategy

> Status: **implementation in progress** — P0 complete (exit gate passed on both platforms),
> P1 partially done. Living document: per-section status blocks record what's implemented.
> Targets: **Android + iOS only** (Windows/Mac Catalyst out of scope).
> Created 2026-07-23 · last updated 2026-07-25

## 0. Status at a glance

**Done (verified on iOS simulator + Android emulator, all suites green):**

- Library `Source/Nalu.Maui.Scaffold` in `Nalu.slnx` (net10.0 / -android / -ios26.0; iOS floor 12.2).
  Now in `Nalu.Pack.slnf` (August 2026): `11.0.0-scaffoldN.N` preview nupkgs are being cut for
  real-app testing. The `Nalu.Maui` meta-package was RETIRED (August 2026) — consumers reference
  the individual packages; no inclusion decision remains.
- Public structure API (§3): `Scaffold` → `ScaffoldArea`/`ScaffoldTabBar` → `ScaffoldRoot`,
  terse XAML via implicit conversions, engine-owned read-only selection
  (`CurrentArea`/`CurrentRoot`/`IsSelected`), template metadata quintet
  (`Title`/`Icon`/`SelectedIcon`/`CurrentIcon`), `IsVisible` (chrome-only), `InitialRootPageType`.
- Host implementation (§4.1): stack model → proxies → presenter seam → platform presenters →
  lean `ScaffoldHandler` (`UseNaluScaffold()` registers it — required).
- System back on Android (§6), `INavigation` bridge (reads + pops), flyout structure (§5.5).
- Test net: 7 proxy unit tests (538 total green); DevFlow `NavigationTests` parameterized over
  BOTH hosts (`NavShell` + `NavScaffold`) — 35/35 per platform; state preservation
  (scroll offset + entry text across push/pop) verified on both platforms.
- **Tab bar (§5.3) IMPLEMENTED and verified on both platforms (July 2026)**: default Telegram-style
  template with `ItemWidth`-driven overflow ("More" + wrap-grid panel reusing the item template),
  engine-routed selection with preserved-stack restore, active-tab-pops-to-root, §5.4 bottom-inset
  contribution (iOS `AdditionalSafeAreaInsets` on the content host, Android system-bars rewrite in
  the page layer), §5.6 overlay primitive (flyout refactored onto it), `SlideStart`/`SlideEnd`
  presentation hints for root switches. Suites: `ScaffoldTabBarChromeTests` (9 green per platform)
  + full navigation suites green on iOS and Android; the shared tab-switch chrome test now runs on
  the Scaffold host too.

- **Nav bar (§5.2) IMPLEMENTED and verified on both platforms (July 2026)**: `Scaffold.NavBarView`
  attached with scaffold-level default factory, `ScaffoldNavBarContext` binding contract,
  default template (drawer/back/title/drawer slots, drawer-button policies), public primitives
  for custom bars, §5.4 top-inset augmentation. Suites: `ScaffoldNavBarChromeTests` (6 green per
  platform); full regression green (iOS 53, Android 37).

- **Flyout completion (§5.5) IMPLEMENTED and verified on both platforms (July 2026)**:
  `ScaffoldFlyoutMode` (Auto/Disabled/Flyout, both sides default Disabled),
  `ScaffoldFlyoutMenuView`, `ScaffoldRoot.SelectCommand` (scaffold-wide busy gate, shared
  with tab-bar selection), `ScaffoldFlyoutOptions`, RTL mapping, per-side open state +
  events, `IScaffoldFlyoutController` (page scope), area-icon removal, stack-of-overrides
  resolution with page-content lifecycle cleanup. Edge-swipe open still open (transition-
  engine territory).
- **Modal pages (§7.1) IMPLEMENTED (July 2026)**: `Scaffold.PageMode`
  (Default/Modal/DismissableModal) composing the §5/§8 machinery — no dedicated modal engine.
- **Transition engine (§8) IMPLEMENTED (July 2026), verified on both platforms**: declarative
  `ScaffoldPageTransition` spec + `Scaffold.PageTransition` attached resolution,
  `Scaffold.TransitionName` shared elements, iOS interactive edge-swipe pop (manual scrub),
  Android predictive back (peek preview, engine-committed handoff). Predictive-back SET
  seeking investigated and PARKED (§8.1); per-navigation `WithTransition(...)` DROPPED.
- **Popups, sheets & overlay stack (§5.6/§7.2) IMPLEMENTED (July 2026)** — the former "v2"
  feature landed early: `ShowPopupAsync`/`ShowBottomSheetAsync` (placement, detents, drag),
  overlay stack semantics, attached presentation properties, and the model-first
  `IOverlayService` MVVM layer (`AddOverlay<TModel, TView>`).
- **Chrome styling model (§5.7)**: plain-MAUI-styles rework applied across all chrome.
- **Nav bar §5.2 REVISED (July–August 2026)**: `ScaffoldNavBarAppearance` per-property merge
  chain, native strip-level application, `NavBarBinding`, press pulse,
  `Scaffold.NavBarOverlapsContent`, and the scroll channel (`Scaffold.ScrollTracker` +
  `ScrollValue`/`ThemeScrollValue` extensions with page-level ramps) — see §5.2.
- **Keyboard/IME correctness (August 2026)**: the Scaffold raises `NavigatedTo/From` via
  `[UnsafeAccessor]` (`ScaffoldPageNavigationEvents`, `disconnectHandlers: false`) so
  `HideSoftInputOnTapped` works on Scaffold pages; the soft keyboard is dismissed BEFORE every
  page swap (Android + iOS). KNOWN LIMITATION (net10 MAUI): committing a navigation while the
  IME hide animation is still running briefly shows the incoming page with stale safe-area
  padding — `MauiWindowInsetListener` swallows every inset dispatch while its internal
  `IsImeAnimating` is set and only re-applies on animation end; deliberately NOT worked
  around (would require polling MAUI internals via reflection; upstream issue filed).
  Suites: `ScaffoldImeTests` (4) + `VirtualScrollImeTests` (2) green on Android.
- **§10 gap CLOSED (August 2026)**: `ScaffoldViewController` forwards `ViewDidAppear`/`Disappear`
  into the scaffold page's MAUI appearing events.
- **XAML hot reload / live structure (§4.2, August 2026)**: observed `Areas`/`Roots` collections
  with proxy re-sync, same-segment stack ADOPTION across re-inflation, current-root-removal
  fallback via real engine navigation, live `TabBarView` swap, post-init duplicate
  reconciliation. Verified via the "Scaffold Hot Reload Tests" harness (5 suites, both
  platforms); a REAL IDE hot reload session (VS/Rider patcher path) is still unverified.
- **Shared elements OVERHAULED for truthfulness (§8.1 revision, August 2026)**: iOS flights
  travel clip-aware VISIBLE rects with overlay-hosted label/scrim cross-fades and a pre-start
  commit flush; Android's androidx `TransitionSet` was REPLACED by a custom overlay engine
  mirroring the iOS choreography (animated corner radii, cross-fades, controlled stacking).
  Frame-capture verified on both platforms (DailyHelper hero).
- **System bars (§5.8, August 2026)**: `Scaffold.SystemBarStyle` (Auto/LightContent/DarkContent)
  driving status-bar (and Android gesture-bar) icon styles from the visible surface stack, with
  PIXEL-SAMPLED Auto (PixelCopy / scaled window snapshot), theme-change re-application (fixes
  stale icons AND stale Android `navigationBarColor` on uiMode toggles without recreation).
  6 ground-truth UI tests + 12 resolver unit tests, both platforms.

- **Host conveniences (August 2026)**: `Scaffold` implements MAUI's `IPageContainer<Page>`
  with an observable read-only `CurrentPage` bindable (recomputed from the proxy on stack
  mutations and selection changes); `Scaffold.InitialIntent` delivers a startup intent to the
  initial root through the standard entering pipeline (also the §9 replay seam — unit-verified);
  `GetScaffold`/`GetScaffoldOrDefault` retarget `IElement` and UNIFY the app-level lookup
  (`Application` IS an `IElement`: when the parent walk reaches an `IApplication` — or starts
  there — the first scaffold-hosted window's scaffold is returned; no conflicting overloads).
- **Conceptual docs + migration guide SHIPPED (August 2026)**: eight docfx pages
  (`conceptual_docs/scaffold*.md`) incl. the NaluShell → Scaffold migration guide.
- **Navigation-state snapshot & restore (§9) IMPLEMENTED (August 2026), verified on both
  platforms** — landed ENGINE-LEVEL in `Nalu.Maui.Navigation` (Scaffold is the verified
  host) after two design pivots in review: automatic capture with intent-serializability-
  derived restorability, replay after the initial root's first appearing (init flows always
  run), pending-restore navigation suppression, `INavigationRestore`
  (`ForgetAsync`/`RestoreWithIntentAsync`/`TryStopRestoreAsync`). 24 unit tests + 3 DevFlow
  kill-and-relaunch tests (real process death via `NaluApp.RestartAppAsync`) green on iOS +
  Android; docfx page `conceptual_docs/navigation-restore.md`. Branch:
  `feature/navigation-restore`.

- **Source-generated registrations SHIPPED (August 2026, branch
  `feature/navigation-source-generator`)**: `AddPages()` / `AddIntents()` (Navigation) and
  `AddOverlays()` (Scaffold) are emitted at compile time by analyzer assemblies packed inside
  the respective NuGet packages — trim/AOT-safe, replacing the reflection `AddPages(...)`
  overloads (removed, breaking). Restore opt-in moved to `builder.UseNaluNavigationRestore(...)`.
  Attributes: `[AutoNavigationPage]`, `[AutoNavigationIntent]`, `[AutoOverlay]`. VIEW-ONLY
  overlays added (`AddOverlay<TView>()`: a View taking `IOverlayRef` is its own lifecycle
  target). 27 generator tests (incl. incrementality-as-invariant) + full suites green; full
  DevFlow iOS run green (one pre-existing flyout flake, tracked separately).
- **Restore (§9) MERGED to main (August 2026, PR #177)**; `ForgetAsync` scoping semantics
  pinned by dedicated unit tests (pages below a forgotten page keep restoring; the exclusion
  dies on pop; a forgotten ROOT excludes only its own stack, other roots keep tracking).

**Next steps, in recommended order:**

1. **P3 remainder**: deep-link mapping (URI → `INavigationInfo`).
2. Verify one REAL IDE XAML hot reload session on the DailyHelper (§4.2 harness covered the
   object-level effects only).
3. Optional/parked: flyout edge-swipe open; predictive-back shared-element flights — newly
   FEASIBLE since the Android engine replacement (see §8.1 addendum); Shell-host restore
   verification (engine-level design should work there — untested, unadvertised).

**Resolved (August 2026):** the NaluTabBar full-height issue on Android API 37 (Shell host) —
`RowDefinitions="Auto"` root + height-only Unspecified measure spec + the reporter's
`View.Layout` NRE fix — is MERGED to `main` (#162).

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

- New package **`Nalu.Maui.Scaffold`**, depends on `Nalu.Maui.Navigation`, `Nalu.Maui.Core` and
  `Nalu.Maui.Layouts` (the default tab bar overflow panel builds on `HorizontalWrapLayout`).
  IMPLEMENTED: net10.0 / net10.0-android / net10.0-ios26.0 (net11 later); iOS floor 12.2, Android 21
  (repo low-floor policy — native sheets would be runtime-guarded, §7.2); `InternalsVisibleTo`
  grants from Core and Navigation. In `Nalu.Pack.slnf` since August 2026 (preview nupkgs for
  real-app testing); the `Nalu.Maui` meta-package was retired instead of extended.
- `Nalu.Maui.Navigation` keeps working with MAUI Shell exactly as today — existing users unaffected
  (verified: zero engine changes; all pre-existing tests green throughout).
- The host-abstraction contracts stay `internal`, consumed via IVT (see §4).
- Registration mirrors the existing pattern: `.UseNaluScaffold()` alongside `.UseNaluNavigation(...)` —
  IMPLEMENTED and **required** (it registers the `ScaffoldHandler`).

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

### 4.1 Host architecture (as built)

Layering, bottom-up — each layer testable without the one below:

1. **`ScaffoldNavigationStack`** (per `ScaffoldRoot`, platform-free): lazily created `RootPage`
   + pushed `NavigationStackPage` entries. Every page entering a stack is logically parented to
   the hosting `Scaffold` (MAUI requires a page's parent to be a page — Shell's `BaseShellItem`
   carve-out doesn't apply to us), giving window resolution, tree visibility and tooling for free.
2. **Proxies** (`ScaffoldProxy`/`ScaffoldAreaProxy`/`ScaffoldRootProxy`): thin engine adapters;
   `ScaffoldRootProxy` implements section AND content contracts (the synthesized third level).
   Batching nuance discovered against the real engine: **pops apply to the model immediately**
   (the engine has already run leaving lifecycle and disposes those pages — deferring leaves
   ghost entries), while **pushes stay pending until commit** so a multi-push batch presents as
   ONE transition. Selection state (`CurrentArea`/`CurrentRoot`/`IsSelected`) is written only here.
3. **`IScaffoldPresenter`** (platform seam): synchronize-to-model with a direction hint
   (`None`/`Push`/`Pop`/`SlideStart`/`SlideEnd` — direction is not derivable from a stack diff on
   replace/cross-area batches, so the proxy passes intent; the slide hints carry the direction of
   travel for root/area switches, computed from the structure ordinal). One awaited
   synchronization per commit; deterministic completion (no `Task.Delay` hacks). Also owns the
   chrome (tab bar strip) and the §5.6 overlay layer.
4. **Platform presenters**: iOS = child-`UIViewController` containment (single visible page,
   covered pages detached NEVER destroyed — scroll/entry state verified preserved);
   Android = fragment-per-visible-page (`Replace`, no back stack yet — predictive-back
   integration will build on it), animator-based transitions via `OnCreateAnimator`.
5. **`ScaffoldHandler`**: lean `ViewHandler` (NOT `PageHandler` — page pipeline carries hidden
   behaviors; Shell ships its own renderer for the same reason). iOS reimplements
   `IPlatformViewHandler` to expose its own `ScaffoldViewController` (the window root VC —
   MAUI installs it, so UIKit containment/safe-area propagation is native); Android owns a plain
   `FrameLayout` root (`ScaffoldLayout`) so overlay/chrome children lay out natively.
   Presenter lifetime = one per handler connection (activity recreation / re-attach safe).
6. **`ScaffoldNavigationImpl`** (`INavigation` bridge, installed as NavigationProxy inner —
   Shell/NavigationPage's integration point): truthful `NavigationStack`, pops routed through the
   engine (guard-aware aliases of `Relative().Pop()`), pushes/modals throw with guidance toward
   `INavigationService`. Kept deliberately: DevFlow-style automation used by customers drives
   back/pop through this channel.

### 4.2 Live structure mutations & XAML hot reload (August 2026)

XAML hot reload re-runs page initialization on LIVE instances, re-`Add`ing `Areas`/`Roots` and
replacing `TabBarView`. The scaffold now supports live structure mutation as a first-class
behavior (which also enables runtime structure edits by apps):

- `Scaffold`/`ScaffoldArea` observe their collections post-init; mutations from inside
  CollectionChanged are DEFERRED via `Dispatcher.Dispatch` with a coalescing flag
  (ObservableCollection reentrancy: >1 listener forbids nested mutation).
- `ScaffoldProxy.SyncStructure()` rebuilds the proxy tree REUSING instances; a re-inflated root
  with the same segment ADOPTS the predecessor's live navigation stack (page state survives a
  hot reload), via an in-area pass plus a global adoption pass.
- Removing the CURRENT root pins the removed root's stack alive (the engine cannot navigate
  from an empty current section and resolves contents BY SEGMENT mid-flight — both crash/break
  otherwise), navigates to a fallback target computed from the NEW tree, then disposes the
  removed content.
- Post-init duplicate reconciliation (`ReconcileDuplicateAreas`/`Roots`): hot reload re-Adding
  the full structure yields duplicates by root-PageType intersection — last-in wins.
- Live `TabBarView` swap: same-area swaps disconnect the previous bar's handlers
  (`OnBarViewReplaced`); presenters re-resolve chrome non-animated.

Verified by the "Scaffold Hot Reload Tests" harness + `ScaffoldHotReloadChromeTests` (5 suites,
both platforms) — these simulate the object-level effects of the IDE patcher; an end-to-end
session against the REAL VS/Rider XAML hot reload channel is still pending.

---

## 5. Chrome (all Nalu-drawn)

### 5.1 Why fully owned (decided)

- Android/iOS native nav bars have incompatible height/content constraints (iOS is severely limited).
- iOS long-press-back multi-pop menu bypasses navigation guards → **will not exist** (decided: not reimplemented either).
- Native swipe/predictive back is hard to reconcile with async guards (§6).
- Owned chrome = virtual views = trivially customizable, testable via DevFlow, consistent cross-platform.

### 5.2 Nav bar

> **IMPLEMENTED (July 2026), verified on iOS simulator + Android emulator.** API as reviewed:
> - `Scaffold.NavBarView` attached (Page → Area → Scaffold resolution; the scaffold level
>   defaults to a `ScaffoldNavBarView` via the property's default value factory) +
>   `Scaffold.IsNavBarVisible` attached bool (default true, animated hide/show, retargeting).
> - **`ScaffoldNavBarContext`** is the binding context of ANY mounted bar (default or custom):
>   `Title`, `TitleView`, `CanNavigateBack`, `IsFlyoutStart/EndButtonVisible`, `BackCommand`,
>   `OpenFlyoutStart/EndCommand` — one observable instance per scaffold.
> - Default template slots, in order: **[start-drawer] [back] [title/TitleView] [end-drawer]**;
>   drawer-button policy via attached `Scaffold.FlyoutStart/EndButtonVisibility`
>   (`Auto` default = shown at stack roots; `Visible` = always, side by side with back;
>   `Hidden`), resolved Page → Area → Scaffold and derived into the context bools.
> - Public primitives for custom bars: `ScaffoldBackButton`, `ScaffoldCloseButton`,
>   `ScaffoldFlyoutButton` (Side), `ScaffoldNavBarTitle` — drop-in, self-binding to the context.
> - **Styling split (REVISED July 2026, §5.7)**: `ScaffoldNavBarView` owns ONLY the strip
>   (`BarBackground`, `BarHeight`, `BarPadding`, `Spacing`). Title and glyph appearance live on
>   the primitives themselves, styled directly — the earlier aggregator properties
>   (`TextColor`, `IconColor`, `FontFamily`, `TitleFontSize`, `TitleFontAttributes`, `BackIcon`,
>   `FlyoutStart/EndIcon`) were REMOVED: pushing them into the children set MANUAL values that
>   outrank every style setter, making the public primitives un-styleable inside the default bar.
>   One owner per value ⇒ the same `<Style>` works in the default bar and in a custom one.
> - §5.4 top insets: the bar fills the top strip (background under the status bar, safe area
>   consumed via .NET 10 `SafeAreaEdges`); measurement normalized to content height
>   (measured − consumed inset, the NaluShellItemRenderer net10 pattern); iOS per-page
>   `AdditionalSafeAreaInsets.Top`, Android top system-bars rewrite in the page layer.
>   Same keep-alive-offscreen + interruptible animation model as the tab bar; the nav strip
>   sits BELOW the tab strip in z-order (behind-chrome overlay scrims dim it).

> **REVISED (August 2026) — appearance chain & scroll-linked chrome, verified on both platforms:**
> - **`Scaffold.NavBarAppearance` attached** (`ScaffoldNavBarAppearance`: `Background`,
>   `Foreground`, `Opacity`, `OffsetY`) with PER-PROPERTY merge, page → area → scaffold — a
>   page overrides only what it sets. `Foreground` flows through `ScaffoldNavBarContext` into
>   the default bar's primitives (per §5.7, styles still win at the primitive level).
> - **Applied at the native STRIP level** (paint / alpha / translate — no MAUI relayout), so
>   scroll-driven appearance animates at frame cadence; scaffold/area-level `NavBarView`
>   changes remap LIVE without recreating the strip.
> - **`TitleView` binds to the `ScaffoldNavBarContext`** like the rest of the bar; the
>   **`{nalu:NavBarBinding ...}`** markup extension reaches THROUGH the context to the page's
>   own BindingContext for page-model data inside `TitleView`/custom bars.
> - `ScaffoldNavBarButtonBase` press pulse — virtual, consistent on both platforms.
> - **`Scaffold.NavBarOverlapsContent` attached bool** ("parallax mode"): the bar's footprint
>   stops contributing top insets — content starts at the top edge and the bar draws over it.
>   Pair with a page-level transparent `NavBarAppearance.Background` for full-bleed headers
>   whose bar materializes on scroll.

- **Deliberately minimal API in P1** (title, back button, drawer buttons, title view).
  ToolbarItems, search boxes etc. are explicitly post-v1 — this is where Shell replacements die.
  Custom bars (full replacement + primitives) are the v1 escape hatch.
- **Scroll-linked chrome IMPLEMENTED (August 2026)** (the AppBarLayout / iOS large-title
  replacement): **`Scaffold.ScrollTracker`** attached (per page) points at the page's primary
  scrollable `View` — the platform subtree is searched a few levels deep for the actual native
  scrollable (component roots often wrap it, e.g. VirtualScroll) — and its NATIVE offset feeds
  `ScaffoldNavBarContext.ScrollOffset`/`IsScrolledUnder` per frame (iOS: KVO on
  `contentOffset`; Android: scroll listeners). Consumption is declarative:
  **`{nalu:ScrollValue From=…, To=…}`** and **`{nalu:ThemeScrollValue FromLight=…, ToLight=…,
  FromDark=…, ToDark=…}`** markup extensions interpolate ANY bindable property from the scroll
  offset; the interpolation window defaults to the page-level **`Scaffold.ScrollRampStart` /
  `ScrollRampEnd`** attached values (resolution page → area → scaffold → 0/100), overridable
  per usage via `RampStart`/`RampEnd`. Combined with `NavBarAppearance` +
  `NavBarOverlapsContent` this gives fully declarative scroll-driven hero chrome — reference
  implementation: the DailyHelper weather detail page. Spike D's finding holds in production:
  MAUI-property-driven chrome sustains display-refresh cadence (Android fling ~65 events/s,
  worst gap 20ms). Rules: collapse is transform-only (content reserves expanded height
  statically); negative offsets (iOS bounce) feed stretch effects.
- Safe-area / edge-to-edge handling reuses the patterns already built for the NaluTabBar renderers
  (scrim views, `AdditionalSafeAreaInsets` on iOS, insets layouts on Android), re-authored for the
  Scaffold container.

### 5.3 Tab bar

> **IMPLEMENTED (July 2026), verified on iOS simulator + Android emulator** — default template
> (`ScaffoldTabBarView` + `ScaffoldTabBarItemsLayout` + `ScaffoldTabBarItemView`), overflow panel
> (`ScaffoldTabBarOverflowView`), full styling surface on `ScaffoldTabBar`. Implementation
> refinements over the original review, all user-confirmed:
> - The overflow panel is a **`HorizontalWrapLayout` grid reusing the tab item template**
>   (icon-over-label, badge, selection pill, same `ItemWidth` slots, wraps to rows) — not list
>   rows; it **hugs its content and centers**, mirroring the bar pill (adds a
>   `Nalu.Maui.Layouts` dependency to the Scaffold package).
> - The overflow scrim is **fullscreen, inserted BELOW the tab bar strip in z-order** (see §5.6)
>   instead of exclusion geometry.
> - Root/area switches animate with new **`SlideStart`/`SlideEnd` presentation hints** computed
>   from the structure ordinal (direction of travel), LTR-mapped for now.
> - Chrome mount state is reflected in the element tree (bar view and panel are logical children
>   only while presented — tooling/UI tests see the truth).
> - **`Scaffold.TabBarVisibility` attached property** (enum `Visible` default / `Hidden` /
>   `Auto` = hidden while the current stack has pushed pages) replaces the earlier bool.
>   Hide/show is ANIMATED in sync with the push/pop transition; the §5.4 inset contribution is
>   applied PER PAGE (iOS: `AdditionalSafeAreaInsets` on each page's own controller; Android:
>   the page-layer rewrite state is set before the fragment commit) so the outgoing page never
>   relayouts — no jumps.
> - Item views are Grids with an inner selection-pill layer (a Border clips its content, cutting
>   the badge) and carry **explicit dp heights** (icon host, label, badge) — the bar measures
>   identically on iOS and Android (verified ~70.5dp pill on both).

- `ScaffoldTabBar` ships with a **default Nalu template** that auto-renders its `ScaffoldRoot`s from
  the metadata quintet (`Title`/`Icon`/`SelectedIcon`/`CurrentIcon`/`IsSelected`, honoring
  `IsVisible`).
- **Visual language (decided): Telegram-style floating pill bar** — translucent dark/light rounded
  container floating above the content with margins (not edge-to-edge), icon + label per item,
  the selected item highlighted by a rounded pill tint with the accent color on the label; badge
  support on any icon. Default accent = **Nalu logo wave blue** `#2C479D`, baked into the
  bindable properties' default values per §5.7 (no shipped ResourceDictionary).
- **Icons render untinted (decided — no `IconColor`)**: the template draws the quintet's
  `ImageSource`s as-is; avatars-as-tabs work out of the box (Telegram "Profilo" case). Monochrome
  tinting is the *root's* concern (`FontImageSource` color, `AppThemeBinding` inside the Icon);
  the selected appearance comes from providing `SelectedIcon`/`CurrentIcon`, never from template
  recoloring.
- **Layout & overflow policy (decided): `ItemWidth`-driven, no fixed item cap.**
  - `ItemWidth` (bindable, default ≈76dp) is the single input.
    `fittingSlots = floor(availableBarWidth / ItemWidth)` (available = container width minus bar
    margins/padding and landscape notch insets per §5.4).
  - All visible roots fit ⇒ show them all, no More button. Otherwise show `fittingSlots − 1`
    roots + a trailing **"More" (•••)** item; the remainder (declaration order) goes to the
    overflow panel.
  - The bar **hugs its content**: width = `shownCount × ItemWidth + padding`, centered — grows
    gradually with item count on tablets, lands at the Telegram look (~4 items) on phones.
    Rotation/resize re-runs the computation (items migrate between bar and overflow; an open
    panel whose item count drops to zero closes).
  - Fixed slot width ⇒ long titles truncate (a `LineBreakMode`-style knob, not slot widening).
- **Overflow "More" panel**: rounded panel anchored above the bar, rendered in the shared overlay
  layer (§5.6). The scrim darkens the page content but **excludes the bottom chrome footprint —
  the tab bar stays undimmed and fully interactive**: tapping an in-bar item while the panel is
  open dismisses the panel AND performs that selection in one gesture. Overflow roots render as
  full-width rows (icon + label + badge); when the current root lives in overflow, the More
  button itself shows the "current" pill tint and the row is highlighted in the panel. Scrim tap
  and Android system back dismiss the panel before the navigation engine is consulted (§7.2
  overlay-dismiss policy). Active-tab-pops-to-root applies to overflow rows too.
- **Styling surface (decided; REVISED AGAIN July 2026 — component split per §5.7 rule 3)**:
  the styling surface lives on the default template COMPONENTS, not on `ScaffoldTabBar`
  (an app installing a custom bar carries none of the default template's properties), and is
  split one-public-type-per-concern so each is targeted by a plain implicit `<Style>` — the same
  decomposition as the nav bar primitives:
  - **`ScaffoldTabBarView`** (the bar itself; `ScaffoldTabBar.TabBarView` defaults to a fresh
    instance via the property's default value factory; resolves its owning tab bar from the
    logical parent when presented): `BarBackground`, `BarCornerRadius`, `BarMargin`,
    `BarPadding` (mirrored by the overflow panel — slot-geometry parity), `BarShadow`,
    `ItemWidth` (the single layout input), `OverflowIcon`, `OverflowTitle` (localizable "More"),
    and the `BadgeText` ATTACHED property (per-root data channel, bindable per tab).
  - **`ScaffoldTabBarItemView`** (public type, INTERNAL ctor — instances are generated, one per
    visible root plus "More"; the overflow panel reuses the same component, so ONE style covers
    bar items and overflow rows alike): `IconSize`, `TextColor`, `SelectedTextColor`,
    `FontFamily`, `FontSize`, `SelectionPillBackground`, `SelectionPillCornerRadius`, plus the
    badge appearance `BadgeBackground`, `BadgeTextColor`, `BadgeFontSize` (the badge is rendered
    by the item — same owner, same style).
  - **`ScaffoldTabBarOverflowView`** (public type, internal ctor — built per open):
    `PanelBackground`, `PanelCornerRadius`, `PanelShadow`, `ScrimColor` (read when the panel
    opens, AFTER logical parenting so implicit styles have applied).
- **Full replacement supported**: user provides their own virtual view (DataTemplate or direct view);
  the Scaffold supplies a binding context exposing the roots, selection state, and a select command.
  Tab selection routes through `NavigationService` (guards respected) — never a direct view swap.
- Tapping the active tab pops that root's stack to its root page (existing NaluTabBar behavior, preserved).
- **Insets (implementation directive)**: mirror NaluTabBar's windowinsets/safe-area renderer code
  (§5.4 reference implementations) with high attention — the hosted page must receive the proper
  augmented insets (bottom = bar footprint incl. the floating margins policy) exactly as it does
  under NaluTabBar today.
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

> **Status (July 2026): structure IMPLEMENTED and verified on both platforms.**
> `FlyoutStart`/`FlyoutEnd` attached properties with Page → Area → Scaffold resolution
> (disabled by default — null at every level); contents are plain MAUI views, logically
> parented at their attachment point (per-page flyouts inherit the page's BindingContext);
> `Scaffold.OpenFlyoutAsync(side)`/`CloseFlyoutAsync()`; presenters render scrim + slide-in
> panel, tap-scrim closes, any navigation auto-closes.
> **Completion IMPLEMENTED (July 2026), verified on both platforms** — suites:
> `ScaffoldFlyoutChromeTests` (5 green per platform) + full regression green
> (Android 183, iOS 180). Edge-swipe open remains transition-engine territory (P2).

- **Two drawers: `Start` and `End`** (logical directions, RTL-aware), independently configurable.
- **Resolution = a STACK of overrides** (content AND mode, decided July 2026): the topmost
  stack page that explicitly SET the property wins (`IsSet` semantics — an explicit
  null/Disabled overrides downward), then older pushed pages, the root page, the current
  `ScaffoldArea`, and finally the `Scaffold` — a page's drawer survives pushes that don't
  override it, and a pop restores the previous page's drawer.
- **Page-level content lifecycle**: attached content is a logical child of its page
  (inheriting the page's BindingContext live); when the page leaves the navigation stack the
  content is detached (clearing the inherited context) and its handlers disconnected —
  the page model is never retained through the drawer view
  (`Scaffold.CleanupPageFlyoutContent`, hooked in `ScaffoldNavigationStack.Pop`/root swap).
- Open question settled: flyout items target roots only for v1 (no per-stack targets).

**Decided completion design (July 2026):**

- **Existence = content AND mode.** A drawer exists when its content resolves non-null AND its
  mode resolves ≠ `Disabled`. No implicit content, no "none" sentinel — suppression goes
  through the mode.
- **`ScaffoldFlyoutMode { Auto, Disabled, Flyout }`** via `Scaffold.FlyoutStartMode` /
  `FlyoutEndMode` attached properties, Page → Area → Scaffold resolution using `IsSet`
  fall-through (`Auto` is a real policy, not an "unset" marker). `Auto` = `Flyout` at stack
  roots, `Disabled` once pages are pushed (mirrors `ScaffoldFlyoutButtonVisibility.Auto`).
  Defaults: **both sides `Disabled`** — a drawer always requires content + an explicit mode
  (`Auto` or `Flyout`); nothing materializes by merely setting content.
  Future member: `Sticky` (always visible, splits the screen with the page — tablets); `Auto`
  may later adapt to form factor. `OpenFlyoutAsync` no-ops when existence fails; the nav-bar
  drawer button keys off the same check.
- **`ScaffoldFlyoutMenuView`** (public view, the opt-in menu content — no implicit default):
  renders the scaffold's VISIBLE roots. Rules: area with one visible root → flat entry (root
  metadata quintet); area with 2+ visible roots → area `Title` as text-only group header with
  its roots below; `ScaffoldTabBar` areas excluded unless `IsTabBarDisplayed="True"` (default
  false). Selection routes through the engine like a tab tap (preserved-stack restore, guards;
  navigation auto-closes the drawer). Customization: `HeaderView`, `FooterView`, `ItemTemplate`
  (BindingContext = the `ScaffoldRoot`; each item is wrapped in a tappable container so
  templates are purely visual). Internals: `ScrollView` + `BindableLayout` over a vertical
  stack — deliberately NOT virtualized.
- **`ScaffoldRoot.SelectCommand`** (read-only `ICommand`, parameterless): engine-routed
  selection of that root via the parent `Scaffold` (guards, preserved-stack restore); no-op
  when detached. Usable from ANY custom flyout content — `ScaffoldFlyoutMenuView`'s default
  items ride the same command. Also becomes the public selection hook for custom TAB BAR
  templates and full `TabBarView` replacements (today taps are wired internally by
  `ScaffoldTabBarView`; its items migrate to the same command).
  **Async-smart**: `CanExecute` is false while a selection navigation is in flight — the gate
  is scaffold-wide (ALL roots' commands raise `CanExecuteChanged` together, so a second tab
  can't race the first; the engine would silently ignore it anyway — this makes the UI honest),
  always re-enabled on settle (success, guard-cancel or failure). Follow-up (not v1): reflect
  ANY engine navigation by surfacing the engine's busy state through the proxy.
- **Metadata cleanup (breaking)**: `ScaffoldArea` loses `Icon`/`SelectedIcon`/`CurrentIcon` and
  keeps `Title` only — the icon quintet lives on `ScaffoldRoot` (verify no tab-bar template
  binds area-level icons when implementing).
- **Styling**: per-side `ScaffoldFlyoutOptions : BindableObject` (`Width` = explicit dp, wins
  when >= 0; else `WidthRatio` default 0.85 capped by `MaximumWidth` default 360; `ScrimColor`
  null = black 40%) via `Scaffold.FlyoutStartOptions` / `FlyoutEndOptions` — scaffold-level
  only (styling is chrome, not page content). Defaults reproduce the current hardcoded
  presenter metrics. `EdgeSwipeEnabled` reserved for this object (added with the P2 gesture).
- **RTL**: presenters map `Start`/`End` to left/right from the scaffold's effective
  `FlowDirection` — one mapping helper drives placement gravity, slide direction and (later)
  the gesture edge.
- **Open-state observability**: read-only bindable `IsFlyoutStartOpen` / `IsFlyoutEndOpen` +
  events `FlyoutStartOpened`/`FlyoutStartClosed`/`FlyoutEndOpened`/`FlyoutEndClosed`.
- **Programmatic control**: `OpenFlyoutAsync(side)` / `CloseFlyoutAsync()` stay; add
  `IScaffoldFlyoutController` registered in the page DI scope forwarding to the ambient
  scaffold.

### 5.6 Overlay layer — one shared primitive (decided July 2026)

All scrim-plus-panel chrome goes through a single internal Scaffold overlay primitive instead of
per-feature scrim implementations — shape (illustrative):
`ShowOverlayAsync(content, OverlayOptions { ScrimColor, ScrimInsets, ExcludeBottomChrome, DismissOnScrimTap, Anchor })`.

Consumers:

| Consumer | Scrim | Notes |
|---|---|---|
| Flyout (§5.5) | fullscreen, above everything | IMPLEMENTED — refactored onto the primitive |
| Tab-bar overflow panel (§5.3) | fullscreen, **inserted BELOW the tab bar strip in z-order** | IMPLEMENTED — reserved for the More button |
| Popups & sheets (§7.2, v2) | fullscreen, above everything | the v1 "don't preclude" obligation is satisfied structurally |

**Public surface (July 2026)**: the behind-chrome panel placement is exposed as
`Scaffold.OpenTabBarPanelAsync(View, Color? scrimColor)` (+ `ScaffoldTabBar.OpenPanelAsync`
convenience, and `Scaffold.CloseOverlayAsync`), so CUSTOM tab bars can present their own panels
with the exact More-button semantics (toggle on re-invoke, scrim below the bar, dismiss on
scrim tap/back/navigation). The default template's overflow runs on this same path
(panel construction moved to shared code in `ScaffoldTabBar.OpenOverflowAsync`); the fullscreen
overlay placement (popups, §7.2) deliberately stays internal until the v2 design review.

Key rules (as implemented):

- **Behind-chrome placement is z-order, not geometry** (decided during implementation, replacing
  the earlier exclusion-zone idea): the fullscreen scrim and the panel are inserted below the
  bottom chrome strip, so the tab bar renders above the scrim — undimmed and interactive — with
  zero exclusion arithmetic to maintain across devices/orientation/bar styling. Hit-testing
  follows z-order natively (bar above scrim ⇒ bar taps win; a bar tap both dismisses the panel
  and performs the selection). This placement mode is reserved for the tab bar's More button.
- Back policy: hardware/system back and scrim tap dismiss the topmost overlay before the
  navigation engine is consulted (same rule §7.2 states for popups); the Android back callback's
  `Enabled` state accounts for open overlays.

### 5.7 Chrome styling model — plain MAUI styles (decided July 2026)

The first implementation resolved appearance at runtime: `null`-as-unset properties, a
`ScaffoldTabBarPalette.Resolve()` returning a `ScaffoldTabBarStyleValues` record, a
`ScaffoldNavBarDefaults.Foreground(dark)` helper, `Application.RequestedThemeChanged`
subscriptions in four constructors, and a catch-all `OnPropertyChanged` string switch re-applying
the whole surface. **All of it is deleted.** The model is now ordinary MAUI:

1. **Every styling property carries a REAL default value** (no `null` sentinels). Defaults sit at
   the bottom of MAUI's value precedence, so an implicit `<Style>` — including one whose setters
   are `AppThemeBinding`s — simply wins. Theming is the app's `<Style>`, and MAUI re-evaluates it
   on theme change for free (no subscriptions, no leaks). Reference styles live in the docs and
   in `Samples/Nalu.Maui.DailyHelper/Resources/Styles/Styles.xaml`; the baked defaults are a
   single light-leaning palette, so a dark-mode app is expected to supply the style.
2. **One `propertyChanged` callback per property**, doing exactly the one assignment it implies.
   No shared "apply everything" path — changing `FontSize` must not re-allocate the bar shadow —
   and no `OnPropertyChanged` override (that switch ran on every layout-driven property change of
   the underlying `Grid`, `X`/`Y`/`Width`/`Height` included).
3. **A value is owned by exactly ONE type.** A parent must never push appearance into a public
   child: assignment from code is a MANUAL value and outranks every style setter, which would
   silently make the child un-styleable. Hence §5.2's aggregator removal, the drawer menu's
   `ScaffoldFlyoutMenuItemView` / `ScaffoldFlyoutMenuGroupHeader` becoming **public** with their
   own properties, and the tab bar's second pass: `ScaffoldTabBarItemView` and
   `ScaffoldTabBarOverflowView` are public styling surfaces owning their own values (§5.3) —
   the owner-push `Update*` machinery they replaced is gone. A generated child (public type,
   internal ctor) is still a perfectly good style target.
4. **Corollary — never set an INHERITED property in a constructor** to get a non-zero default;
   that is the same manual-value trap. Introduce an owned property instead and let its callback
   drive the inherited one: `BarMargin` → `Padding`, `BarBackground` → `Background`,
   `PanelBackground` → `Background`, `HeaderPadding` → the inner label's `Padding`.
   The group header hosts a `Label` rather than deriving from one for exactly this reason.
5. Defaults never raise `propertyChanged`, so each constructor **seeds its children once** from
   the current property values. That is the only bulk-apply left, and it runs once per instance.
6. **CAVEAT (cost a debugging session, July 2026): implicit styles are applied by the
   `VisualElement` BASE ctor** (`MergedStyle` in the ctor chain), i.e. BEFORE the derived class's
   ctor body has built its subviews. Every `propertyChanged` callback dereferencing a
   ctor-assigned field must therefore tolerate `null` (C# 14 null-conditional assignment:
   `view._pill?.Background = value`), and rule 5's ctor seeding is what makes the style value
   land anyway — the seed reads the final property value AFTER the subviews exist. Symptom when
   violated: NRE during chrome construction inside the presenter → silently blank app.

Reference-type defaults (`Brush`, `Shadow`) use `defaultValueCreator`, not a shared static: MAUI
re-parents those values on assignment, so one instance handed to two owners breaks the first.

### 5.8 System bars — status/navigation bar icon styles (August 2026)

`Scaffold.SystemBarStyle` attached property (`Auto`/`LightContent`/`DarkContent`, resolution
page → area → scaffold) drives the STATUS BAR icon style on both platforms plus the Android
gesture-navigation bar. The icons contrast with the VISIBLE surface stack, resolved by
`Internals/ScaffoldSystemBars` (state contributors + pure, unit-tested resolver):

1. **Open flyout** — its surface luminance (set at present, cleared at close start).
2. **Nav bar** (visible AND opaque enough, alpha·opacity ≥ 0.5) by luminance — fed LIVE from
   `ScaffoldNavBarHost.ApplyEffectiveAppearance`, so scroll-materializing bars flip the icons at
   the threshold. Outranks declarations: chrome above content is the actual surface.
3. **Declared style** — author intent over the page's own content (photos the sampler would
   read "wrong" for aesthetic consistency).
4. **Pixel sample** — ground truth of the rendered strip under the status bar: Android
   `PixelCopy` into a 32×4 bitmap (API 26+; the copy scales, cost is negligible), iOS a scaled
   `DrawViewHierarchy` snapshot. Event-driven + debounced (80 ms), refreshed at
   presentation-settle points (never a mid-transition frame), last sample kept until replaced
   (no flicker). Handles photos/scrims no semantic rule can know — the DailyHelper hero needs
   NO declaration.
5. **Semantic page surface** — top-spanning first child (`SafeAreaEdges` None) background, else
   the page background.
6. **Theme default**, re-resolved on `RequestedThemeChanged`. This also fixes two Android
   staleness bugs on system theme toggles WITHOUT activity recreation (`ConfigChanges.UiMode`):
   status-bar icon appearance (`windowLightStatusBar` resolves only at creation) and the bottom
   bar's `navigationBarColor` (re-resolved from the night-aware theme and re-applied — 3-button
   navigation honors an opaque color even on Android 15+).

Platform application: iOS via `ScaffoldViewController.PreferredStatusBarStyle` (UIKit fades;
verified end-to-end through MAUI's root VC chain), Android via `WindowInsetsControllerCompat`.
Tests: 12 resolver unit tests + `ScaffoldSystemBarTests` (6 scenarios per platform) asserting on
PLATFORM ground truth read in-app (iOS effective `StatusBarManager.StatusBarStyle`, Android
`AppearanceLightStatusBars`).

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
- **Learned (July 2026)**: modern Android (predictive-back enforcement, targetSdk 36+) never
  dispatches the legacy `KEYCODE_BACK`/`onBackPressed` channel — MAUI's own
  `Page.OnBackButtonPressed` contract is dead there app-wide (Shell included). The dispatcher
  callback's `Enabled` flag must be maintained ahead of the gesture (updated after every
  presenter synchronization). Automation drivers (DevFlow Back) don't press the system key
  either — they go through `INavigation`, served by the §4.1 bridge.

---

## 7. Modals vs popups & sheets — two different families (decided)

**Modal pages are navigation. Popups and sheets are NOT** — they are presented UI, offered by a
separate mechanism the navigation engine never sees.

### 7.1 Modal pages (navigation) — IMPLEMENTED (July 2026)

- Modal pages stay ordinary navigation stack entries (routes, guards, lifecycle, DI scoping,
  snapshot/restore all apply). No dedicated modal presentation engine: the transition spec,
  chrome policies and back gating built for §5/§8 compose into modal presentation.
- **`Scaffold.PageMode` attached property** (`Default` | `Modal` | `DismissableModal`):
  - enters with `ScaffoldPageTransition.SlideFromBottom` by default (an explicit page-attached
    `Scaffold.PageTransition` still wins; resolution page-spec → modal default → scaffold → Default);
  - forcefully covers the tab bar (`ComputeTabBarVisible` = false, hides animated with the push);
  - NO interactive back preview (iOS edge swipe and Android predictive back refuse to begin);
    Android system back still COMMITS through the engine, where `ILeavingGuard` decides;
  - default nav bar shows title only — back chevron and drawer buttons hidden; `DismissableModal`
    adds a trailing `ScaffoldCloseButton` (X, new primitive, binds `IsCloseButtonVisible` +
    `BackCommand`); `ScaffoldNavBarContext` gains `IsModal`/`IsCloseButtonVisible`.
- The engine's `NavigationStackPage.IsModal` (Shell `PresentationMode`-derived) is untouched —
  presenters key off `Scaffold.GetPageMode(page)` directly.
- MAUI's own `Navigation.PushModalAsync` on the page: out of scope / unsupported inside Scaffold
  (document it; all page navigation goes through Nalu).
- Harness: "Scaffold Modal Tests" + ScaffoldModalChromeTests (tab-bar cover, X close, plain
  modal programmatic close, Android system-back pop) — green on both platforms.

### 7.2 Popups & sheets — IMPLEMENTED (July 2026; the former "v2" feature landed early)

Implemented in three phases on the §5.6 overlay STACK (see the phase commits), plus a
model-first MVVM layer. **No route, no stack entry, no navigation lifecycle in the engine,
no guards, excluded from snapshot/restore** — as originally decided.

- **Low-level (view-first) API** on `Scaffold`: `ShowPopupAsync(View, ScaffoldPopupOptions?)` /
  `ShowBottomSheetAsync(View, ScaffoldBottomSheetOptions?)` returning the `IScaffoldPopup`
  lifetime handle (`IsOpen`, `Closed` — completes on EVERY close path — `CloseAsync`,
  `IAsyncDisposable`); `ShowTabBarPanelAsync(View, Brush?, bool closeIfOpened)` /
  `CloseTabBarPanelAsync` for the bottom-chrome panel (toggle or replace-in-place).
- **Overlay stack semantics**: entries stack in open order, each above its own Brush scrim
  (gradients supported; transparent = dropdown, always input-blocking); scrim tap and system
  back dismiss the TOPMOST entry (per-entry policy flags); navigation commits dismiss ALL;
  the tab bar panel keeps its below-strip z-slot. Popups/sheets ignore chrome insets entirely —
  only SYSTEM insets shape the presentation area (Android: insets are CONSUMED at the overlay
  boundary or MAUI's net10 inset handling displaces inner layouts).
- **Placement**: Center / anchored (Below|Above|Start|End, auto-flip, RTL-mapped, clamped) /
  `IScaffoldPopupPlacer` full custom; `Margin` insets the area; content
  `MaximumWidth/HeightRequest` participate in the measure. Shared cross-platform resolver;
  presenters only supply area + anchor frame + measured size (via VIRTUAL measure/arrange —
  a manually-framed platform view leaves the MAUI Frame invalid and iOS silently skips
  transforms).
- **Sheets are Nalu-drawn** (no native sheet — cross-platform consistency; the iOS 15 floor
  question is moot): public stylable `ScaffoldBottomSheetView` (SheetBackground,
  SheetCornerRadius, HandleColor per §5.7), detents `Content|Fraction|Height` clamped to the
  available height, WHOLE-SHEET pan at the virtual view layer (inner gesture-hungry controls
  stop propagation themselves; drag clamps at the largest detent; pull past the smallest
  dismisses), `SnapToDetentAsync`, `MaxWidth` for tablets (floats centered).
- **Attached presentation properties** (`ScaffoldPopup.*` / `ScaffoldBottomSheet.*` on the
  CONTENT view) declare how a view prefers to be presented; call-site options are ALL-NULLABLE
  and override per property: `caller ?? attached ?? default`.
- **MVVM layer** (`IOverlayService`, registered by `UseNaluScaffold`): model-first
  `Show{Popup|BottomSheet}Async<TModel[, TResult]>(object? intent, options?)` mirroring the
  navigation engine — intents delivered to `OnEnteringAsync(TIntent)` via the same
  reflection-dispatch pattern (AOT-safe: `AddOverlay<TModel, TView>` annotations preserve
  members; registrations are trim-safe closures, factory overload as the zero-magic hatch).
  Construction: per-presentation DI scope; a wrapper provider serves the non-generic
  `IOverlayRef` → model via `ActivatorUtilities` → view likewise with the model resolvable —
  each ctor declares only what it needs (ONE public ctor per type). The model closes via
  `IOverlayRef.CloseAsync(result)` — result type runtime-checked against the Show call's
  `TResult` (throws on mismatch); the caller's task completes with the result, or `default`
  on any dismissal. `ILeavingAware` + `IDisposable`/`IAsyncDisposable` run on close; a close
  requested during `OnEnteringAsync` skips presentation entirely.
- Win confirmed: popups never enter the navigation pipeline — no popup special-casing anywhere.

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

> **Shared elements IMPLEMENTED (July 2026), verified on both platforms.**
> `Scaffold.TransitionName` attached property (the `transitionName` analogue): matching names on
> the outgoing/incoming pages animate between their geometries during push/pop, riding the
> standard slides.
> - iOS: PoC spike A engine ported into the library
>   (`Platforms/iOS/ScaffoldSharedElementTransitions.cs`) — flight overlay in a single
>   `UIViewPropertyAnimator` (seekable by construction), image aspect morph with corner radii
>   read from the LIVE views (PoC's hardcode removed), transform-match cross-fade for any other
>   pair, incoming-layout gate with plain-slide fallback.
> - Android: PoC spike B pattern in the fragment path — `transitionName` stamped on tagged
>   views, `AddSharedElement` + native androidx `TransitionSet`
>   (ChangeBounds/Transform/ImageTransform/ClipBounds) built fresh per transaction (managed
>   Transition subclasses break on clone), `postponeEnterTransition` + pre-draw as the
>   readiness gate. Both push AND pop wire pairs as enter transitions (Replace-based, no back
>   stack).
> - Android gotchas (measured with logcat timing, July 2026): the fragment framework IGNORES
>   `OnCreateAnimator` animators on transition-involved fragments, so (a) the incoming page's
>   slide must be a transition-framework `Slide` set as `EnterTransition` (shared pairs are
>   excluded from it automatically), and (b) the presented signal fires at first pre-draw for
>   postponed fragments — wiring it to the (never-running) animator end made every SET push eat
>   the full 2 s settle timeout, which blocked the engine and silently swallowed pop taps
>   (perceived "freeze on pop"). Also: exit choreography must come from the CURRENT
>   navigation's hint (`PrepareRemoval(hint)` on the outgoing fragment), not the hint the
>   fragment was created with — the creation-hint test made the old page slide out on push and
>   left real pops with no exit slide.
> - Verified: mid-flight frames captured on both platforms; `ScaffoldTransitionChromeTests`
>   (end-geometry + flight cleanup restoration + repeated round trips) green on both.
> - iOS interactive pop (left-edge swipe) IMPLEMENTED (July 2026): plain-pan recognizer with
>   manual edge/direction gating on the content container; peek-mounts the page below
>   (presentation-only); scrubs the pop choreography via MANUAL per-fraction interpolation
>   (`ScaffoldPopAnimationSession` + `IScrubElement`s); release either reverses or settles
>   forward and dispatches the pop through the engine — the sync adopts the settled visuals via
>   a handoff without re-animating. Guard gating: `BindingContext is ILeavingGuard` blocks the
>   gesture (sniff at begin + re-check at release).
> - iOS 26 gesture/animator gotchas that shaped the implementation (all measured, sim + device):
>   (1) `UIScreenEdgePanGestureRecognizer` misfires erratically — its begin-time edge test
>   consumes STALE recognizer state; unusable. (2) Inside `TouchesBegan`, a recognizer's
>   `LocationInView` still returns the PREVIOUS gesture's end position — read the `UITouch`
>   from the `touches` set instead (this was the every-other-swipe-fails bug). (3) A PAUSED
>   `UIViewPropertyAnimator` accepts `FractionComplete` (state Active, read-back correct) but
>   never renders the interpolation — started animators work, paused-scrub does not; hence the
>   manual interpolation. (4) `ShouldBeRequiredToFailBy` against scroll pans deadlocks with the
>   scroll view's `delaysTouchesBegan` gate (touches flushed compressed at release); no failure
>   requirements are needed — vertical-only scroll views don't engage on horizontal edge drags.
>   (5) Transform-match pairs live INSIDE the sliding page: the view on the MOVING page must
>   compensate the page translation or the pair rides the slide (the "Bot hero flies in from
>   the right" bug).
>   (6) During the INTERACTIVE scrub, transforms set directly on MAUI-managed views never
>   render: each per-frame page-transform change invalidates the container layout and MAUI's
>   arrange pass re-sets the pair views' frames, visually cancelling the transform (alpha and
>   background survive; proven with a rotation probe). The interactive session therefore flies
>   label/generic pairs as OVERLAY SNAPSHOTS (cross-dissolve along the lerped geometry, live
>   views hidden via alpha) — the same overlay strategy that makes the image flight immune.
> - Android predictive back IMPLEMENTED (July 2026): `OnBackPressedCallback`
>   HandleOnBackStarted/Progressed/Cancelled/Pressed. Started peek-mounts the page below
>   (presentation-only) beneath the fragment container and sniffs `ILeavingGuard` (guarded
>   pages: no preview, committed back still routes through the engine → guard runs);
>   Progressed scrubs page motion only (v1, translationX up to 40% width); Cancelled slides
>   home and unmounts the peek; Pressed settles the top page fully offscreen, then dispatches
>   the engine pop with a handoff — the sync adopts the settled state (no exit animator, no
>   SET) and the new fragment re-parents the very platform view the peek was showing.
>   Root pages keep the callback disabled → native back-to-home preview. Requires
>   `android:enableOnBackInvokedCallback="true"` (TestApp manifest) + gesture navigation.
> - Android predictive-back + shared elements gotcha (FIXED July 2026): the push's SET machinery
>   hides the OUTGOING source view via `setTransitionAlpha(0)` — invisible to `getAlpha()`,
>   drawable/visibility/matrix/clip all read healthy — and relies on the paired return SET to
>   restore it. The predictive-back pop skips the SET (handoff adopts settled visuals), so the
>   hero stayed permanently undrawn ("hero image missing after slow predictive back"; the slow
>   scrub is just when it's noticeable — any committed predictive back reproduced it). Ruled
>   out by instrumentation: Glide drawable clears (drawable present at every checkpoint) and
>   visibility/alpha/matrix/clip residue. Fix: the presenter records the outgoing source views
>   at AddSharedElement time (`ScaffoldPageRestore.CaptureSharedElementSources`, a
>   ConditionalWeakTable keyed by page — no tree walk); `Repair` then resets `TransitionAlpha=1`
>   + re-runs visibility/opacity/translation/scale/rotation mappers on exactly those views,
>   one-shot, from fragment remount (OnCreateView) and predictive-back peek (StartBackPreview).
>   Regression test:
>   `ScaffoldTransitionChromeTests.PredictiveBackRestoresSharedElementRendering` — pixel-samples
>   the hero after a stepped adb motion-event scrub (`NaluApp.PredictiveBackScrubAsync`; a plain
>   `input swipe` commits as a canned fling and can even dispatch a second back).
> - Still P2: predictive-back SET seeking (Android follow-up). Per-navigation
>   `WithTransition(...)` was DROPPED (decided July 2026): page/scaffold-level
>   `Scaffold.PageTransition` covers the use cases without threading the spec through
>   `INavigationInfo`.
> - **Predictive-back SET seeking — investigated (July 2026), design parked:**
>   - Availability: the Scaffold already resolves Xamarin.AndroidX.Transition **1.6.0.1**
>     (seeking exists since androidx transition 1.5.0) at the low MAUI floor — binding surface
>     confirmed (`TransitionManager.ControlDelayedTransition` → `ITransitionSeekController`:
>     `CurrentFraction`, `AnimateToEnd/Start`, `IsReady`). Fragment 1.8.8.1, Activity 1.10.1.3.
>     No version bumps needed.
>   - The BUILT-IN fragment predictive-back seeking only works for back-STACK pops
>     (`FragmentManager` drives the seek controller when popping an `addToBackStack`
>     transaction). Our architecture is deliberately Replace-based with an engine-owned stack
>     (guards, awaitable intents) — adopting the fragment back stack to get free seeking would
>     hand system back to the FragmentManager and bypass the engine. Ruled out.
>   - The viable route is a MANUAL scrub: on HandleOnBackStarted peek-mount the below page
>     (as today, incl. `ScaffoldPageRestore.Repair` — see next point), stage the below page's
>     pair views at the top page's geometry, call `ControlDelayedTransition(pageLayer, set)`
>     (ChangeBounds/Transform/ImageTransform/ClipBounds + Slide for page motion), restore the
>     natural layout as the end state, then drive `CurrentFraction` from back progress;
>     commit → `AnimateToEnd` + engine pop handoff; cancel → `AnimateToStart` + unmount. This
>     re-implements the fragment machinery's shared-element staging (`setSharedElementState`)
>     by hand — bounded but substantial.
>   - **It does NOT replace the transitionAlpha repair**: the `setTransitionAlpha(0)` hide and
>     its restore live in the FRAGMENT transition machinery (DefaultSpecialEffectsController
>     pairs first-out/last-in shared views); a manual `TransitionManager` scrub runs outside
>     that machinery, so nothing would restore the hidden sources — the repair must in fact run
>     BEFORE the start-state capture so the below page's pair views are drawable at all.
>     Seeking is a pure UX upgrade (hero morphs under the finger, parity with the iOS
>     interactive pop), not a correctness fix.

> **§8.1 ADDENDUM — truthful-flights overhaul (August 2026).** Frame-by-frame capture of the
> DailyHelper hero exposed quality gaps in both engines; both were reworked:
>
> - **iOS**: flights now travel between CLIP-INTERSECTED VISIBLE rects (the detail photo's
>   120pt parallax bleed corrupted end geometry → crop shift + snap at overlay removal);
>   non-image pairs fly as PRE-RENDERED stretchable copies INSIDE the overlay, stacked
>   images-first then larger-below-smaller (they used to ride live-view transforms UNDER the
>   opaque photo flight — invisible until the end); `RenderedCopy` replaces `SnapshotView`
>   (afterScreenUpdates capture races the alpha-hiding of pair views → blank copies);
>   `CATransaction.Flush()` before `StartAnimation` (the freshly-mounted page's first render
>   commit was eating the opening ~30% of the flight); corner radii also read from a MAUI
>   `Border` ancestor (MAUI clips via mask layer, invisible to `Layer.CornerRadius`); cleanup
>   restores ORIGINAL pair alphas (a 0.32 scrim forced to 1 turned opaque black). Scrims can be
>   paired (`weather-scrim` in the DailyHelper) so photo dimming stays constant mid-flight.
> - **Android: the native androidx SET is GONE** — replaced by a custom overlay engine
>   (`Platforms/Android/ScaffoldSharedElementTransitions.cs`) mirroring the iOS choreography.
>   The SET could not animate corner radii (rounded cards snapped square at pop end), could not
>   scale text (label pairs teleported), could not cross-fade pairs, gave no stacking control —
>   and managed `Transition` subclasses lose their peer on the framework clone, so it could not
>   be extended. Source side captured at presenter commit (page at rest); destination measured
>   at the incoming fragment's first pre-draw (`OnFirstPreDraw` hook replaces the postpone
>   machinery); flights live in the fragment container's `ViewGroupOverlay` (above both pages,
>   immune to MAUI layout), driven by ONE `ValueAnimator` through `Apply(progress)` elements —
>   geometry frames measured relative to the PAGE ROOT (immune to the page's slide transform),
>   image drawables captured at prepare time (async pipeline may clear them a frame later), and
>   any failure degrades to the plain slide (live views hide only after every flight built).
>   Page motion returns to the normal fragment animators, so shared and plain navigations move
>   identically. `ScaffoldPageRestore` stays as a no-op safety (the SET's `setTransitionAlpha`
>   hiding is gone with it).
> - **Consequence for the parked seeking design above**: its premise (fragment SET machinery)
>   no longer exists. The new engine's `Apply(progress)` elements are EXACTLY the scrub surface
>   the iOS interactive pop uses — wiring shared-element flights into the predictive-back peek
>   is now a bounded follow-up, not a re-implementation of `setSharedElementState`.

### 8.2 Cross-platform API (independent of engine choice)

- `Scaffold.TransitionTag="photo-{id}"` attached property on any `View` (the `transitionName` analogue).
  Matching tags on outgoing/incoming pages animate automatically.
- **Customizable push/pop page transitions — IMPLEMENTED (July 2026)**: public
  `ScaffoldPageTransition(Enter, Behind, DurationSeconds)` + `ScaffoldTransitionMotion`
  (fractional translate / scale / opacity) with built-ins (Default = stock slide,
  SlideFromRight w/ parallax, SlideUpFade, ZoomFade, None). Both engines interpret the spec
  natively (iOS animation blocks + the manual-scrub interactive session; Android
  `Fragment.OnCreateAnimator` PropertyValuesHolder animator — NOT a managed Transition
  subclass). Pop replays the PUSHED page's spec reversed (the spec is resolved from the page
  that entered with it); the iOS edge swipe scrubs the same spec. Attachment:
  `Scaffold.PageTransition` attached property — page-level overrides scaffold-level; resolution
  page → scaffold → Default. Boundaries (by design): SlideStart/SlideEnd root/tab switches keep
  their dedicated both-pages slide and NEVER consult the spec; shared-element navigations keep
  the standard slide (the flight math assumes it). Harness: "Scaffold Page Transition Tests" +
  ScaffoldPageTransitionChromeTests (both platforms).
- Per-navigation `WithTransition(...)` on the fluent builder: **DROPPED (decided July 2026)** —
  the attached-property resolution (page → scaffold → Default) covers the use cases; no spec
  threading through `INavigationInfo`. Restore (§9) and programmatic bulk navigations run with
  transitions suppressed.

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

> **IMPLEMENTED (August 2026) — ENGINE-LEVEL, final design after two review pivots.**
> Restore lives in **Nalu.Maui.Navigation** (zero Scaffold coupling; the Scaffold is the
> VERIFIED host — Shell "should work" but is untested/unadvertised). Suites:
> `NavigationRestoreTests` (24 unit tests against the real engine + ScaffoldProxy) and
> `ScaffoldRestoreChromeTests` (3 DevFlow tests with REAL kill-and-relaunch via
> `NaluApp.RestartAppAsync` — adb force-stop / simctl terminate + relaunch), green on iOS
> simulator AND Android emulator. Conceptual doc: `conceptual_docs/navigation-restore.md`.

### 9.1 Design evolution (August 2026 review, recorded for posterity)

1. **v1 — developer-driven tracking** (`Track`/`Untrack` on a scoped service, contiguous
   tracked prefix): fully implemented and verified on iOS before being SUPERSEDED — the
   tracking rulebook (last-write-wins, untracked semantics, prefix rules) existed only
   because capture was implicit-but-gated.
2. **v2 — explicit `SetRestorePoint(builder)` + app-driven `TryRestoreAsync()`**: rejected
   because checkpoint pages would have to declare their FULL path (position coupling), and
   the decisive scenario — an app that must run an INITIALIZATION root before any
   destination — pointed at a better replay seam instead.
3. **v3 — FINAL: automatic capture + replay after the initial root's first appearing.**
   Restorability derives mechanically from intent serializability; the init flow always runs;
   explicit control shrinks to three methods on one singleton.

### 9.2 The final design (as implemented)

- **Automatic capture**: every successful `GoToAsync` re-captures the current stack; the
  TARGET page's entering intent is recorded (serialized immediately) at navigation time.
  Per-frame restorability: no intent ⇒ restorable; REGISTERED intent type ⇒ restorable with
  intent (plain objects, JSON as-is — NO marker interface; the `AddIntent<T>` registry was
  briefly dropped for full-name wire ids, then RESTORED for trim/AOT safety: its DAM
  annotations preserve the type members the reflection serializer needs); an unregistered
  intent (or serialization failure) ⇒ the restorable stack ENDS at that page.
  Non-serializable intent
  state is excluded with `[JsonIgnore]` and REHYDRATED at replay: before navigating with a
  restored intent the engine walks the already-restored stack TOP→ROOT and awaits the first
  lifecycle target implementing `IIntentHydrator<TIntent>` (`HydrateAsync(intent)` fills the
  missing properties; the initialization root qualifies — it is alive during the replay).
  Pop-ending navigations never record (pop intents are appearing context). Root selection +
  root intent captured the same way (boot intent via an `OnRootEntered` hook).
- **`INavigationRestore`** (singleton, always injectable, inert when not enabled):
  - `ForgetAsync()` — removes the CURRENT page from the restoration stack (restore lands on
    the page below; pages above cannot restore). Wizard pages call it in `OnEnteringAsync`.
  - `RestoreWithIntentAsync(ISerializableIntent)` — sets/replaces the current page's replay
    intent (draft → saved-id swap; also re-opts-in a page reached with an opaque intent).
  - `TryStopRestoreAsync()` — discards the pending/in-flight restore and lifts the
    suppression window (the auth-redirect escape hatch); returns whether one existed.
  - The CURRENT page is deduced via `NavigationHelper.AmbientLifecyclePage` — an
    `AsyncLocal<Page?>` set by an async wrapper around every lifecycle invocation (visible
    inside the callback even mid multi-push, where the page is not on the committed stack
    yet; no leak into the engine context) — falling back to the stack top (command context).
  - Both per-page methods persist the re-captured snapshot before completing.
- **Boot & replay** (inside `NavigationService.InitializeAsync`): read-and-DELETE + validate
  the snapshot BEFORE booting; the engine still boots the CONFIGURED initial destination (an
  app's initialization root always runs); after the initial page's first `OnAppearingAsync`
  completes, the replay executes — absolute navigation to the captured root (count==1
  delivers the root intent), then chunked pushes (each chunk ends at an intent-carrying
  frame). Once per app launch; re-persists immediately after (the snapshot was deleted at
  boot). Known edge (documented): a captured root intent is NOT redelivered when the
  captured root IS the configured initial root (already entered with the app's own intent).
- **Suppression window** (validated-snapshot → replay end): non-replay `GoToAsync` calls are
  IGNORED — `false` + `NavigationIgnored` lifecycle event (replay navigations bypass via an
  AsyncLocal flag set INSIDE the dispatched flow). Each replay step is enqueued via
  `DispatchAsync`, so auto-navigations dispatched by restored pages' lifecycles drain BEFORE
  the next replay step — deterministically suppressed, not raced; the window lifts just
  BEFORE the LAST replay navigation, so the final restored destination keeps its right to
  auto-navigate (queue order makes lifting after impossible: its dispatched redirect is
  already ahead of any continuation of ours). `TryStopRestoreAsync` is the deliberate way
  out (auth flows).
- **Validation header**: schema version + app version/build (AppInfo, test-hookable) + SHA256
  route hash (ordered root segments from the proxy tree + registered page segments — Mapping
  values AND view-only registrations via `NavigationConfigurator.ViewOnlyPages`, a gap found
  on-device: view-only pages never enter `Mapping`) + `MaxAge` option.
  Fail-open everywhere: unknown segment/intent id truncates the prefix at that frame; any
  exception discards the snapshot; restore never bricks startup.
- **Persistence**: capture on successful commits, debounced (500ms) background writes,
  immediate flush on window Deactivated/Stopped (best-effort `Application.Current` hook) and
  after the per-page methods/replay. Store: `INavigationRestoreStore` (default: atomic JSON
  file in the app cache). A non-restorable ROOT writes an intentionally invalid snapshot —
  restoring nothing must not resurrect an OLDER state.
- **Registration**: `UseNaluNavigation(nav => nav.WithRestore(r => ...))` —
  `NavigationRestoreOptions` { `Enabled` (default true; DEBUG-only policy is app-side — the
  library cannot see the app's build configuration), `MaxAge`, `AddIntent<T>(stableId)`
  (collision-checked; never assembly-qualified names), `IntentSerializerContext` }.
  Services always registered (inert): `INavigationRestore`, `IIntentSerializer` (default:
  STJ reflection, source-gen context override for trimming/AOT), `INavigationRestoreStore`.

### 9.3 Dropped along the way (deliberate)

- `Track`/`Untrack`, `IsRestoring`, `RestoreCompleted`, `SetRestorePoint`, boot-destination
  override (snapshot never overrides the startup destination anymore), `HasPendingRestore` +
  `DiscardAsync` (merged into `TryStopRestoreAsync`), whole-stack drop-and-stop-listening
  (not needed for now), and the `ISerializableIntent` marker (plain objects serialize as-is;
  `[JsonIgnore]` + `IIntentHydrator<T>` for non-serializable intent state). The `AddIntent<T>`
  registry was dropped too (full-name wire ids) and then RESTORED: under trimming an
  unregistered intent's members can be stripped (silent empty deserialization) and NativeAOT
  needs the DAM annotations + `IntentSerializerContext` story.

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

> **Status**: engine-driven lifecycle (Entering/Appearing/Disappearing/Leaving/guards/dispose)
> verified byte-identical to the Shell host on both platforms (the `NavLog` sequences match).
> Verified: covered pages are detached, never destroyed — platform view state (scroll offset,
> entry text) survives push/pop on both platforms; disposal-on-pop asserted by the leak checks.
> Added (August 2026): the Scaffold raises MAUI's `NavigatedTo`/`NavigatedFrom` on page swaps
> (`ScaffoldPageNavigationEvents`, `[UnsafeAccessor]` into the internal senders with
> `disconnectHandlers: false` — the scaffold preserves covered pages) so `HasNavigatedTo`-gated
> MAUI features (`HideSoftInputOnTapped`) work on Scaffold pages.
> Still pending: forwarding `ViewDidAppear`/`Disappear` from `ScaffoldViewController` into MAUI's
> page-appearing events for the scaffold page itself (lands with the chrome work).

---

## 11. Testing strategy

- Every behavior lands with a **TestApp page** (`Samples/Nalu.Maui.TestApp/Tests/`, `[TestPage]`)
  and a **DevFlow UI test** (`UITests/UITests.DevFlow`, via the `NaluApp` wrapper — extend the wrapper,
  never call `AgentClient` from tests). See the `maui-devflow-uitests` skill.
- IMPLEMENTED: the navigation UI suite is an abstract base (`NavigationTestsBase`) parameterized by
  harness — `NavigationTests` (NavShell) and `ScaffoldNavigationTests` (NavScaffold) run the SAME
  scenarios against both hosts, with per-host variance points (tab-bar chrome availability,
  native-back support, multi-pop leak expectation — the Scaffold asserts `Leaked:0` where Shell-iOS
  has its documented residue). Harness fixtures for state preservation (`HomeStateEntry`, filler
  rows) and flyouts (`GlobalFlyout`/`SettingsFlyout` + open buttons) are in place for future tests.
- **P0 exit criterion**: the existing `NavigationTests` suite passes against a Scaffold-hosted TestApp
  variant (proves the host seam holds).
- Transitions: DevFlow recording for visual verification; assertions on end-state + lifecycle event
  sequences (the `NavigationEvent` telemetry hook gives deterministic assertions where pixels can't).
- Leak detector runs in TestApp (existing `LeakTracker`) — every pop/destroy path asserted.
- Restore: kill-and-relaunch test flow (DevFlow can restart the app; assert landing location + intents).

---

## 12. Phasing

### P0 — seam spike (de-risk, throwaway-quality allowed)

> **Status (July 2026): COMPLETE — exit gate passed.** Contracts stay internal (IVT), engine
> untouched (538 unit tests green). Real `ScaffoldProxy` + area/root proxies unit-tested against
> the real engine; presenters: iOS child-UIViewController containment, Android fragment hosting;
> system back via `OnBackPressedDispatcher` (guard-aware, preview-preserving at root);
> `INavigation` bridge (`page.Navigation` truthful stack + pops routed through the engine);
> scroll/entry state verified preserved across push/pop on both platforms.
> **Exit gate: the DevFlow `NavigationTests` suite runs against BOTH hosts
> (`NavigationTests` = NavShell, `ScaffoldNavigationTests` = NavScaffold) and is green on iOS
> and Android** — 35 passed / 0 failed per platform. Skips (by design until P1/P2): the tab-bar
> chrome test on the Scaffold; native-back on Scaffold-iOS (no nav bar yet); native-back on
> Shell-Android (upstream: MAUI Shell still uses the legacy back channel, dead under
> predictive-back enforcement). Bonus: the Scaffold does NOT exhibit Shell's documented iOS
> multi-pop leak (asserted at `Leaked:0`).
- Contracts promoted/renamed in Nalu.Maui.Navigation; the two `NaluShell` couplings removed; Shell host still green.
- Bare `Scaffold`: one `ScaffoldArea`/one stack, push/pop with a simple slide, modal push.
- Correct Appearing/Disappearing, DI scopes, handler disconnect, leak detector.
- **Exit**: existing DevFlow `NavigationTests` pass on the Scaffold-hosted TestApp.

### P1 — structure & chrome

> **Status (August 2026): COMPLETE.**
> ✅ Full hierarchy incl. cross-area/root navigation with stack preservation (exercised by the
>   shared UI suite). ✅ Back policy per §6.
> ✅ Tab bar (§5.3) + §5.6 overlay primitive + §5.4 bottom-inset distribution.
> ✅ Nav bar (§5.2, incl. the appearance/scroll revision) + §5.4 top-inset distribution.
> ✅ Flyout completion (§5.5): modes, default menu template, options/styling, RTL, controller.
> ✅ Modal pages (§7.1) via `Scaffold.PageMode`.
> ✅ Chrome styling model (§5.7) applied across all chrome.

- Full hierarchy: `ScaffoldArea` base, `ScaffoldTabBar` (default template + custom view replacement,
  stack preservation, active-tab-pops-to-root), cross-item/stack navigation.
- Start/End flyouts with resolution order (Page → Area → global), default template + custom content.
- Minimal nav bar (title, TitleView, back button, toolbar items, visibility), safe-area/edge-to-edge.
- Back policy per §6 (system back interception; no gestures yet).
- **Exit (met)**: a real-world sample app shape (tabs + drawer + modals) fully navigable with
  guards — see `Samples/Nalu.Maui.DailyHelper`.

### P2 — transitions & gestures

> **Status (August 2026): COMPLETE** (see §8). Declarative `ScaffoldPageTransition` spec +
> attached resolution, shared elements (`Scaffold.TransitionName`), iOS interactive edge-swipe
> pop, Android predictive back. Popups & sheets (§7.2) also landed in this window despite being
> planned for v2. Remaining follow-ups, parked: predictive-back SET seeking (§8.1 —
> UX upgrade, not correctness), flyout edge-swipe open.

- PoC spikes A + B → engine decision → implement engine + `TransitionTag` API
  (`WithTransition(...)` dropped July 2026 — attached-property resolution covers it).
- Platform-parity push/pop animations, then shared elements, then interactive pop (iOS) and
  predictive back (Android), honoring the guard policy.
- **Exit (met)**: photo-grid→detail scenario shipping quality on both platforms, interruptible.

### P3 — restore, deep links, polish

> **Status (August 2026): snapshot/restore DONE** (§9 — engine-level, both platforms,
> kill-and-relaunch verified). Remaining: deep links, iOS 18 zoom opportunism.

- ✅ Snapshot/restore per §9 (DEBUG DevEx first; production is the same mechanism behind
  `Enabled`/`MaxAge` policy).
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
| Contract promotion breaks Shell host subtly | Shell host kept green in CI/unit tests throughout P0 (verified: zero engine changes needed). |
| Two tab bars to maintain (Shell NaluTabBar + Scaffold) | Accepted short-term; Shell variant is feature-frozen once Scaffold ships. |
| Newest-Android behavior drift (predictive-back enforcement killed legacy back; suspected NaluTabBar layout regression on API 37) | Keep an up-to-date emulator in the local test loop; the parameterized UI suite catches host-visible drift on both hosts. |
| Automation tooling (DevFlow) hard-codes Shell/NavigationPage assumptions | `INavigation` bridge serves the pop/stack channel; system-back tests use the real dispatcher path; revisit when DevFlow matures. |

---

## 14. Open questions (settle during design review)

1. ~~Final public names for the promoted contracts~~ — RESOLVED: contracts stay `internal`
   (`InternalsVisibleTo` to the Scaffold, see §4); naming is now an internal concern.
2. ~~Terse XAML forms for single-stack areas~~ — RESOLVED: implicit conversion operators
   (parse-time composition of real elements, see §3).
3. Flyout items targeting a specific stack inside an item — v1 proposal is **no** (item-level only).
4. ~~Modal presentation config~~ — RESOLVED (July 2026): per-page attached property
   (`Scaffold.PageMode`, §7.1); no push-time builder option (consistent with dropping
   `WithTransition(...)`). Popups/sheets shipped early as the separate non-navigation family
   (§7.2) with Nalu-drawn sheets.
5. Drawer "locked/side-by-side" mode — post-v1, but does the v1 API shape need to reserve room for it?
6. Snapshot storage location & retention policy (cache dir, single slot vs per-build slot).
7. Does `Scaffold` need a Shell-style "current page changed" public event surface beyond the existing
   `NavigationEvent` telemetry? (Consumers may want it for analytics.)
8. `Scaffold` currently derives `ContentPage` only as a historical artifact (its `Content` is dead
   weight since the lean handler ignores it) — drop to `Page` at the next API review?
9. ~~Flyout API naming/shape~~ — RESOLVED (July 2026): implemented per §5.5
   (`ScaffoldFlyoutMode` incl. `Disabled` as the suppression sentinel, `ScaffoldFlyoutOptions`
   for width/scrim styling, `IScaffoldFlyoutController` page scope, RTL mapping).
10. Should `ScaffoldViewController` forward appearing/disappearing to the scaffold page's MAUI
    events (parity with PageHandler-hosted pages)?

---

## 15. Field notes — hard-won facts from the implementation

Platform and framework behaviors discovered while building P0/P1; they shaped decisions above and
will bite again if forgotten:

- **net10 `MauiWindowInsetListener` IME gate (August 2026, diagnosed frame-by-frame + via
  reflection probes)**: while an IME animation is in flight the listener sets an internal
  `IsImeAnimating` and SWALLOWS every `OnApplyWindowInsets` dispatch (parking only the LAST
  view as `_pendingView`); combined with padding being reset on detach, a page (re)attached
  mid-hide keeps ZERO safe-area padding until the animation ends (~250ms) and then jumps.
  Nothing can bypass it: manual `DispatchApplyWindowInsets` into the subtree is swallowed too,
  and the state is unobservable from outside — the listener registers animation callbacks with
  `DispatchModeStop` (events never reach non-MAUI views; DecorView and `android.R.id.content`
  callbacks receive nothing), and in the default soft-input mode the IME never appears in the
  window's insets at all (`ime=0, visible=false` with the keyboard fully open). Accepted as a
  KNOWN LIMITATION (§0); the only working fix was polling the flag via reflection — rejected.
- **MAUI element tree**: `Page.OnParentSet` throws unless the parent is a Page, Window/Application,
  or `BaseShellItem` (Shell's private carve-out). Scaffold-hosted pages are therefore logically
  parented to the **Scaffold itself**, wired inside the stack model so every mutation path stays
  correct. Without logical parenting, pages are invisible to the visual tree (DevFlow) and lose
  `Window` resolution.
- **Predictive-back enforcement** (Android 16+/targetSdk 36): the legacy back channel
  (`KEYCODE_BACK` → `onBackPressed` → MAUI `Page.OnBackButtonPressed`) is never dispatched.
  `OnBackPressedDispatcher` is the only channel, and the callback's `Enabled` state is consulted
  BEFORE the gesture — any "ask the page at event time" contract is structurally impossible at
  stack roots. This drove §6: `ILeavingGuard` as the one confirmation mechanism.
- **Back-at-root finishes the activity** (observed API 37): relaunch = new activity over a live
  process. Presenters must treat "host platform view changed" as full reset; handler-owned
  presenter-per-connection makes this structural. Same path covers configuration changes.
- **Fragment hosting traps** (PoC-discovered, revalidated in the library): only async
  `Commit`/`CommitAllowingStateLoss` (MAUI's own `ScopedFragment` transaction may be executing);
  managed `androidx.transition.Transition` subclasses crash on `Transition.clone()` (managed peer
  loss) — animator-based `Fragment.OnCreateAnimator` is the supported, seekable path;
  `IOnBackStackChangedListener` requires implementing ALL newer default interface methods.
- **Android binding overload traps**: `LinearGradient(..., int, int, ...)` silently binds to the
  color-long overload → runtime ColorSpace crash; prefer array overloads.
- **iOS window-root hosting**: MAUI installs the handler's `IPlatformViewHandler.ViewController`
  as the window's root VC — a lean `ViewHandler` + interface reimplementation (ShellRenderer
  pattern) beats `PageHandler` (hidden page-pipeline behaviors). Child-VC containment then gives
  safe-area/appearance propagation natively; the same chain is the future carrier of
  `AdditionalSafeAreaInsets` chrome contributions (§5.4). Constraint: this holds because the
  Scaffold is the window root — document it (Shell has the same constraint).
- **State preservation model**: covered pages are detached, never destroyed — the page handler
  owns the platform view; presenters reparent the same instance (iOS: same VC re-added; Android:
  same platform view re-hosted by a NEW fragment). Verified: scroll offset and entry text survive
  push/pop on both platforms. Corollary: window-attachment-driven state (running animations,
  video surfaces) pauses while covered — same as Shell.
- **DevFlow's synthetic Back** is not the system key: it drives NavigationPage/Shell/`INavigation`.
  The Scaffold serves it via the `INavigation` bridge; MAUI's default `NavigationProxy` (no inner)
  would otherwise silently manipulate a FAKE stack — worse than throwing.
- **Engine batching truth** (from `ExecuteRelativeNavigationAsync`): guard checks commit
  mid-navigation and re-begin (an extra presenter sync showing the intermediate state is correct
  and required); pops run leaving lifecycle BEFORE `PopAsync` and the engine disposes those pages
  — the host model must reflect pops immediately or re-entry crashes on ghost entries.
- **Multi-pop leak**: Shell-iOS's documented pop-to-root renderer-tracker leak does NOT exist in
  the Scaffold (asserted `Leaked:0` in the shared suite) — evidence the leak is Shell-adapter
  specific, not engine-caused.
- **DevFlow environment**: two agent-enabled apps collide on port 9223 with half-registered
  brokers (kill the other app; `adb forward` vs iOS-simulator binds conflict on the HOST port —
  remove forwards when switching platforms). Identical-byte screenshots are the tell for a stale
  agent connection.
- **.NET 10 safe-area rule (§5.4 corollary — REVISED July 2026 after native-state measurement)**:
  page scrollables must use the DEFAULT `SafeAreaEdges` — iOS maps it to
  `UIScrollViewContentInsetAdjustmentBehavior.Automatic`, which natively applies the augmented
  safe area (system + chrome contribution) in plain child-VC containment: no
  `UINavigationController` ancestor needed, `AdditionalSafeAreaInsets` propagates from any
  `UIViewController`. Do NOT set `SafeAreaEdges(Container)` on scroll views: MAUI net10 maps it
  to behavior `Always` AND pads the native contentSize by the same safe-area thickness
  (`MauiScrollView.CrossPlatformArrange`: `height += _safeArea.VerticalThickness`) — the space
  is reserved twice, leaving a phantom scroll range of exactly the inset sum (user-visible as a
  half-empty page that scrolls). Related upstream defect: `ScrollViewHandler.iOS.MapRequestScrollTo`
  clamps to `ContentSize - Frame` WITHOUT `AdjustedContentInset`, so programmatic scrolls (and
  DevFlow synthetic swipes built on them) under-scroll by the safe-area amount — the bottom-probe
  test scrolls via the harness's native inset-aware `ScrollToEnd{name}` button instead.
- **MAUI `AutomationId` can only be set once** — reusing a templated view with a different id
  needs the id decided at construction time (a later set throws, surfacing as a DevFlow
  "tap failed" when it happens inside a tap handler).
- **iOS simulator deploys over a RUNNING app keep the stale bundle** (trimmer `linked/` cache +
  install skip, no error): always kill → build → `-t:Run` (see the maui-devflow-uitests skill).
