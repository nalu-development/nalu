# Scaffold System Bars

The Scaffold keeps the **status-bar icon style** (and Android's gesture-navigation bar) in
contrast with whatever is actually visible behind it — automatically, on both platforms, with
live updates for theme changes, scroll-driven chrome, flyouts and navigation.

## The default: `Auto`

You usually configure nothing. `Auto` resolves the icon style from the visible surface stack:

1. **An open flyout** — icons contrast with the drawer surface.
2. **The nav bar**, when visible and opaque enough — by its actual luminance. This is live:
   with a [scroll-materializing bar](scaffold-navbar.md#scroll-driven-chrome), the icons flip
   exactly when the bar becomes opaque.
3. **Your declaration** (see below).
4. **A pixel sample of the real rendered content** under the status bar — the ground truth
   that handles photos, gradients and scrims no rule could know. Samples are taken when
   presentation settles (never mid-transition) and on visual changes, debounced; the cost is
   negligible (a tiny scaled copy).
5. The page's own background color (or its top-spanning first child's).
6. The app theme.

The same resolution also **fixes two Android staleness bugs** that bite any
`ConfigChanges.UiMode` app (that is: every standard MAUI app) on system theme toggles without
activity recreation: stale status-bar icon appearance, and a stale bottom
`navigationBarColor` (visible with 3-button navigation) — both re-apply on theme change.

## Declaring intent

When the pixels shouldn't decide — e.g. a photo header whose sky is bright but whose chrome
(white chevron, white title) wants light icons — declare the style over *your content*:

```xml
<ContentPage nalu:Scaffold.SystemBarStyle="LightContent">   <!-- Auto | LightContent | DarkContent -->
```

Resolution is page → area → scaffold. Two things to know:

- The declaration describes the **page's own surface**. An opaque chrome layer covering the
  status-bar region (a materialized nav bar, an open flyout) still wins with its own
  brightness — the icons never end up illegible against chrome.
- `LightContent` = white icons (dark surfaces); `DarkContent` = black icons (light surfaces).

## Platform notes

- **iOS**: applied through the view-controller chain (`preferredStatusBarStyle`); UIKit
  cross-fades style changes. No `Info.plist` changes needed.
- **Android**: applied via `WindowInsetsControllerCompat` for both the status bar and the
  gesture-navigation bar; SystemUI fades the flips. The pixel sampler uses `PixelCopy`
  (API 26+; older devices fall back to the semantic rules).
