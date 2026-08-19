# Plan: per-page nav bar as the ONLY nav bar model

Audience: an agent working in this repository (`Nalu.Maui.Scaffold`). Baseline: `main` at
`f10cb48` (OverlayEvent). Branch `feature/scaffold-navbar-swap-choreography` is **obsolete** with
this plan (its ghost choreography only makes sense for a shared strip) — do not merge it.

## 0. Decision

The nav bar becomes part of each page's presentation: library-owned chrome (same
`ScaffoldNavBarHost`, appearance chain, scroll tracker, default `ScaffoldNavBarView` / custom
`NavBarView`), mounted **inside each page's host container** and therefore moving with the page in
every motion the scaffold performs (push/pop slides, custom `ScaffoldPageTransition` specs, shared
elements, interactive pop, Android predictive back, modal presentation). No "shared strip" mode
survives: the single controller-level strip, the virtual bar swap, `InvalidateNavBarMeasure`, the
stale-height slide targets and all related code are deleted, not kept behind a switch.

Non-goals: the tab bar (shared bottom chrome) is unchanged; overlays (popups, sheets, panels,
flyouts) are unchanged; `OverlayEvent`/`NavigationEvent` unchanged; no new public styling API.

## 1. Invariants (do not bend these)

- Layout invalidation discipline: invalidation only marks dirty + requests layout; measure/arrange
  happen in the platform pass (`ViewDidLayoutSubviews` / `LayoutSubviews` on iOS, `OnMeasure` /
  `OnLayout` on Android). Never rely on the Controls `MeasureInvalidated` event. No dispatch
  queues / timers for layout or settles (`ScaffoldViewFrames.NextLayoutAsync` one-shot layout
  listeners are fine on Android). See `ScaffoldChromeStrip` (iOS) and `ScaffoldNavBarStripLayout`
  (Android) — reuse them, re-parent them.
- A page's top inset comes from ITS OWN bar, immediately and finally, before the page is staged;
  page insets are never animated during a navigation (they may animate on a same-page visibility
  toggle, as today).
- Chrome animations honor `ScaffoldChromeMotion.DurationScale` (internal slow-motion knob used by
  UI tests) — reuse it for the per-page visibility slide.
- `Scaffold` derives from `Page` and implements `IPageContainer<Page>` (`ScaffoldPageTreeTests`
  guards it). Do not change the base type.
- Nullable fields assigned late must be declared nullable; idempotent setters in layout passes.

## 2. Target architecture

### 2.1 Cross-platform model (Source/Nalu.Maui.Scaffold, shared code) — Option A

**`ScaffoldPageHost : Element`** (new, `Internals/`, **internal**): the logical "screen" = one page
+ its chrome.
- Logical children: the page's `ScaffoldNavBarHost` (when the page resolves a bar and it is
  visible — see 2.3) and the `Page`. The host is a logical child of the `Scaffold`. This REPLACES
  today's direct `scaffold.AddLogicalChild(page)` (`ScaffoldNavigationStack` / `FindHostPage()`
  callers): `page.Parent` becomes the `ScaffoldPageHost`, whose `Parent` is the `Scaffold`.
- Owns: `Page`, `ScaffoldNavBarContext Context` (per page — 2.2), `ScaffoldNavBarHost? NavBarHost`,
  the resolved bar view, the `IsNavBarVisible` / `NavBarOverlapsContent` evaluation for its page,
  and the observation of the page's `NavBarView` / `IsNavBarVisible` / `NavBarOverlapsContent` /
  `NavBarAppearance` / `ScrollTracker` / `Title` / `TitleView` / `PageMode` property changes (today
  spread between `ScaffoldNavBarContext.Update`, `ScaffoldNavBarHost.UpdateSources` and the
  presenters' `OnCurrentPagePropertyChanged`).
- Lifetime: created when the page enters a navigation stack (`ScaffoldNavigationStack.Push` /
  `RootPage` set), disposed when it leaves (`Pop` / root destroyed). Stack pages that are not
  current keep their host alive (their bar keeps its state, like the page does).
- Non-visual and minimal: an `Element`, never a `View`; no handler, no platform view, no layout
  participation. Public surface of the internal type: `Page`, `Context` (public getters, needed by
  the `FindAncestor` binding path), `Scaffold`.
- `ScaffoldPageHost.Find(Element)`: walks `Parent` to the nearest host (bindings,
  `Scaffold.FindNavBarContext`).

**Reversibility seams (mandatory, and DOCUMENTED IN THE `ScaffoldPageHost` SOURCE as "Fallback
strategy"):** the parenting decision must be confined so that switching to the fallback (below)
touches exactly these places and nothing else:
1. `Scaffold.AttachPage(page)` / `DetachPage(page)` — the ONLY code that builds/tears the logical
   shape (today: `AddLogicalChild(host)` + `host.AddLogicalChild(page)`; fallback:
   `AddLogicalChild(page)` + host kept in a dictionary). `ScaffoldNavigationStack` calls these.
2. `NavBarBindings.ContextSource` (one internal factory) used by `NavBarBindingExtension`,
   `NavBarBindings.Create`, `ScrollValueExtension`, `ThemeScrollValueExtension` and
   `Scaffold.FindNavBarContext` — today: `RelativeBindingSource.FindAncestor(typeof(ScaffoldPageHost))`
   + path `Context.<X>` (plain `Binding`; annotate for trimming:
   `[DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(ScaffoldPageHost))]`
   and make sure `ScaffoldNavBarContext`'s public properties are preserved).
3. No code anywhere assumes `page.Parent is Scaffold` nor `page.Parent is ScaffoldPageHost`:
   use `GetScaffold()` (ancestor walk) and `scaffold.GetPageHost(page)`. Grep and fix:
   `Parent is Scaffold`, `as Scaffold`, `FindHostPage`, `RealParent`, `((Page)…).Parent`.
   A unit test pins the invariants that must hold in either shape: `page.GetScaffold()`,
   `Scaffold.FindNavBarContext(pageContent)`/`(barContent)`, `page.Window` resolution
   (`Element.Window` through the chain), app/scaffold resources and implicit styles reaching page
   content, `NavigationEvent`/`OverlayEvent` unaffected, `IPageContainer<Page>.CurrentPage` = the page.

**Fallback strategy (Option B — implement ONLY if `Page.Parent` being a non-Page `Element` proves
unworkable in the spike or later):** pages become direct logical children of the `Scaffold` again
and the per-page `ScaffoldPageHost` objects live in a `Scaffold`-owned dictionary (same class, no
longer an `Element` in the tree); the `ScaffoldNavBarHost` becomes a logical child of the PAGE
(`page.AddLogicalChild(host)` — with `VerticalOptions = Start` on the host so a `TemplatedPage`
does not make it `Fixed`, which would stop the native measure-invalidation walk at the host —
and accepting that page-level resources/implicit styles then reach the bar); the context is
exposed through a hidden attached bindable property on the page (`Scaffold.NavBarContextProperty`,
attached, read-only); `NavBarBindings.ContextSource` produces a `TypedBinding<Page, T>` with
`RelativeBindingSource.FindAncestor(typeof(Page))`, getter
`page => ((ScaffoldNavBarContext?)page.GetValue(NavBarContextProperty))?.X` and change handlers
`[(p => p, "NavBarContext"), (p => ctx(p), "X")]`, from a fixed map of the context's public
properties (deeper string paths unsupported in that mode). MAUI binding paths cannot address
attached properties and a "relay" that follows the target's parents cannot see ancestors attached
later — those are the reasons the fallback needs the TypedBinding map. Write this paragraph
(condensed) as a `<remarks>` block on `ScaffoldPageHost`.

**Spike (step 1 of the schedule, ½ day):** `ScaffoldPageHost` as the page's logical parent in the
TestApp + DailyHelper on both platforms: `DisplayAlert`/`DisplayActionSheet` from a hosted page,
`Element.Window`, `GetScaffold()`, resources/styles, XAML hot reload of a hosted page, DevFlow
visual tree (an extra `ScaffoldPageHost` node, AutomationIds unaffected), MAUI `Page` internals
that read `RealParent as Page` (grep MAUI source: `Page.OnParentSet`, `NavigationProxy`,
`Page.SendAppearing`), and the unit test from seam 3. Go/no-go on A vs the fallback.

### 2.2 `ScaffoldNavBarContext` per page

- One context per `ScaffoldPageHost`, updated from its own page + root (the current
  `Update(root, page)` logic minus the "observed page" switching — the page never changes).
  `CanNavigateBack`, `IsModal`, `IsCloseButtonVisible`, flyout button visibility still depend on
  stack/root state: the presenters call `Refresh()` for the incoming page on every
  synchronization; the outgoing page's context keeps its last state (it is leaving).
- `Scaffold.NavBarContext` (public) keeps its signature and meaning "what the bar shows now": a
  forwarder to the CURRENT page's context (raise `PropertyChanged` for every property when the
  current page changes). Document that a bar's `BindingContext` — and every `{nalu:NavBarBinding}`
  inside a page or its bar — is that page's own context.
- `Scaffold.FindNavBarContext(Element)` → nearest `ScaffoldPageHost.Context`, fallback
  `Scaffold.NavBarContext`.
- `{nalu:NavBarBinding}` / `NavBarBindings.Create` / `ScrollValueExtension` /
  `ThemeScrollValueExtension`: resolve through `NavBarBindings.ContextSource` (seam 2) — page
  content and bar content both get their own page's context, including during a transition when
  two pages are alive (today the outgoing page's parallax reads the incoming page's offset; that
  bug disappears). `NavBarBindings.ScaffoldAncestor` stays for compatibility (it still points at
  the scaffold; it is no longer what `Create` uses) — mark `[Obsolete]` with a message.
- `ScaffoldNavBarAppearance.SetContext`: the host stamps ITS page's context.
- `ScaffoldSystemBars.UpdateBar(page, background, opacity)`: called by each host; the system bars
  apply only the CURRENT page host's values (ignore others; re-apply on current-page change).

### 2.3 `ScaffoldNavBarHost` (per page)

- One per `ScaffoldPageHost` whose page resolves a bar view (`ResolveNavBarView(page)` unchanged:
  page → area → scaffold → default `ScaffoldNavBarView`; the DEFAULT bar view is instantiated PER
  HOST — never shared across pages, two can be on screen at once).
- Logical child of its `ScaffoldPageHost` while the page's bar is visible; detached while hidden
  (keeps today's "the element tree reflects presented chrome" rule and the UI tests'
  `WaitForElementGoneAsync("NavBarTitleLabel")` expectations).
- Remove: `UpdateSources(Page?)` page switching (fixed page) and the scaffold-level `CurrentArea`
  re-targeting (the area is fixed per page; only area attachment PropertyChanged still matters).
  Keep: `SetBar` (hot reload / `NavBarView` change on the same page), the appearance chain
  (page → area → scaffold), scroll tracking, `EnsureRooted`, native-invalidation-only measure.
- Constraints: the host's parent is an `Element` (`ScaffoldPageHost`), which never assigns a
  `LayoutConstraint` — the host stays `None`, nothing Fixed sits between the bar and the strip, so
  the native walk reaches the strip. Remove the `ScaffoldNavBarHost => HorizontallyFixed` arm from
  `Scaffold.ComputeConstraintForView` (the host is no longer the scaffold's child); the overlay
  `None` rule stays.

### 2.4 iOS (`Platforms/iOS`)

- New `ScaffoldPageHostController : UIViewController` per `ScaffoldPageHost` (owned by the presenter, keyed by page): view =
  `[ScaffoldNavBarStrip (top, full width, height = bar's measured height)] + [page VC view (fills)]`.
  Child VC = the MAUI page's `UIViewController` (`page.ToUIViewController(mauiContext)`). The
  container, not the page view, is what `TransitionToPageAsync`, `ResetMotion`, `ScaffoldPageDepth`,
  shared-element flights (source/destination capture stays on content views), the interactive edge
  pop (`EnsureEdgeGesture`/peek mount) and modal choreographies mount and animate. Audit every
  `newView`/`previousController.View`/`topView` usage in `ScaffoldPresenter.ios.cs` and point it at
  the container.
- Measure/insets (in the container's `ViewDidLayoutSubviews`, same discipline as today's
  `ScaffoldViewController.ViewDidLayoutSubviewsCore` for the nav strip): if the strip is dirty or
  bounds/insets changed → `strip.Measure(width)`, position the strip at the top, set the page VC's
  `AdditionalSafeAreaInsets.Top` = `FootprintAboveInset(bar, systemTop)` when the page wants the
  inset (`IsNavBarVisible && !NavBarOverlapsContent`), else 0. Keyboard/bottom contributions stay
  where they are (`ScaffoldViewController.ApplyCurrentPageInsets` keeps bottom/keyboard; move the
  top part into the container, or have the container feed `NavBarInsetContribution` per page —
  pick one place, the container is the owner of the top inset).
- The nav strip keeps `ScaffoldChromeStrip` semantics (`IPlatformMeasureInvalidationController`,
  not a backing; the host→surface→bar chain contains a MAUI backing so the safe-area re-fold
  climbs natively — see `ScaffoldChromeStrip` remarks). `NavStripHeight`, `PositionNavStrip`,
  `MountNavBar/UnmountNavBar/InvalidateNavBarMeasure/SetNavBarPresentedAsync/BeginAnimatedNavBarSwap`
  on `ScaffoldViewController` are deleted; a per-container `SetNavBarPresentedAsync(bool, animated)`
  handles same-page visibility toggles (slide the strip by its own height, animate the page's
  top inset along — today's behavior for `IsNavBarVisible` toggles), with lazy target and the
  `prepare` step as today.
- Z-order/scrims: overlays above all pages; the tab bar strip above pages; a "behind-chrome"
  scrim (`ScaffoldOverlayRequest` behind-chrome kinds) must dim the nav bars too — they are inside
  the page layer now, so they are dimmed by construction. Check `chromeLayer` / z-slots code.
- System bars sampling (`ScaffoldSystemBars`, `OnPresentationSettled`) reads the current page's
  bar region — unchanged semantics, verify the sampling rect.
- `CurrentPageController`, `CurrentPageWantsNavBarInset`, `_navBarStrip`, `_navBarPresented`,
  `_navBarAnimating`, `ChromeTopFootprint`-like members on `ScaffoldViewController`: remove or
  re-home per container. `WindowGeometryChanged` keeps firing from the root controller.
- Presenter `UpdateNavBarChromeAsync` (iOS) is deleted; `SynchronizeAsync` asks the incoming
  page's `ScaffoldPageHost` to `Refresh()` its context/appearance before mounting, and the
  container lays out bar + insets in its own pass (flush: `container.View.LayoutIfNeeded()` before
  the transition animation, like today's pre-mount flush, so strip geometry and page insets are
  final before the slide).

### 2.5 Android (`Platforms/Android`)

- New `ScaffoldPageFrame : FrameLayout` per `ScaffoldPageHost` (keyed by page; or extend `ScaffoldPageFragment`'s root view):
  `[ScaffoldNavBarStripLayout (top)] + [page platform view]`. The fragment/presenter animators
  (translationX slides, `ScaffoldPageDepth`, predictive-back peek, shared elements) target the frame.
- Insets: the frame implements the top rewrite today done by `ScaffoldPageLayerLayout` /
  `ScaffoldLayout.PageTopInsetPx`/`ChromeTopDesired` — per frame: in `OnLayout`, once its strip
  has a height, `PageTopInsetPx = wantsInset ? strip.Height : 0` and `RequestApplyInsets` on the
  page view. Bottom/IME rewrites stay in `ScaffoldPageLayerLayout`. The predictive-back peek's
  "own chrome intent" special case (see `ScaffoldPageLayerLayout` remarks) disappears: every frame
  carries its own intent.
- Visibility toggle: per-frame `AnimateNavStripToAsync` (translationY ± strip height, insets
  frozen during flight, `NextLayoutAsync` after a bar view change before choosing targets — reuse
  the current code, re-homed). Delete `_navBarStrip`, `_navBarHost`, `_lastNavStripHeight`,
  `UpdateNavBarChromeAsync`, `DetachNavBarHost`, `ScaffoldLayout.NavBarLayer`.
- Back callback / system bars / keyboard: unchanged; check `ScaffoldBackCallback` does not
  reference the nav strip.

### 2.6 Presenters: what `SynchronizeAsync` does after the change

1. `scaffold.GetPageHost(targetPage)` → `Refresh()` (context + bar resolution + appearance).
2. Mount the incoming page's container (iOS) / frame (Android) — strip already inside, dirty.
3. Flush layout (container) so bar geometry + top inset are final; stage the page; run the page
   transition on the container; tab bar chrome as today.
4. Outgoing page keeps its container/bar untouched while leaving; its host stays alive while it
   stays in a stack.

## 3. Public API changes

- No new PUBLIC types (`ScaffoldPageHost` is internal). `Scaffold.NavBarContext` keeps its
  signature (now the current page's context). `Scaffold.FindNavBarContext(Element)`: the element's
  own page's context (fallback: the current one). `{nalu:NavBarBinding}` / `NavBarBindings.Create`
  keep their signatures; they now resolve the element's own page's context.
  `NavBarBindings.ScaffoldAncestor` obsolete (still works, points at the scaffold).
- Observable behavior changes: the nav bar travels with its page in every transition; a page's
  `Parent` is no longer the `Scaffold` (an internal host element sits in between — `GetScaffold()`
  and ancestor walks are unaffected); the DEFAULT bar is one instance per page.

## 4. Deletions checklist (grep each, delete or re-home)

iOS `ScaffoldViewController.cs`: `ScaffoldNavBarStrip` (keep the type, re-parent), `_navBarStrip`,
`_navBarPresented`, `_navBarAnimating`, `MountNavBar`, `UnmountNavBar`, `InvalidateNavBarMeasure`,
`SetNavBarPresentedAsync`, `NavStripHeight`, `PositionNavStrip`, `NavBarInsetContribution`,
`CurrentPageWantsNavBarInset`, nav parts of `ViewDidLayoutSubviewsCore`/`ApplyCurrentPageInsets`.
iOS `ScaffoldPresenter.cs`: `UpdateNavBarChromeAsync`, `_navBarHost`, `DetachNavBarHost`,
`OnCurrentPagePropertyChanged` nav cases, `EnsureScaffoldObserver`/`ObserveNavBarArea` nav parts,
the pre-mount `controller.View.LayoutIfNeeded()` (re-homed to the container flush).
Android `ScaffoldPresenter.cs`: `UpdateNavBarChromeAsync`, `_navBarStrip`, `_navBarHost`,
`_lastNavStripHeight`, `AnimateNavStripToAsync` (re-home), `DetachNavBarHost`; `ScaffoldLayout`:
`NavBarLayer`, `ChromeTopDesired`, `PageTopInsetPx` (re-home to the frame).
Shared: `ScaffoldNavBarHost.UpdateSources`, `ScaffoldNavBarContext.Update`'s page switching,
`Scaffold.ComputeConstraintForView` nav bar arm.

## 5. Tests

Unit (`Tests/Nalu.Maui.Test/ScaffoldTests`): page host lifecycle (created on push, disposed on
pop/root destroy, one per page; nav bar host attached/detached with bar visibility), the tree
shape (`page.Parent` is the host, host's parent is the scaffold, `page.GetScaffold()` works),
per-page context values (Title, CanNavigateBack, IsModal) for two live pages,
`Scaffold.NavBarContext` forwards the current page's values and raises on page change,
`FindNavBarContext` and `NavBarBindings.Create` resolve the page's own context from page content
AND from bar content (two live pages → two different contexts), appearance chain per host,
system bars follow the current page only, the seam-3 invariants test.

UI (DevFlow, `UITests/UITests.DevFlow`): existing suites must stay green on iOS and Android —
`ScaffoldNavBar*`, `ScaffoldNavBarAppearanceTests`, `ScaffoldGrowingTabBarChromeTests`
(nav band part), `ScaffoldCustomTabBar*`, `ScaffoldOrientationChromeTests`, `ScaffoldSystemBarTests`,
`ScaffoldTransitionChromeTests`, `ScaffoldPageTransitionChromeTests`, `ScaffoldMotionChromeTests`,
`ScaffoldModal*`, `ScaffoldFlyoutChromeTests`, `ScaffoldKeyboard*`, `ScaffoldRestoreChromeTests`,
`ScaffoldSlowAppearingChromeTests`, `ScaffoldHotReloadChromeTests`. Expected rewrites: anything
asserting the bar stays put during a navigation, and `BarSwapIsChoreographedBothWays`
(replace with "the bar travels with its page": with `SlowChromeNavBar`/transition slow-motion,
mid-slide the incoming page's bar is horizontally offset together with its page and the outgoing
bar leaves with the outgoing page). New tests: (a) bar moves with the page on push and pop
(bounds of `NavBarTitle` sampled mid-slide track the page's X), (b) two pages' bars show their
OWN titles during a transition, (c) `{nalu:NavBarBinding ScrollOffset}` in page content reads its
own page's offset while another page is presented, (d) visibility toggle on the same page still
slides and re-insets, (e) custom/edge-to-edge bars per page: insets per page immediately, no
peek/pop after a pop (`HiddenBarStaysHiddenAcrossABarSwapRoundTrip` stays meaningful).

Samples: DailyHelper, TestApp, template app (`dotnet new maui-nalu-scaffold`) build and behave;
visual check of DailyHelper Today → Weather → back on iOS and Android.

## 6. Docs / template

- `conceptual_docs/scaffold-navbar.md`: the bar is part of the page's presentation (moves with it);
  per-page context; `{nalu:NavBarBinding}` resolves the page's own context; the
  "strip"/"swap" wording goes away. `scaffold-transitions.md`: note the bar travels.
- Template skills (`nalu-scaffold-structure`, `nalu-scaffold-scroll`): the bar moves with the
  page; bindings resolve the page's own context; do not rely on `page.Parent is Scaffold`.
- Release notes: behavior change (nav bar animates with the page); `{nalu:NavBarBinding}` now
  resolves the page's own context; `Scaffold.NavBarContext` = current page's context; a page's
  `Parent` is an internal host element (use `GetScaffold()`); `NavBarBindings.ScaffoldAncestor`
  obsolete.

## 7. Schedule / PR slicing

1. Spike (½ day): `ScaffoldPageHost` as the page's logical parent — checks listed in 2.1.
   Go/no-go on A; if no-go, switch to the documented fallback (same class, same seams).
2. Shared model: `ScaffoldPageHost`, `Scaffold.AttachPage/DetachPage`, per-page
   `ScaffoldNavBarContext`, per-page `ScaffoldNavBarHost`, `NavBarBindings.ContextSource`,
   `Scaffold.NavBarContext` forwarder, `FindNavBarContext`, the seam-3 invariants test, the
   fallback `<remarks>` on `ScaffoldPageHost`. Presenters temporarily adapted to keep building
   (still one strip, fed by the current page's host) — green suites, mergeable.
3. iOS per-page container + deletions; iOS suites green.
4. Android per-page frame + deletions; Android suites green.
5. Docs, skills, release notes; delete the choreography branch.

Keep each step green on both platforms before moving on; do not push without Alberto's OK.
