# nalu-scaffold-transitions — reference

Read when: authoring a custom `ScaffoldPageTransition`, porting Shell/NavigationPage markup, or
debugging Android back behavior with third-party SDKs/popups.

## 1. Custom transition specs

```csharp
namespace Nalu;

public sealed record ScaffoldTransitionMotion(
    double FractionX = 0,   // horizontal translation as fraction of page WIDTH (+ = right; physical, not RTL-mirrored)
    double FractionY = 0,   // vertical translation as fraction of page HEIGHT (+ = down)
    double Scale = 1,       // uniform scale (1 = natural)
    double Opacity = 1)     // 1 = opaque
{
    public bool IsIdentity { get; }   // all four at natural values
}

public sealed record ScaffoldPageTransition(
    ScaffoldTransitionMotion Enter,   // incoming page START state → animated to natural
    ScaffoldTransitionMotion Behind,  // covered page END state ← animated from natural
    double DurationSeconds = 0.25)
{
    public bool IsAnimated { get; }   // DurationSeconds > 0 and at least one non-identity motion
}
```

Semantics:

- `Enter` is where the pushed page STARTS (it animates to its natural place). `Behind` is where
  the covered page ENDS (from natural). Pop plays exactly the same spec backwards.
- Both engines interpret the record with native animators (iOS animation blocks, Android property
  animators): every spec is seekable and reversible. Keep specs to these four channels — there is
  no hook for arbitrary per-frame code, custom easing, or per-view animation.
- Records are immutable: derive variants with `with`, e.g.
  `ScaffoldPageTransition.SlideFromRight with { DurationSeconds = 0.35 }`.
- Interactive gestures never play custom specs (they scrub the standard slide). Design specs
  knowing the programmatic push/pop and gesture pop may look different.
- Root switches use `ScaffoldPageTransition.Default` (adjacent roots) or a cross-fade (different
  areas), borrowing only the scaffold-level `DurationSeconds`.

Built-in values for calibration:

| Spec | Enter | Behind | Duration |
|------|-------|--------|----------|
| `Default` | `FractionX: 1` | identity | 0.25 |
| `SlideFromRight` | `FractionX: 1` | `FractionX: -0.3, Opacity: 0.9` | 0.25 |
| `SlideUpFade` | `FractionY: 0.03, Opacity: 0` | `Scale: 0.97, Opacity: 0.85` | 0.38 |
| `ZoomFade` | `Scale: 0.85, Opacity: 0` | `Scale: 1.05, Opacity: 0.6` | 0.3 |
| `SlideFromBottom` | `FractionY: 1` | `Scale: 0.97, Opacity: 0.9` | 0.3 |
| `None` | identity | identity | 0 |

Declaring and using a custom spec:

```csharp
// Transitions.cs
public static class Transitions
{
    public static readonly ScaffoldPageTransition Reveal = new(
        Enter: new ScaffoldTransitionMotion(FractionY: 0.05, Opacity: 0),
        Behind: new ScaffoldTransitionMotion(Scale: 0.97, Opacity: 0.9),
        DurationSeconds: 0.3);
}
```

```xml
<ContentPage xmlns:local="clr-namespace:MyApp"
             nalu:Scaffold.PageTransition="{x:Static local:Transitions.Reveal}">
```

Or from code on a page instance: `Scaffold.SetPageTransition(this, Transitions.Reveal);`
(`Scaffold.GetPageTransition`, `Scaffold.SetPageMode`/`GetPageMode`,
`Scaffold.SetTransitionName`/`GetTransitionName` are the C# accessors of the attached properties).

Resolution order (implemented by the scaffold for the PUSHED page):
`GetPageTransition(page)` → `SlideFromBottom` if `GetPageMode(page) != Default` →
`GetPageTransition(scaffold)` → `Default`.

## 2. Shared-element details

- The pair is matched by exact `Scaffold.TransitionName` string between the outgoing page and the
  incoming page of the SAME push/pop; use one view per name on each page.
- Image ↔ Image: aspect-crop morph (e.g. `AspectFill` thumbnail → full-bleed hero) plus corner
  radius interpolation, including a parent `Border` clip.
- Anything else (Label at different font sizes, BoxView, scrim, layout): rendered snapshots of
  both ends cross-fade along the path; z-order follows the live layouts.
- Flights use visible geometry: a thumbnail partially clipped by a `ScrollView`, or translated by
  a scroll-driven `TranslationY`, departs from where the user sees it.
- Fallbacks are silent: no match, or target view not laid out yet (e.g. data still loading on
  the detail page) → plain page slide, no flight. If a flight does not appear,
  check that the target view is measured/visible at push time (bind image sources synchronously
  or pass them via intent, skill `nalu-navigation`).
- Both interactive gestures scrub flights too: iOS edge swipe and Android predictive back move
  the pair with the finger, settle on commit, and fly home on cancel.

## 3. Modal presentation matrix

| Behavior | `Default` | `Modal` | `DismissableModal` |
|----------|-----------|---------|--------------------|
| Default transition | scaffold/`Default` | `SlideFromBottom` | `SlideFromBottom` |
| Tab bar | per `TabBarVisibility` | covered | covered |
| Nav bar back chevron / flyout buttons | yes | no | no |
| Close (X) button | no | no | yes (default nav bar) |
| Android back button/gesture | pops via engine | consumed, no pop | pops via engine |
| Predictive back preview | yes | no | no |
| iOS edge swipe | yes (unless `ILeavingGuard`) | no | no |
| Programmatic `Pop()` | yes | yes | yes |

`ScaffoldNavBarContext.IsModal` and `IsCloseButtonVisible` expose the state to custom nav bars.

## 4. Migration table (Shell / NavigationPage → Scaffold)

| Old | Scaffold |
|-----|----------|
| `Shell` / `NaluShell` subclass, `TabBar`/`Tab`/`ShellContent` | `nalu:Scaffold` + `ScaffoldTabBar` + `ScaffoldRoot PageType="{x:Type ...}"` (skill `nalu-scaffold-structure`) |
| `NavigationPage` wrapper, `NavigationPage.HasNavigationBar/HasBackButton/TitleView` etc. | Not used; pages are bare `ContentPage`s. Nav bar is drawn by the Scaffold: `nalu:Scaffold.IsNavBarVisible`, `nalu:Scaffold.TitleView`, `nalu:Scaffold.NavBarAppearance` |
| `Shell.NavBarIsVisible` | `nalu:Scaffold.IsNavBarVisible` |
| `Shell.TitleView` | `nalu:Scaffold.TitleView` (BindingContext is the PAGE MODEL) |
| `Shell.TabBarIsVisible` | `nalu:Scaffold.TabBarVisibility` (`Visible` default / `Auto` / `Hidden`) |
| `Shell.PresentationMode="ModalAnimated"` / modal routes | `nalu:Scaffold.PageMode="Modal"` or `"DismissableModal"` |
| `Shell.BackgroundColor`, `Shell.ForegroundColor`, `Shell.TitleColor`… | `ScaffoldNavBarAppearance` (page → area → scaffold merge) |
| `Shell.BackButtonBehavior` | Default nav bar back button + `ILeavingGuard`; custom bar via `Scaffold.NavBarView` |
| Shell flyout | `Scaffold.FlyoutStart`/`FlyoutEnd` + `ScaffoldFlyoutMenuView` |
| Native/Fragment page animations | `nalu:Scaffold.PageTransition` + `nalu:Scaffold.TransitionName` |
| `Shell.Current`, `GoToAsync("route?x=1")`, query-string routes | `INavigationService` + typed intents (skill `nalu-navigation`) |
| `page.Navigation.PushAsync/PushModalAsync/PopModalAsync/InsertPageBefore/RemovePage` | `NotSupportedException` — use `INavigationService`. `PopAsync` and stack reads still work (engine-routed). |
| `Page.OnBackButtonPressed` override | Unsupported on hosted pages — implement `ILeavingGuard` (covers back button, gestures, tab taps, programmatic pops) |
| `UseNaluTabBar()` / `SetTabBarView` | Remove; default `ScaffoldTabBarView` or `ScaffoldTabBar.TabBarView` |

Registration: `.UseNaluNavigation<App>(nav => nav.AddPages())` unchanged, plus `.UseNaluScaffold()`.
Host: `new Window(serviceProvider.GetRequiredService<AppScaffold>())`, `AppScaffold` registered as
singleton. `Scaffold` subclass constructor takes no arguments.

## 5. Platform caveats

iOS
- Edge-swipe pop is a leading-edge pan (RTL-aware). It does not engage on pages whose model
  implements `ILeavingGuard`; the nav-bar back button runs the guard.
- Modal pages have no interactive dismissal at all; give `DismissableModal` pages the X (default
  bar) or a button bound to a Pop command.

Android
- Predictive back requires `android:enableOnBackInvokedCallback="true"` on `<application>`
  (template default). The opt-in is application-wide: every window stops receiving legacy
  `KEYCODE_BACK`.
- Root pages (empty stack) defer to the system back-to-home preview.
- The system gesture peeks the page below padded for ITS OWN nav/tab bar footprints.
- The Scaffold keeps its `OnBackPressedDispatcher` callback topmost. A permanently-enabled
  callback from an SDK sitting above it would swallow the predictive stream (pages still pop, no
  scrub); the Scaffold prevents that, so such SDKs only receive presses nothing consumes.
- `AndroidLifecycle.OnBackPressed` delegates run FIRST on every press (a consumer wins over the
  Scaffold). To feed an SDK every back press without consuming, register an observing delegate:

```csharp
builder.ConfigureLifecycleEvents(events => events.AddAndroid(android =>
    android.OnBackPressed(activity =>
    {
        // forward to SDK tracking API
        return false; // observe, never consume
    })));
```

- Third-party popups hosted in their OWN focusable window receive back directly from the
  system; if the vendor registered no `OnBackInvokedCallback`, back does nothing while it is
  focused and nothing on the activity window can intercept it. Workaround: on popup open,
  `view.FindOnBackInvokedDispatcher()?.RegisterOnBackInvokedCallback(0, callback)` (API 33+)
  where the callback closes the popup (vendor API, or synthesize `Keycode.Back` down/up via
  `view.DispatchKeyEvent`); unregister on close.

Both
- Navigating while the soft keyboard is open dismisses it before the swap (skill
  `nalu-scaffold-keyboard`).
- Windows/Mac Catalyst cannot host a `Scaffold` (throws `PlatformNotSupportedException`).
