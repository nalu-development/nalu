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
the placement area — and **re-placed whenever that natural size changes** after presentation (a
deferred image, an expanding section, a loaded list): the popup re-fits and re-centers/re-anchors
on its own, nothing to call.

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
bottom safe-area padding. A `Content` detent follows the content's natural height **live**: content
that grows or shrinks after presentation re-resolves the detent (the sheet stays bottom-anchored on
the detent it rests on). The same `ScaffoldBottomSheet.*` attached properties exist for
declaring options on the sheet view.

## Soft keyboard

Sheets and popups hosting text input are keyboard-aware out of the box: by default the keyboard
takes room away from the topmost presented sheet or popup (`Resize` — the sheet pads its content
above the keyboard, the popup is re-placed above it), and `Scaffold.KeyboardMode` on the content
(or `KeyboardMode` in the options) switches to `Pan` or `None`. The whole story — pages, sheets,
popups, who owns the keyboard, `None` + MAUI's `SafeAreaEdges` — lives in
[Scaffold & the Soft Keyboard](scaffold-keyboard.md).

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

Options can still be passed per call, or declared on the view via the attached properties.

### Closing: `IOverlayRef`

The ref the model (or the view — it is resolvable throughout the presentation scope) injects is
exactly two methods, both non-generic:

```csharp
public interface IOverlayRef
{
    Task CloseAsync();               // caller's task completes with default
    Task CloseAsync(object? result); // caller's task completes with the result
}
```

- the result is passed as `object?` and validated at close time against the `TResult` the
  overlay was shown with: a mismatch throws `InvalidOperationException`, and so does reporting
  any result at all when the overlay was shown through a **resultless** overload
  (`Show*Async<TModel>()`) — the result-carrying overload is the only one that accepts one;
- `CloseAsync(null)` on a `TResult` presentation completes the caller with `default`, which is
  also what every DISMISSAL produces (scrim tap, pull-down, system back, navigation). A model
  that must distinguish "dismissed" from an explicit empty answer needs a reference `TResult`
  wrapper — a `bool` result cannot tell `false` apart from a dismissal;
- closing is legal BEFORE the overlay is presented: a close requested from `OnEnteringAsync` is
  buffered and skips the presentation entirely — the caller's task completes without the
  overlay ever appearing, and `ILeavingAware`/disposal still run.

### Intents: `OnEnteringAsync`, not the constructor

The intent is NOT a constructor parameter — the model is built by DI first (it can only see
`IOverlayRef` and registered services), then the intent is delivered exactly as the navigation
engine delivers it to page models:

- a single-parameter method named `OnEnteringAsync` returning `ValueTask`, whose parameter type
  the intent is assignable to, is found by reflection and invoked — implementing
  `IEnteringAware<TIntent>` is the typed way to declare it (explicit interface implementations
  match too);
- when no such overload fits — or no intent was passed — the parameterless `IEnteringAware`
  hook runs instead, if implemented;
- `ILeavingAware` runs when the overlay closes, then `IAsyncDisposable`/`IDisposable`.

```csharp
public partial class DurationSheetModel(IOverlayRef overlay) : IEnteringAware<DurationSheetIntent>
{
    public ValueTask OnEnteringAsync(DurationSheetIntent intent) { ... }

    private Task Done() => overlay.CloseAsync(new DurationSheetResult(Duration));
}
```

### Scopes

`IOverlayService` is registered as a **singleton** (inject it like `INavigationService` — from
page models and from app-wide services alike), over a **singleton** registry built once inside
`UseNaluScaffold` — the `AddOverlay*` calls are evaluated at startup, never per presentation.
Each presentation creates its **own** DI scope for the model/view pair, disposed when the
overlay closes: it is a fresh scope (a child of the root provider), not a child of the calling
page's scope, so page-scoped services are not shared with the overlay — the intent is the
channel for what the overlay needs to know.

Keep ONE public constructor per model/view: multi-constructor selection is not service-aware.

While the app is not scaffold-hosted (a non-scaffold navigation host, or a platform without
scaffold hosting — see [platform support](scaffold.md#platform-support)), every call is a
graceful no-op returning `default` immediately.

### Options resolution order (popups vs sheets)

Both kinds resolve each option as *call-site value ?? the content's attached value ?? default*,
but they read the attached values at a different moment:

- a **popup** is attached to the scaffold's element tree FIRST (when the view has no parent
  yet) and read AFTER — attached values produced by styles or resources resolved on parenting
  are seen;
- a **sheet**'s GEOMETRY options (`Detents`, `InitialDetent`, `AllowPullDownToClose`,
  `ShowDragHandle`, `MaxWidth`) are read BEFORE the content is wrapped in the sheet chrome and
  attached — they must be literal on the view; only `Scrim`, `CloseOnScrimTap` and
  `CloseOnBack` are read after attachment.

Pass `ScaffoldBottomSheetOptions` at the call site when a geometry value is not a literal.

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
