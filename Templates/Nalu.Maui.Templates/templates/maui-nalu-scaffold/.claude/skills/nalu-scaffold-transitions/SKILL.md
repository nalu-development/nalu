---
name: nalu-scaffold-transitions
description: Nalu.Maui.Scaffold page transitions, shared elements (TransitionName), modal pages (PageMode), iOS/Android back gestures and Shell/NavigationPage migration; load when styling how pages enter/leave.
---
# Scaffold transitions, shared elements & modals

Package `Nalu.Maui.Scaffold`, namespace `Nalu`, XAML `xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"`.
Every push/pop in a `nalu:Scaffold` is animated by Nalu (not the platform): a declarative
`ScaffoldPageTransition` record decides how the PUSHED page enters and what the covered page does;
the pop replays it in reverse. Views tagged with the same `Scaffold.TransitionName` on both pages
fly between their geometries. Modals are ordinary stack pages with a different presentation
(`Scaffold.PageMode`). All of these are attached properties on `Nalu.Scaffold`.

## Quick reference

| API | Purpose | Notes |
|-----|---------|-------|
| `nalu:Scaffold.PageTransition` (attached, `ScaffoldPageTransition?`) | Push/pop spec | On a page: that page's own spec. On the `Scaffold`: default for all pages. Resolution: page value → `SlideFromBottom` if page is modal → scaffold value → `Default`. |
| `ScaffoldPageTransition.Default` | Slide in from right, behind page static | Fallback. |
| `ScaffoldPageTransition.SlideFromRight` | iOS-style slide, behind page parallaxes (-0.3 X, opacity 0.9) | Template sets this scaffold-wide in `AppScaffold.xaml`. |
| `ScaffoldPageTransition.SlideUpFade` | 3% slide-up + fade in; behind scales 0.97 and dims | 0.38 s. |
| `ScaffoldPageTransition.ZoomFade` | Scale 0.85→1 + fade; behind grows 1.05 and dims | 0.3 s. |
| `ScaffoldPageTransition.SlideFromBottom` | Slide up from bottom edge; behind recedes | 0.3 s. Automatic default for modal pages. |
| `ScaffoldPageTransition.None` | Instant swap | Duration 0. |
| `new ScaffoldPageTransition(Enter, Behind, DurationSeconds = 0.25)` | Custom spec (record) | See reference.md. |
| `ScaffoldTransitionMotion(FractionX, FractionY, Scale, Opacity)` | One page's motion | Fractions of page size; `Scale`/`Opacity` default 1. |
| `nalu:Scaffold.TransitionName` (attached, `string`) | Shared element tag | Same name on a view in BOTH pages of the push/pop. |
| `nalu:Scaffold.PageMode` (attached, `ScaffoldPageMode`) | `Default` / `Modal` / `DismissableModal` | Set on the pushed page. |
| `ScaffoldNavBarContext.IsModal` / `.IsCloseButtonVisible` | Nav-bar state for custom bars | Read via `{nalu:NavBarBinding}` → skill `nalu-scaffold-structure`. |

## Patterns

Scaffold-wide default (template's `AppScaffold.xaml`):

```xml
<nalu:Scaffold xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"
               nalu:Scaffold.PageTransition="{x:Static nalu:ScaffoldPageTransition.SlideFromRight}">
```

Per-page override — the spec belongs to the page being PUSHED (it enters and leaves with it):

```xml
<ContentPage xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"
             nalu:Scaffold.PageTransition="{x:Static nalu:ScaffoldPageTransition.SlideUpFade}">
```

Shared element (template `HomePage.xaml` ↔ `DetailPage.xaml`): same name, any size/position.

```xml
<!-- HomePage -->
<Image nalu:Scaffold.TransitionName="nalu-logo" Source="nalu_logo.png" HeightRequest="96" WidthRequest="96" />
<!-- DetailPage -->
<Image nalu:Scaffold.TransitionName="nalu-logo" Source="nalu_logo.png" HeightRequest="160" WidthRequest="160" />
```

Modal page — pushed/popped with the normal navigation API (skill `nalu-navigation`):

```xml
<ContentPage nalu:Scaffold.PageMode="DismissableModal" Title="Filters">
```

```csharp
await _navigation.GoToAsync(Nav.Push<FiltersPageModel>()); // present (Nav = Nalu.Navigation, see GlobalUsings.cs)
await _navigation.GoToAsync(Nav.Pop());                    // dismiss
```

Custom spec in C#, referenced from XAML with `{x:Static local:Transitions.Reveal}`:

```csharp
public static class Transitions
{
    public static readonly ScaffoldPageTransition Reveal = new(
        Enter: new ScaffoldTransitionMotion(FractionY: 0.05, Opacity: 0),
        Behind: new ScaffoldTransitionMotion(Scale: 0.97, Opacity: 0.9),
        DurationSeconds: 0.3);
}
```

## Rules & gotchas

- The spec is resolved from the PUSHED page only. Setting `PageTransition` on page A never affects
  how B enters on top of A; set it on B (or scaffold-wide).
- Interactive gestures (iOS leading-edge swipe, Android predictive back) do NOT replay custom
  specs: they scrub the standard slide under the finger, including shared-element flights.
  Programmatic push/pop and the nav-bar back button use the page's spec.
- Tab/root switches ignore page specs: adjacent roots slide in the travel direction, roots in
  different areas cross-fade. Only the scaffold-level spec's `DurationSeconds` is reused.
- Stacked motions get an automatic dim on the page beneath (proportional to coverage) — do not
  add your own scrim for depth.
- Shared elements: pair by exact string on both ends. Image pairs morph aspect crop and corner
  rounding (incl. `Border` clipping); any other pair (labels, boxes, scrims) cross-fades along the
  path. Flights use the VISIBLE geometry (clipped/parallaxed as rendered). Unmatched names or a
  target not yet laid out fall back to the plain slide silently — no exception.
- Pair header scrims too (same name on both pages) so photo dimming stays constant mid-flight.
- Transitions with and without shared elements move identically; flights ride the page motion.
- `Modal`: enters with `SlideFromBottom` unless the page sets its own `PageTransition`; covers the
  tab bar; nav bar shows title only (no back chevron, no flyout buttons); ALL system back is
  blocked (Android back/predictive back consumed, iOS edge swipe inert). Dismiss programmatically.
- `DismissableModal`: same, plus a close (X) button on the default nav bar and Android system back
  pops again — both through the engine (guards + lifecycle run). No interactive preview on
  either platform; on iOS the X is the only gesture-free dismissal.
- Modals are stack pages, not overlays: same `Push`/`Pop`, same lifecycle, same `ILeavingGuard`.
  For popups/bottom sheets use overlays → skill `nalu-scaffold-overlays`.
- `ILeavingGuard` on a page model disables the iOS edge swipe on that page (back button still
  runs the guard). Android predictive back commit hands off to the engine, guards honored.
- Android predictive back needs `android:enableOnBackInvokedCallback="true"` on `<application>`
  in `Platforms/Android/AndroidManifest.xml` (already set in the template). Root pages defer to
  the system back-to-home preview. Third-party back callbacks: see reference.md.
- `Page.OnBackButtonPressed` is unsupported on hosted pages; use `ILeavingGuard`.
- Do NOT use `Shell.*` / `NavigationPage.*` attached properties, `Shell.Current`, or
  `NavigationPage`/`Shell` wrappers around pages — the Scaffold hosts plain `ContentPage`s.
  `page.Navigation.PushAsync/PushModalAsync/PopModalAsync/InsertPageBefore/RemovePage` throw
  `NotSupportedException` (only reads and `PopAsync` work); use `INavigationService`.
  Migration table in reference.md.
- Navigating while the soft keyboard is open dismisses it first on both platforms.
- Read `reference.md` when authoring custom motion specs, migrating Shell/NavigationPage
  markup, or debugging Android back interop with SDK/popup libraries.

## See also

- `nalu-navigation` — Push/Pop/intents/guards used to present and dismiss pages.
- `nalu-scaffold-structure` — tab bar, nav bar (title view, appearance, custom bar), safe areas.
- `nalu-scaffold-overlays` — popups and bottom sheets (non-stack presentation).
- `nalu-scaffold-keyboard` — soft keyboard behavior during navigation.
