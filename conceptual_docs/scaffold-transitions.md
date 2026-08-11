# Scaffold Transitions, Shared Elements & Modals

Every page transition in the Scaffold is Nalu-driven: declarative specs, seekable
animations, interactive gestures on both platforms — no Shell/Fragment animation quirks.

## Page transitions

A `ScaffoldPageTransition` declares how the **pushed** page enters (`Enter` motion: fractional
translation, scale, opacity), what the covered page does behind it (`Behind`), and the
duration. The pop plays the same spec in reverse. The **interactive gestures** (iOS edge
swipe, Android predictive back) deliberately do NOT replay custom specs: a horizontal drag
scrubs the standard slide so the page tracks the finger — the page's own spec plays on
programmatic pushes and pops.

Built-in specs: `Default` (plain slide), `SlideFromRight` (iOS-style with behind parallax),
`SlideUpFade`, `ZoomFade`, `SlideFromBottom` (the modal default), `None`.

```xml
<!-- Scaffold-wide -->
<nalu:Scaffold nalu:Scaffold.PageTransition="{x:Static nalu:ScaffoldPageTransition.SlideFromRight}">

<!-- Or per page (the spec belongs to the PUSHED page) -->
<ContentPage nalu:Scaffold.PageTransition="{x:Static nalu:ScaffoldPageTransition.SlideUpFade}">
```

Custom specs are plain records:

```csharp
public static readonly ScaffoldPageTransition Reveal = new(
    Enter: new ScaffoldTransitionMotion(FractionY: 0.05, Opacity: 0),
    Behind: new ScaffoldTransitionMotion(Scale: 0.97, Opacity: 0.9),
    DurationSeconds: 0.3);
```

## Shared elements

Tag any view on both pages with the same `Scaffold.TransitionName` — matching pairs fly
between their geometries during push and pop, riding the standard slide:

<img src="assets/images/scaffold-shared-elements.gif" width="300" alt="Hero photo, temperature and icon flying between the card and the detail page" />

*The card's photo, temperature, icon — and even the darkening scrim — fly between the two
layouts on push and pop.*

```xml
<!-- List page: the card photo -->
<Image nalu:Scaffold.TransitionName="weather-photo" Source="..." Aspect="AspectFill" />

<!-- Detail page: the full-bleed hero -->
<Image nalu:Scaffold.TransitionName="weather-photo" Source="..." Aspect="AspectFill" />
```

The engines (custom on both platforms) are built for **truthful flights**:

- Flights travel between the *visible* geometries — clipped/parallaxed content flies as the
  user sees it, not as its unclipped frame says.
- **Image pairs** morph their aspect crop; corner rounding follows (a rounded card un-rounds
  as it expands, including MAUI `Border` clipping).
- **Any other pair** (labels at different font sizes, scrims, boxes) cross-fades between
  rendered copies along the path, stacked in the same order as the live layouts.
- Pair your header **scrims** too (same `TransitionName` on both) so photo dimming stays
  constant mid-flight.
- Unmatched pairs and not-yet-laid-out targets gracefully fall back to the plain slide.

## Depth cues

Stacked motions (push, pop, and both interactive gestures) carry one automatic depth cue,
identical on iOS and Android: the page revealed **beneath** sits under a well-visible dim
proportional to how covered it still is, lifting as the top page departs. Side-by-side motions
(root switches) get no cue — those pages are adjacent, not stacked.

## Interactive gestures

- **iOS edge-swipe pop**: leading-edge pan (RTL-aware) scrubs the standard slide — including
  shared-element flights — under the finger; release either completes (dispatching the pop
  through the engine) or cancels. On pages whose model implements `ILeavingGuard` the swipe
  simply does not engage — use the back button, which runs the guard.
- **Android predictive back**: the system back gesture peeks the page below — padded for
  where it will land (its own nav/tab bar footprints, not the scrubbed page's) — and scrubs
  the pop under the finger, **including shared-element flights**: matching
  `Scaffold.TransitionName` pairs fly between the two pages driven by the gesture, complete
  with the settle on commit, and reverse home on cancel. Committing hands off to the engine
  pop (guards honored, `enableOnBackInvokedCallback` required, root pages defer to the native
  back-to-home preview).

## Android back interop

Predictive back requires `android:enableOnBackInvokedCallback="true"` in the manifest — and
that opt-in is **application-wide**: every window of the app (dialogs and third-party popup
windows included) switches to the new back dispatch and stops receiving the legacy
`KEYCODE_BACK`. The Scaffold plays well with the rest of the ecosystem on top of that:

- **The Scaffold's callback keeps itself topmost** on the activity's `OnBackPressedDispatcher`.
  Libraries register permanently-enabled callbacks of their own (analytics SDKs, popup
  frameworks); if one sits above the Scaffold's it swallows the predictive stream in its empty
  `Started`/`Progressed` defaults — pages still pop, but the scrub silently never runs.
- **`AndroidLifecycle.OnBackPressed` delegates keep working.** MAUI's activity gives those
  delegates the first chance at every back press, but its own callback is permanently disabled
  for non-Shell window content — so the Scaffold pumps the event itself: delegates run first
  (a consumer wins over every Scaffold concern), and when nothing at all consumes, the press is
  re-dispatched below, exactly like MAUI's own handling.
- **Third-party popups hosted in their own window** (some vendors present popups as separate
  focusable windows): while such a popup is focused, the system delivers back to *that*
  window — if the vendor never registered an `OnBackInvokedCallback` there, back does nothing.
  No library on the activity window (Nalu included) can intercept this; until the vendor adds
  predictive-back support, restore the old close-on-back behavior from the app:

  ```csharp
  #if ANDROID
  // Wire these to your popup's Opened/Closed events.
  Android.Window.IOnBackInvokedCallback? _popupBackCallback;

  void OnPopupOpened(object? sender, EventArgs e)
  {
      if (!OperatingSystem.IsAndroidVersionAtLeast(33)
          || (sender as IElement)?.Handler?.PlatformView is not Android.Views.View view
          || view.FindOnBackInvokedDispatcher() is not { } dispatcher)
      {
          return;
      }

      _popupBackCallback = new PopupBackCallback(() => ClosePopup());
      dispatcher.RegisterOnBackInvokedCallback(0 /* PRIORITY_DEFAULT */, _popupBackCallback);
  }

  void OnPopupClosed(object? sender, EventArgs e)
  {
      if (OperatingSystem.IsAndroidVersionAtLeast(33)
          && _popupBackCallback is { } callback
          && (sender as IElement)?.Handler?.PlatformView is Android.Views.View view)
      {
          view.FindOnBackInvokedDispatcher()?.UnregisterOnBackInvokedCallback(callback);
          _popupBackCallback = null;
      }
  }

  sealed class PopupBackCallback(Action onBack) : Java.Lang.Object, Android.Window.IOnBackInvokedCallback
  {
      public void OnBackInvoked() => onBack();
  }
  #endif
  ```

## Modal pages

Modals are **navigation**, not overlays — same stack, same lifecycle, different presentation:

```xml
<ContentPage nalu:Scaffold.PageMode="Modal">              <!-- Default | Modal | DismissableModal -->
```

- `Modal` presents with `SlideFromBottom` (override with a page transition), hides the back
  chevron and drawer buttons, and blocks system back entirely (Android back/predictive back,
  iOS edge swipe) — dismissal is programmatic only.
- `DismissableModal` additionally shows the close (X) button and lets the Android system back
  dismiss; both route through the navigation engine (guards and lifecycle run). Interactive
  previews stay disabled for both modal modes — on iOS the X is the only gesture-free
  affordance.
- Push and pop modals with regular navigations (`Push<MyModalPageModel>()` / `Pop()`); the nav
  bar context exposes `IsModal` / `IsCloseButtonVisible` for custom bars.

## Notes

- Tab/root switches don't use the page spec: neighbouring roots slide in the direction of
  travel, roots in different areas cross-fade.
- Navigating while the soft keyboard is open dismisses it before the swap on both platforms.
- Transitions with and without shared elements move identically — the flights ride the same
  page motion.
