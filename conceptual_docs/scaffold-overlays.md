# Scaffold Popups & Bottom Sheets

The Scaffold ships one shared overlay primitive powering popups, bottom sheets, drawers and
the tab bar's overflow panel: popups and sheets **stack** freely in open order, each above its
own scrim (the drawer and the tab bar panel are single-instance; the panel's scrim sits below
the bar), with consistent back/scrim-tap dismissal — identical on iOS and Android, no platform
modals involved.

## Popups

```csharp
IScaffoldPopup popup = await scaffold.ShowPopupAsync(new MyPopupView());

// Then either await the close...
await popup.Closed;          // completes on EVERY close path

// ...OR close it programmatically (also IAsyncDisposable):
await popup.CloseAsync();
```

`ScaffoldPopupOptions` (or the equivalent `ScaffoldPopup.*` attached properties on the view
itself — except `Anchor`, `AnchorOffset` and `CustomPlacer`, which are call-site only) control
presentation:

| Option | Purpose |
|--------|---------|
| `Placement` | `Center` (default) or anchor-relative: `AnchorBelow`, `AnchorAbove`, `AnchorStart`, `AnchorEnd` (dropdowns). |
| `Anchor` + `AnchorOffset` | The view the anchored placements are relative to. |
| `CustomPlacer` | `IScaffoldPopupPlacer` for fully custom geometry. |
| `Margin` | Safe-area-aware margin around the placement area. |
| `Scrim` | Dimming brush (always fades in/out; input-blocking even when transparent). |
| `CloseOnScrimTap` / `CloseOnBack` | Dismissal policy (default true). |

Declarative flavor — options attached to the view:

```xml
<ContentView nalu:ScaffoldPopup.Placement="AnchorBelow"
             nalu:ScaffoldPopup.Scrim="#40000000">
```

Popups enter with a subtle fade+scale; the content view is measured at its natural size within
the placement area.

## Bottom sheets

<img src="assets/images/scaffold-duration-sheet.gif" width="300" alt="A bottom sheet hosting a duration wheel, shown from a page model via IOverlayService" />

*The sample's duration picker: a model-first bottom sheet (`IOverlayService`) hosting the
`Nalu.Maui.Controls` DurationWheel, closing with a typed result.*

```csharp
await scaffold.ShowBottomSheetAsync(new FilterSheet(), new ScaffoldBottomSheetOptions
{
    Detents = [ScaffoldSheetDetent.Content, ScaffoldSheetDetent.Fraction(0.9)],
    InitialDetent = 0,
    ShowDragHandle = true
});
```

| Option | Purpose |
|--------|---------|
| `Detents` | Resting heights: `Content` (natural height), `Fraction(0..1)`, `Height(dp)`. |
| `InitialDetent` | Index into `Detents`. |
| `AllowPullDownToClose` | Drag below the lowest detent dismisses (default true). |
| `ShowDragHandle` | The standard grabber. |
| `MaxWidth` | Caps sheet width (centered) — tablets/landscape. |
| `Scrim`, `CloseOnScrimTap`, `CloseOnBack` | As for popups. |

Sheets are draggable between detents with native-feeling physics; the sheet handles its own
bottom safe-area padding. The same `ScaffoldBottomSheet.*` attached properties exist for
declaring options on the sheet view.

## Soft keyboard

Sheets and popups hosting text input are **keyboard-aware** out of the box — no MAUI
`SafeAreaEdges` tweak, no keyboard manager:

- A **bottom sheet** treats the keyboard as a (much) bigger bottom safe-area inset: the sheet
  surface stays anchored to the window's bottom edge — continuous behind the keyboard — while its
  content is padded up to the keyboard's top edge. Detents keep resolving against the window
  height, so a `Fraction`/`Height` sheet keeps its size and its *content area* shrinks; a
  `Content` sheet grows by the keyboard. Put scrollable forms in a `ScrollView`: the shrunken
  content area then scrolls, and the platform brings the focused entry into view.
- A **popup** is re-placed in the area **above** the keyboard: a centered popup re-centers in what
  is left, an anchored one flips/clamps into it (an `IScaffoldPopupPlacer` simply receives the
  smaller area).

Both move **with** the keyboard animation and go back where they were when it hides. The
keyboard geometry comes from `UIView.keyboardLayoutGuide` on iOS and from the IME window insets
on Android — which is why the scaffold configures the app for it at startup
(`UseNaluScaffold()`):

- **iOS**: MAUI's built-in `KeyboardAutoManagerScroll` is disconnected (it pans/scrolls the
  presented view controller under the keyboard and fights the scaffold's overlay layer).
- **Android**: the activity goes edge-to-edge (`EdgeToEdge.enable()`) and its window is forced
  to `adjustResize` — the only mode in which the framework reports IME insets. MAUI's
  `Application.On<Android>().WindowSoftInputModeAdjust` (default `Pan`) is overridden. Page
  content keeps MAUI's own `SafeAreaEdges` behavior (`SoftInput` is part of the default).

`Nalu.Maui.Core`'s `UseNaluSoftKeyboardManager` is **not supported** alongside the scaffold (it
re-pads the page controller / rewrites the soft-input mode); the scaffold's analyzer reports it as
an error (`NALU0104`). Because of these mechanisms the scaffold package targets **iOS 15+ and
Android API 30+**.

## Tab bar panels

`Scaffold.ShowTabBarPanelAsync(View, Brush? scrim, bool closeIfOpened)` presents a panel
docked above the tab bar **while keeping the bar interactive** (the scrim covers the page, not
the bar) — this is what the default tab bar's overflow "More" uses (see
[Structure & Tab Bar](scaffold-structure.md#the-tab-bar)), available for your own quick-switch
panels. `ScaffoldTabBar.ShowPanelAsync(...)` is the area-level equivalent.

## MVVM overlays — `IOverlayService`

For model-first flows, register overlays and show them without touching views:

```csharp
builder.UseNaluScaffold(scaffold => scaffold.AddOverlays());
```

**`AddOverlays()` is source-generated** (the generator ships inside the NuGet package): at
build time it discovers every class whose public constructor takes `IOverlayRef` — the
model-first anchor — and emits plain `AddOverlay<...>` calls, AOT/trim-safe:

- a **`View`-derived** class taking `IOverlayRef` registers **view-only**
  (`AddOverlay<TView>()`): the view is its own lifecycle target, shown via
  `Show*Async<TView>()`, its `BindingContext` left untouched;
- any **other** class is an overlay **model**, paired with the `View` whose constructor takes
  the model type (a view assigning it to `BindingContext` wins ties), or the
  `FooModel → FooView` naming convention.

`[AutoOverlay]` tunes the discovery: opt in a model that doesn't inject `IOverlayRef`, name
the view explicitly with `[AutoOverlay(typeof(TheView))]` when several match, or opt out with
`[AutoOverlay(Enabled = false)]`. Overlays in **other assemblies** (which the generator does
not scan) register manually — `AddOverlay<TModel, TView>()`, `AddOverlay<TView>()` and the
view-factory overload all remain available and compose freely with `AddOverlays()`.
Diagnostics `NALU0101`–`NALU0103` flag unresolvable or ambiguous views.

```csharp
public class ItemsPageModel(IOverlayService overlays)
{
    public async Task DeleteAsync()
    {
        // Model receives the intent; TResult completes when the overlay closes.
        var confirmed = await overlays.ShowPopupAsync<ConfirmDeleteModel, bool>(itemId);
        ...
        await overlays.ShowBottomSheetAsync<FilterSheetModel>();
    }
}
```

The overlay model can receive an intent, and closes itself through its `IOverlayRef`
(injected), optionally with a result. Options can still be passed per call, or declared on the
view via the attached properties.

Contract notes: keep ONE public constructor per model/view (multi-constructor selection is not
service-aware); `ILeavingAware` and `IAsyncDisposable`/`IDisposable` run on close, in one DI
scope per presentation. While the app is not scaffold-hosted (a non-scaffold navigation host,
or a platform without scaffold hosting — see [platform support](scaffold.md#platform-support)),
every call is a graceful no-op returning `default` immediately.

## Stack semantics

- Entries stack freely (popup over sheet over popup); each has its own scrim.
- **System back** closes the topmost entry with `CloseOnBack = true`; entries with
  `CloseOnBack = false` consume back without closing.
- Navigation closes all open overlays before the page swap.
- Overlay content never self-insets (the scrim covers the whole window uniformly); sheets and
  panels manage the safe areas that matter for them.

## Modal pages are not overlays

Full modal *pages* (with their own navigation stack semantics) are a navigation feature —
`Scaffold.PageMode` — documented in [Transitions & Modals](scaffold-transitions.md).
