# Nalu Scaffold overlays — reference

## Custom popup placement

```csharp
public interface IScaffoldPopupPlacer
{
    // area: safe presentation area (system insets and Margin already excluded; chrome ignored).
    // contentSize: measured content size, already constrained to the area.
    // anchorBounds: the Anchor's bounds when ScaffoldPopupOptions.Anchor is set.
    // Returned rect is used AS-IS — the placer owns any clamping. Coordinates: scaffold dips.
    Rect Place(Rect area, Size contentSize, Rect? anchorBounds);
}
```
Set via `ScaffoldPopupOptions.CustomPlacer`; it overrides `Placement`. Under keyboard `Resize` the
`area` handed in is already reduced by the keyboard.

Built-in placements: `Center`; `AnchorBelow` (start-aligned with the anchor, flips above when it
does not fit); `AnchorAbove` (flips below); `AnchorStart` (top-aligned, flips to end); `AnchorEnd`
(flips to start). `AnchorOffset` pushes the popup away from the anchor along the placement
direction (sign follows the chosen side, RTL-mirrored). Popups enter with a fade+scale; the content
is measured at its natural size within the area (`MaximumWidthRequest`/`MaximumHeightRequest`
participate). Popups ignore the tab bar footprint — only system insets shape the area.

## Sheet chrome (`ScaffoldBottomSheetView : Border`)

Created per presentation, public as a styling surface:

| Property | Default | Notes |
|---|---|---|
| `SheetBackground` (Brush) | opaque white | Drives the view's `Background` — style THIS, not `Background` |
| `SheetCornerRadius` (double) | 16 | Top corners |
| `HandleColor` (Color) | `#4D8E8E93` | Drag handle |
| `SnapToDetentAsync(int)` | — | Animate to a detent index (clamped); no-op before geometry init. Reach it from content: `(ScaffoldBottomSheetView?)content.Parent?.Parent` |

```xml
<Style TargetType="nalu:ScaffoldBottomSheetView">
    <Setter Property="SheetBackground" Value="{AppThemeBinding Light=White, Dark=#1C1C1E}" />
</Style>
```

Detents: `Fraction` is relative to window height minus the top system inset; every detent is
clamped to the available height. Pull-down dismissal triggers when a drag is released ~56dp below
the smallest detent. Sheet pads its own bottom safe area (home indicator).

## Tab bar panels

`Scaffold.ShowTabBarPanelAsync(View content, Brush? scrim = null, bool closeIfOpened = true)`
(`ScaffoldTabBar.ShowPanelAsync` forwards to it):
- docked above the tab bar; scrim covers the page, NOT the bar (bar stays interactive);
- single instance: `closeIfOpened = true` toggles (dismisses if a panel is open); `false` replaces the
  presented panel's content in place (crossfade, scrim brush updates, no scrim re-animation);
- content's horizontal `Margin` insets it from the container edges; the view is attached to the
  scaffold tree while presented (unless it already has a parent) and is reusable — handlers are NOT
  disconnected on close;
- this is what the default tab bar's overflow "More" uses.

## Generator (`AddOverlays()`) rules

`AddOverlays()` is emitted as an extension on `IScaffoldConfigurator` (global namespace, no using
needed) and expands to plain `AddOverlay<...>()` calls — trim/AOT-safe. Only the compiling assembly
is scanned (C# classes and `.xaml` code-behind pairs).

Anchor: a non-abstract, non-generic class with a public constructor taking `Nalu.IOverlayRef`,
or any class marked `[AutoOverlay]`.

| Anchor kind | Registration | View resolution |
|---|---|---|
| Derives from `Microsoft.Maui.Controls.View` | `AddOverlay<TView>()` (view-only; `BindingContext` untouched; intents to the view's `OnEnteringAsync`) | — |
| Anything else (model) | `AddOverlay<TModel, TView>()` | 1. `[AutoOverlay(typeof(TView))]` 2. the `View` whose public ctor takes the model type — several: prefer the one assigning that parameter to `BindingContext` 3. naming: strip trailing `Model`, look for `<Base>View` then `<Base>` |

`AutoOverlayAttribute(Type? viewType = null) { bool Enabled = true }`:
- `[AutoOverlay]` — opt in a model that does not inject `IOverlayRef`;
- `[AutoOverlay(typeof(FooView))]` — explicit view (ignored on a `View`-derived anchor); must be a
  concrete, non-generic `View`;
- `[AutoOverlay(Enabled = false)]` — skip; still registrable manually.

Manual registrations (`IScaffoldConfigurator`), composable with `AddOverlays()`:
- `AddOverlay<TModel, TView>()` — view built per presentation with the model and services resolvable
  through its ctor;
- `AddOverlay<TModel, TView>(Func<IServiceProvider, TModel, TView> viewFactory)` — zero-reflection
  escape hatch (`IOverlayRef` resolvable from the provider);
- `AddOverlay<TView>()` — view-only.

Diagnostics (category `NaluScaffold`):

| Id | Severity | Meaning |
|---|---|---|
| NALU0101 | Warning | Model discovered but no `View` with a ctor taking it nor a `<Base>View`/`<Base>` naming match — overlay skipped. Register manually or add `[AutoOverlay(typeof(...))]`. |
| NALU0102 | Warning | Several `View`s take the model in their ctor (no `BindingContext` tie-breaker) — skipped. Pick one with `[AutoOverlay(typeof(...))]` or register manually. |
| NALU0103 | Warning | `[AutoOverlay(typeof(X))]` names a type that is not a non-abstract, non-generic `View` — skipped. |
| NALU0104 | Error | `UseNaluSoftKeyboardManager` used alongside the scaffold — remove it (see skill `nalu-scaffold-keyboard`). |

## Model construction & lifecycle

- Model created via `ActivatorUtilities` against a provider serving `IOverlayRef` + the presentation
  scope; the view likewise, with the model additionally resolvable. `IOverlayRef` is resolvable
  throughout the scope (a view may inject it too).
- Order: construct model → construct view (`BindingContext ??= model` for model/view pairs) →
  `OnEnteringAsync(intent)` (typed overload by assignability, explicit interface impls count; else
  parameterless `IEnteringAware`) → present (skipped if close was requested during entering) →
  on close: `ILeavingAware.OnLeavingAsync()` → `IAsyncDisposable`/`IDisposable` → scope disposed.
- `IOverlayService` is a singleton (inject like `INavigationService`, also from app-wide services); the
  registry is a singleton built once inside `UseNaluScaffold`; each presentation gets its own DI scope
  (child of the root, not of the calling page's scope).

## Options resolution timing

- Popup: content is attached to the scaffold tree FIRST (when parentless), attached values read AFTER —
  values from styles/resources resolved on parenting are honored.
- Sheet: geometry options (`Detents`, `InitialDetent`, `AllowPullDownToClose`, `ShowDragHandle`,
  `MaxWidth`) are read BEFORE the content is wrapped/attached (must be literal on the view);
  `Scrim`, `CloseOnScrimTap`, `CloseOnBack` are read after attachment.
- `ScaffoldSheetDetent` has no XAML type converter — set `Detents` in C# (options, or
  `ScaffoldBottomSheet.SetDetents(view, [...])` in the view's constructor).

## Stack semantics

- Entries stack freely (popup over sheet over popup), each with its own scrim (default: theme-aware
  translucent black, always fades in/out).
- System back (Android) closes the topmost entry with `CloseOnBack = true`; `false` consumes back
  without closing. iOS has no system back; edge-swipe pop is disabled while an overlay is open.
- Navigation closes all open overlays before the page swap; the resulting close is a dismissal.
- Overlay content never self-insets (`SafeAreaEdges.None` on the sheet chrome); the scrim covers the
  whole window uniformly.
