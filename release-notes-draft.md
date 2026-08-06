# Highlights

- 🧨 **Fixed a hard crash** in `VirtualScroll` on iOS when data sources emit multi-change change sets (e.g. DynamicData `Bind` with `Reset` escalation) while a page is being pushed.
- 🧭 **Navigation: fixed two crashes** — returning to a tab whose preserved stack had pushed pages ("Unable to find page instance for specified route"), and re-creating a `NaluShell` after disposing the previous one ("Duplicated Route", e.g. logout/login flows).
- 🎯 **`ScrollTo` overhaul on iOS**: precise section-header targeting, exact `MakeVisible` minimal-scroll semantics, and cancellation of superseded scroll commands.
- 🆕 **`ScrollTo` now supports the global header/footer** on iOS and Android via `VirtualScrollRange.GlobalHeaderSectionIndex` / `GlobalFooterSectionIndex`.
- 🔄 **Pull-to-refresh actually shows the spinner** when `IsRefreshing` is set programmatically on iOS.
- 🤖 **Android cell measurement fixed**: no more clipped content inside `VirtualScroll` cells.
- ✅ New DevFlow-driven UI test suite (135 tests) verified on iOS Simulator and Android emulator.

# Nalu.Maui.VirtualScroll

## Fixed

- **iOS — crash `NSInternalInconsistencyException` ("invalid number of sections") with multi-change change sets.** A single change set carrying multiple mutations — e.g. DynamicData `Bind` escalating a >25-change changeset to `Reset` while sections were added in the same main-loop pass, typically during a page push — was applied as separate `UICollectionView` transactions against a data source already holding the final counts. Change sets are now applied atomically in a single `PerformBatchUpdates`, including the Reset path's delete/reload/insert sequence.
- **iOS — `ScrollTo` to a section header landed far from the target** with self-sizing cells (estimate-based supplementary frames). Section headers now use a two-phase scroll with iterative offset correction, and honor `ScrollToPosition` (`Start`/`Center`/`End`/`MakeVisible`) — previously the position was ignored for header targets.
- **iOS — `MakeVisible` over/undershot far-away targets** and could leave them just outside the viewport. Minimal scroll is now computed against the offset the gesture started from, converging on the exact edge, and never moves an already-visible target. Item-targeted scrolls also gained a post-settle refinement pass against estimated sizes.
- **iOS — superseded `ScrollTo` commands could re-assert their old target**: the asynchronous correction chains are now cancelled whenever a newer scroll command is issued.
- **iOS — programmatic `IsRefreshing = true` showed no spinner** (`UIRefreshControl.BeginRefreshing` renders nothing on its own). The control is now revealed by scrolling the content — only when the list rests at the top — and the pre-reveal offset is restored when the refresh completes.
- **iOS — a pull-to-refresh gesture wrote `IsRefreshEnabled` instead of `IsRefreshing`** when syncing platform state back to the virtual view.
- **iOS — fading edge rendered with zero length at the exact start/end of the content** (leading/trailing fade lengths were swapped in the edge branches); mid-scroll fades were unaffected.
- **Android — cell content could be clipped** (e.g. buttons rendered at half height): cells were measured natively instead of through MAUI's cross-platform measure, ignoring margins, `HeightRequest` and cross-platform layout logic.
- **Android — a non-animated `ScrollTo` issued during an in-flight smooth scroll was overridden** by the ongoing animation; jump commands now cancel it.

## Added

- **`ScrollTo` global header/footer targets** on iOS and Android:

  ```csharp
  virtualScroll.ScrollTo(VirtualScrollRange.GlobalHeaderSectionIndex, 0, ScrollToPosition.Start);
  virtualScroll.ScrollTo(VirtualScrollRange.GlobalFooterSectionIndex, 0, ScrollToPosition.End);
  ```

  All `ScrollToPosition` values are honored (clamped at the content extremes).

# Nalu.Maui.Navigation

## Fixed

- **Crash returning to a tab with a preserved navigation stack** (`InvalidOperationException: Unable to find page instance for specified route`). Navigating to the root of a section whose preserved stack had pushed pages — e.g. `Navigation.Absolute().Root<HomePage>()` while the Home tab held a pushed detail page, or simply switching back via the tab bar — committed a shell route that still referenced the just-popped page, making MAUI ask the route factory to re-create it.
- **`ArgumentException: Duplicated Route` after re-creating a `NaluShell`.** `Routing.RegisterRoute` is MAUI-global: a new shell instance (e.g. after a logout/login flow) re-registered the same segments with different factories. Disposing a `NaluShell` now unregisters the routes it registered.
- **Replacing a page with another instance of a page type already on the stack threw** (`Relative().Pop().Push<DetailPageModel>()` with a `DetailPage` already below): MAUI's stack rebuilding mis-handles duplicate adjacent route names, popping the just-created page and re-requesting its route. The route factory now re-vends the last created page when it is not attached to a stack, and same-content navigations are committed as relative routes so MAUI no longer re-processes the whole stack.
- **Returning to a tab with a preserved stack sent `IAppearingAware` to the wrong page**: the section's root content page received the event instead of the page actually appearing (the top of the preserved stack).
- **Discarding a whole `NaluShell` leaked all live pages.** Disposing the shell now tears down every live page (navigation context, DI scope, page-model disposal, handler disconnection) and detaches from the singleton navigation service — previously a logout/login flow swapping the window page left every page model undisposed and the shell graph rooted by the navigation service.

## Changed

- **`NaluTabBar` tab switches now restore the target tab's preserved navigation stack** (matching native tab-bar semantics) instead of navigating to the tab's root page. Tapping the tab of the current section still navigates back to its root.

# Nalu.Maui.Layouts

## Fixed

- **`HorizontalWrapLayout` / `VerticalWrapLayout` — rows could wrap differently between measure and arrange**, producing a phantom line clipped outside the measured bounds. Native pixel-grid alignment can hand the arrange pass a sub-point smaller size than the measured constraint (notably inside `VirtualScroll` cells); a half-point tolerance applied symmetrically in both passes absorbs it.

# Nalu.Maui.Core

## Fixed / improved (iOS background HTTP)

- **Errors no longer surface as a bare `unknown error`**: failures now include the `NSError` domain, code, failing URL and the underlying error chain.
- **All requests now run as background download tasks**: multipart/stream bodies are spooled to a file and attached memory-mapped, so every response flows through the fully-handled download-completion path (upload-task responses were not delivered by the delegate).
- Improved `CancellationToken` management and richer debug logging in the background session handler.

# Internal / testing

- Legacy Appium-based UI tests and the `VisualTestUtils` / Magick.NET utilities have been removed, replaced by a DevFlow-driven UI test suite (135 tests) running against `Nalu.Maui.TestApp`, verified on iOS Simulator and Android emulator (the navigation suite is currently verified on iOS; Android verification is planned). Coverage includes `VirtualScroll` rendering and virtualization, observable/DynamicData mutations (including mutation-during-push/pop crash regressions and randomized mutation-while-scrolling stress storms), the full `ScrollTo` matrix, pull-to-refresh with real-spinner verification, wrap-layout rounding regressions, and an iOS-only manual page for exercising the background HTTP handler on a real device.
- New navigation UI test suite (19 tests) driving a dedicated `NaluShell` harness through the full lifecycle matrix: push/pop/replace ordering (`IEnteringAware`/`IAppearingAware`/`IDisappearingAware`/`ILeavingAware`/dispose), typed and awaitable intents, `ILeavingGuard` block/allow/`IgnoreGuards` (relative and absolute), tab-section switches preserving stacks (including real `NaluTabBar` taps), cross-item navigation clearing stacks, `Root().Add()` deep links, and native back-button behavior.
- Every navigation scenario now also asserts **no page/page-model leaks**: a weak-reference tracker in the TestApp forces GC after each test and verifies that everything Nalu disposed during navigation is actually collected. This also documented two MAUI 10 iOS platform limitations (a discarded vanilla `Shell` is retained after `Window.Page` swap, and pages popped by a single multi-page pop commit stay rooted by renderer trackers), both isolated with control pages and pinned by exact assertions.

---

A heartfelt thank you to [Anthropic](https://www.anthropic.com) for granting free access to [Claude for open-source development](https://claude.com/contact-sales/claude-for-oss) — much of the testing and bug-hunting work in this release was done with its help. 💜
