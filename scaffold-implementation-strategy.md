# Nalu.Maui.Scaffold — implementation strategy

> Status: **implementation in progress** — P0 complete (exit gate passed on both platforms),
> P1 partially done. Living document: per-section status blocks record what's implemented.
> Targets: **Android + iOS only** (Windows/Mac Catalyst out of scope).
> Created 2026-07-23 · last updated 2026-07-25

## 0. Status at a glance

**Done (verified on iOS simulator + Android emulator, all suites green):**

- Library `Source/Nalu.Maui.Scaffold` in `Nalu.slnx` (net10.0 / -android / -ios26.0; iOS floor 12.2;
  deliberately NOT in `Nalu.Pack.slnf` nor the `Nalu.Maui` meta package until releasable).
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

**Next steps, in recommended order:**

1. **Flyout completion** (§5.5): default template over `Areas`, width/styling API, RTL mapping;
   edge-swipe open lands with the transition engine (P2).
2. **Modal pages** (§7.1) — the one P0 contract surface not yet exercised end-to-end.
4. P2: transition engine port from the PoC (`PageTransition` spec, `TransitionTag`,
   interactive pop, predictive-back seeking — androidx seeking-version check still pending).
5. Housekeeping when releasable: add to `Nalu.Pack.slnf` + meta package, docfx pages.

**Known open issue (Shell host, not Scaffold):** NaluTabBar renders full-height tab items on the
Android API 37 emulator (visually broken, taps may fail; files untouched since commit `66c626f`) —
likely an Android 16/17 behavior change; needs its own investigation.

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
  grants from Core and Navigation; **not** in `Nalu.Pack.slnf` / `Nalu.Maui` meta package until
  there is something releasable (avoids publishing a stub on the next release tag).
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
> - Public primitives for custom bars: `ScaffoldBackButton`, `ScaffoldFlyoutButton` (Side),
>   `ScaffoldNavBarTitle` — drop-in, self-binding to the context.
> - §5.4 top insets: the bar fills the top strip (background under the status bar, safe area
>   consumed via .NET 10 `SafeAreaEdges`); measurement normalized to content height
>   (measured − consumed inset, the NaluShellItemRenderer net10 pattern); iOS per-page
>   `AdditionalSafeAreaInsets.Top`, Android top system-bars rewrite in the page layer.
>   Same keep-alive-offscreen + interruptible animation model as the tab bar; the nav strip
>   sits BELOW the tab strip in z-order (behind-chrome overlay scrims dim it).

- **Deliberately minimal API in P1** (title, back button, drawer buttons, title view).
  ToolbarItems, search boxes etc. are explicitly post-v1 — this is where Shell replacements die.
  Custom bars (full replacement + primitives) are the v1 escape hatch.
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
  support on any icon. Default accent = **Nalu logo wave blues**: `#68A3F1` on dark theme,
  `#2C479D`/`#3C64BC` on light — shipped as a built-in themed ResourceDictionary style.
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
- **Styling surface (decided; REVISED July 2026)**: the whole styling surface lives on the
  default template COMPONENT — public `ScaffoldTabBarView` — not on `ScaffoldTabBar`
  (an app installing a custom bar carries none of the default template's properties).
  `ScaffoldTabBar.TabBarView` defaults to a fresh `ScaffoldTabBarView` via the bindable
  property's default value factory; the component resolves its owning tab bar from the logical
  parent when presented. Plain `Style TargetType="ScaffoldTabBarView"` + `AppThemeBinding`
  covers theming. Property set —
  - *Bar container*: `BarBackground` (Brush), `BarCornerRadius`, `BarMargin`, `BarPadding`,
    `BarShadow` (Shadow), `BarHeightRequest`.
  - *Items*: `ItemWidth`, `TextColor`, `SelectedTextColor`, `FontFamily`, `FontSize`,
    `SelectedFontAttributes`, `SelectionPillBackground`, `SelectionPillCornerRadius`, `IconSize`.
  - *Badges*: `BadgeBackground`, `BadgeTextColor`, `BadgeFontSize`; the badge value is a
    per-root bindable (`BadgeText` attached/bindable property) so it's data-bindable per tab.
  - *Overflow*: `OverflowIcon`, `OverflowTitle` (localizable "More"), `ScrimColor`,
    `OverflowPanelBackground`, `OverflowPanelCornerRadius`, `OverflowPanelShadow`,
    `OverflowItemTemplate` (optional; default row otherwise).
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
> panel, tap-scrim closes, any navigation auto-closes. Still pending (P1): default template
> over Areas, edge-swipe open (transition-engine territory), width/styling API (design review),
> RTL mapping of Start/End.

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
- The Scaffold provides the **overlay layer** they render into (the shared primitive of §5.6 —
  above page + chrome, below nothing), with scrim and safe-area handling per §5.4; back handling
  policy: hardware/system back and back gestures dismiss the topmost overlay before the
  navigation engine is ever consulted.
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
> - Still P2: public `PageTransition` spec + `WithTransition(...)` builder option, interactive
>   pop (iOS scrub handle exists in the engine's construction), predictive-back SET seeking.

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

> **Status**: engine-driven lifecycle (Entering/Appearing/Disappearing/Leaving/guards/dispose)
> verified byte-identical to the Shell host on both platforms (the `NavLog` sequences match).
> Verified: covered pages are detached, never destroyed — platform view state (scroll offset,
> entry text) survives push/pop on both platforms; disposal-on-pop asserted by the leak checks.
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

> **Status (July 2026): in progress.**
> ✅ Full hierarchy incl. cross-area/root navigation with stack preservation (exercised by the
>   shared UI suite). ✅ Back policy per §6. ✅ Flyout STRUCTURE (attached props, resolution,
>   open/close, scrim, auto-close on navigation) — templates/styling pending.
> ✅ Tab bar (§5.3) + §5.6 overlay primitive (flyout refactored onto it) + §5.4 bottom-inset
>   distribution — implemented and verified on both platforms (July 2026).
> ✅ Nav bar (§5.2) + §5.4 top-inset distribution — implemented and verified on both platforms
>   (July 2026).
> ⬜ Flyout default template, width/styling API, RTL.
> ⬜ Minimal nav bar + §5.4 inset distribution.
> ⬜ Modal pages (§7.1).

- Full hierarchy: `ScaffoldArea` base, `ScaffoldTabBar` (default template + custom view replacement,
  stack preservation, active-tab-pops-to-root), cross-item/stack navigation.
- Start/End flyouts with resolution order (Page → Area → global), default template + custom content.
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
4. Modal presentation config: per-page attached property vs push-time builder option (or both).
   Popups/sheets are settled as a separate non-navigation family shipping in v2 (§7.2, uxd-popups
   reference); its sub-questions (native vs Nalu-drawn sheets, packaging) belong to the v2 review.
5. Drawer "locked/side-by-side" mode — post-v1, but does the v1 API shape need to reserve room for it?
6. Snapshot storage location & retention policy (cache dir, single slot vs per-build slot).
7. Does `Scaffold` need a Shell-style "current page changed" public event surface beyond the existing
   `NavigationEvent` telemetry? (Consumers may want it for analytics.)
8. `Scaffold` currently derives `ContentPage` only as a historical artifact (its `Content` is dead
   weight since the lean handler ignores it) — drop to `Page` at the next API review?
9. Flyout API naming/shape (`OpenFlyoutAsync`/`ScaffoldFlyoutSide`, width/scrim styling,
   explicit "none" sentinel to suppress an inherited flyout) — design review.
10. Should `ScaffoldViewController` forward appearing/disappearing to the scaffold page's MAUI
    events (parity with PageHandler-hosted pages)?

---

## 15. Field notes — hard-won facts from the implementation

Platform and framework behaviors discovered while building P0/P1; they shaped decisions above and
will bite again if forgotten:

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
- **.NET 10 safe-area opt-in (§5.4 corollary, verified on the tab bar)**: content is
  edge-to-edge by default; a page's scrollable only gains the augmented bottom inset (system +
  chrome footprint) when it opts in — `scrollView.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container)`.
  Without it the content end hides behind the bar. The augmentation channel itself
  (iOS `AdditionalSafeAreaInsets` on the content-host controller, Android system-bars rewrite in
  `ScaffoldPageLayerLayout`) is verified working on both platforms.
- **MAUI `AutomationId` can only be set once** — reusing a templated view with a different id
  needs the id decided at construction time (a later set throws, surfacing as a DevFlow
  "tap failed" when it happens inside a tap handler).
- **iOS simulator deploys over a RUNNING app keep the stale bundle** (trimmer `linked/` cache +
  install skip, no error): always kill → build → `-t:Run` (see the maui-devflow-uitests skill).
