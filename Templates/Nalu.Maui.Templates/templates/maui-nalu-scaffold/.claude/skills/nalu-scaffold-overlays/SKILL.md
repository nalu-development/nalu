---
name: nalu-scaffold-overlays
description: Popups, bottom sheets and tab bar panels in Nalu.Maui.Scaffold — Show*Async APIs, options/attached properties, placement, detents, dismissal, and MVVM overlays via IOverlayService/AddOverlays(). Load when presenting or closing any overlay.
---
# Nalu Scaffold — Popups & Bottom Sheets

Package `Nalu.Maui.Scaffold`, namespace `Nalu` (global using in this app), XAML
`xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"`. One overlay layer drawn by the
scaffold above all chrome (no platform modals): popups and sheets **stack** in open order, each
above its own scrim; the tab bar panel is single-instance and keeps the bar interactive.
Two entry points: view-first (`Scaffold.Show*Async(View, options)`) and model-first
(`IOverlayService.Show*Async<TModel[, TResult]>(intent, options)`).
Keyboard behavior of sheets/popups (`KeyboardMode`) → see skill `nalu-scaffold-keyboard`.
Modal *pages* (`Scaffold.PageMode`) are navigation, not overlays → see skill `nalu-scaffold-transitions`.

## Quick reference

| API | Purpose | Notes |
|---|---|---|
| `Scaffold.ShowPopupAsync(View, ScaffoldPopupOptions?)` → `Task<IScaffoldPopup>` | Present a popup | Content is single-use (handlers disconnected on close) |
| `Scaffold.ShowBottomSheetAsync(View, ScaffoldBottomSheetOptions?)` → `Task<IScaffoldPopup>` | Present a bottom sheet | Content wrapped in `ScaffoldBottomSheetView` chrome |
| `Scaffold.ShowTabBarPanelAsync(View, Brush? scrim = null, bool closeIfOpened = true)` | Panel docked above the tab bar | `closeIfOpened`: true = toggle, false = replace content in place; view reusable |
| `ScaffoldTabBar.ShowPanelAsync(...)` | Same, from the tab bar element | |
| `IScaffoldPopup` (`IsOpen`, `Closed`, `CloseAsync()`, `IAsyncDisposable`) | Lifetime handle | `Closed` completes on EVERY close path; `CloseAsync` idempotent; `await using` scopes it |
| `element.GetScaffold()` / `GetScaffoldOrDefault()` | Reach the owning scaffold from a page/view | Only after parenting — never in a constructor |
| `IOverlayService` (singleton) | Model-first `ShowPopupAsync` / `ShowBottomSheetAsync` | `<TModel, TResult>` returns `Task<TResult?>`; `<TModel>` returns `Task` |
| `IOverlayRef` (`CloseAsync()`, `CloseAsync(object? result)`) | Injected into the model (or view) to close itself | Result validated against `TResult` at close time |
| `UseNaluScaffold(s => s.AddOverlays())` | Source-generated registration of this assembly's overlays | Combine with manual `AddOverlay<...>` for other assemblies |
| `[AutoOverlay]`, `[AutoOverlay(typeof(TView))]`, `[AutoOverlay(Enabled = false)]` | Tune generator discovery | Opt in / pick view / opt out |

Popup options (`ScaffoldPopupOptions`, all nullable; attached `nalu:ScaffoldPopup.*` for the starred ones):
`Placement`* (`Center` default, `AnchorBelow`, `AnchorAbove`, `AnchorStart`, `AnchorEnd`), `Anchor` (View),
`AnchorOffset` (Point), `CustomPlacer` (`IScaffoldPopupPlacer`), `Margin`* (default 16, safe-area-aware),
`Scrim`* (Brush; always input-blocking, even transparent), `CloseOnScrimTap`* / `CloseOnBack`* (default true), `KeyboardMode`.

Sheet options (`ScaffoldBottomSheetOptions`; every one also as attached `nalu:ScaffoldBottomSheet.*`):
`Detents` (`ScaffoldSheetDetent[]`: `.Content`, `.Fraction(0..1)`, `.Height(dp)`; default `[Content]`; sorted/deduped),
`InitialDetent` (index, default 0), `AllowPullDownToClose` (default true), `ShowDragHandle` (default true),
`MaxWidth` (centered when window is wider), `Scrim`, `CloseOnScrimTap`, `CloseOnBack`, `KeyboardMode`.

Resolution for every option: **call-site value ?? attached value on the content ?? default**.

## Patterns

**View-first popup / dropdown from a page**
```csharp
// in a ContentPage (e.g. a Clicked handler; the page is parented by now)
var popup = await this.GetScaffold().ShowPopupAsync(new MenuView(), new ScaffoldPopupOptions
{
    Placement = ScaffoldPopupPlacement.AnchorBelow,
    Anchor = MenuButton,
    AnchorOffset = new Point(0, 4),
    Scrim = new SolidColorBrush(Colors.Transparent) // still blocks the content below
});
await popup.Closed;            // or: await popup.CloseAsync();
```

**Declarative presentation preferences on the content view**
```xml
<ContentView xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"
             nalu:ScaffoldPopup.Placement="AnchorBelow"
             nalu:ScaffoldPopup.Scrim="#40000000"
             nalu:ScaffoldPopup.CloseOnBack="False" ... />
```

**Bottom sheet with detents (view-first)**
```csharp
await using var sheet = await this.GetScaffold().ShowBottomSheetAsync(new FilterSheetView(), new ScaffoldBottomSheetOptions
{
    Detents = [ScaffoldSheetDetent.Content, ScaffoldSheetDetent.Fraction(0.9)],
    InitialDetent = 0,
    MaxWidth = 560
});
await sheet.Closed;
```

**MVVM overlay: model + view, discovered by `AddOverlays()`**
```csharp
// MauiProgram.cs
.UseNaluScaffold(scaffold => scaffold.AddOverlays())

// Overlays/ConfirmDeleteModel.cs — IOverlayRef in the ctor is the discovery anchor
public partial class ConfirmDeleteModel(IOverlayRef overlay) : ObservableObject, IEnteringAware<int>
{
    public int ItemId { get; private set; }
    public ValueTask OnEnteringAsync(int itemId) { ItemId = itemId; return ValueTask.CompletedTask; }
    [RelayCommand] private Task Confirm() => overlay.CloseAsync(true);
    [RelayCommand] private Task Cancel() => overlay.CloseAsync();       // caller gets default
}

// Overlays/ConfirmDeleteView.xaml.cs — ctor taking the model pairs it (or use FooModel -> FooView naming)
public partial class ConfirmDeleteView : ContentView
{
    public ConfirmDeleteView(ConfirmDeleteModel model) { BindingContext = model; InitializeComponent(); }
}

// PageModels/ItemsPageModel.cs
public class ItemsPageModel(IOverlayService overlays)
{
    public async Task DeleteAsync(int id)
    {
        var confirmed = await overlays.ShowPopupAsync<ConfirmDeleteModel, bool>(id);
        await overlays.ShowBottomSheetAsync<FilterSheetModel>(options: new() { Detents = [ScaffoldSheetDetent.Fraction(0.5)] });
    }
}
```

**View-only overlay (no model)**
```csharp
public partial class QuickNoteView : ContentView   // View + IOverlayRef ctor => AddOverlay<QuickNoteView>()
{
    public QuickNoteView(IOverlayRef overlay) { InitializeComponent(); /* overlay.CloseAsync(...) from handlers */ }
}
await overlays.ShowBottomSheetAsync<QuickNoteView>();
```

## Rules & gotchas

- `Closed` / the `IOverlayService` task complete on every close path: `CloseAsync`, scrim tap, pull-down,
  system back, or a navigation (navigation closes ALL open overlays before the page swap).
- Dismissals yield `default` for `TResult` — a `bool` result cannot distinguish `false` from a dismissal;
  use a reference wrapper type when that matters. `CloseAsync(null)` also completes with `default`.
- `IOverlayRef.CloseAsync(result)` throws `InvalidOperationException` when the result is not assignable
  to `TResult`, or when the overlay was shown through a resultless `Show*Async<TModel>()` overload.
- Closing from `OnEnteringAsync` is legal: the presentation is skipped entirely, the caller completes,
  `ILeavingAware` and disposal still run.
- Intent is NOT a ctor parameter: the model is built by DI (sees `IOverlayRef` + registered services),
  then `OnEnteringAsync(TIntent)` (typed via `IEnteringAware<TIntent>`, found by parameter assignability)
  or the parameterless `IEnteringAware.OnEnteringAsync()` runs. `ILeavingAware.OnLeavingAsync()` on close,
  then `IAsyncDisposable`/`IDisposable`.
- Each presentation gets its OWN DI scope (not a child of the page's scope): page-scoped services are not
  shared; pass what the overlay needs through the intent. Registry is built once at startup.
- ONE public constructor per model and per view — multi-ctor selection is not service-aware.
  If the view's ctor takes the model, `BindingContext` is set to the model automatically when still null;
  view-only overlays keep their `BindingContext` untouched.
- Generator discovery: any non-abstract, non-generic class with a public ctor taking `IOverlayRef`. A
  `View` subclass → view-only; anything else → model, paired to the `View` whose ctor takes the model
  (a view assigning it to `BindingContext` wins ties), else naming `FooModel → FooView` / `FooModel → Foo`.
  Only THIS assembly is scanned; other assemblies use `AddOverlay<TModel, TView>()`, `AddOverlay<TView>()`
  or `AddOverlay<TModel, TView>(Func<IServiceProvider, TModel, TView>)`. Diagnostics: NALU0101 no view,
  NALU0102 ambiguous view, NALU0103 invalid `[AutoOverlay(typeof(...))]` — see reference.md.
- Sheet GEOMETRY attached values (`Detents`, `InitialDetent`, `AllowPullDownToClose`, `ShowDragHandle`,
  `MaxWidth`) are read BEFORE the content is parented — they must be literal on the view (set in its ctor
  via `ScaffoldBottomSheet.SetDetents(this, [...])`), never from styles/bindings; otherwise pass options at
  the call site. Popup attached values are read after parenting (styles work).
- `Anchor`, `AnchorOffset`, `CustomPlacer` exist only on `ScaffoldPopupOptions` (no attached form).
  Anchor placements fall back to `Center` when `Anchor` is unset or not realized; they flip to the opposite
  side when they do not fit. Cap popup size with `MaximumWidthRequest`/`MaximumHeightRequest` on the content.
- The scrim always blocks input below, even when transparent; `CloseOnBack = false` makes the topmost entry
  consume back without closing. iOS edge-swipe pop is disabled while an overlay is open.
- Overlays FOLLOW their content's natural size after presentation: a popup re-fits/re-places and a `Content`
  detent sheet re-resolves its height when the content grows or shrinks (deferred images, expanded sections)
  — no call needed. Fixed `Fraction`/`Height` detents don't move.
- Popup/sheet content is single-use (handlers disconnected on close): create a new view per presentation.
  Tab bar panel content is reusable.
- Sheet is as tall as its LARGEST detent; drag rides the whole surface (inner scrollables arbitrate
  natively). Programmatic detent change: walk `content.Parent` up to the `ScaffoldBottomSheetView` ancestor and call `SnapToDetentAsync(i)` (the depth is an implementation detail).
  Style the chrome via `Style TargetType="nalu:ScaffoldBottomSheetView"` (`SheetBackground`,
  `SheetCornerRadius`, `HandleColor`) — `SheetBackground` defaults to opaque white, set it per theme.
- Overlay content never self-insets (scrim covers the whole window); sheets/panels handle their own safe
  areas. Do not set `SafeAreaEdges` on sheet content.
- Not scaffold-hosted (Windows/Catalyst, or no `Scaffold` presented yet): `IOverlayService` calls are no-ops
  returning `default`; `Show*Async` on an unpresented scaffold returns an already-closed handle.
- Read `reference.md` when you need `IScaffoldPopupPlacer` details, the full generator/`[AutoOverlay]` rules,
  or the diagnostics text.

## See also

- `nalu-scaffold-keyboard` — `KeyboardMode` for sheets/popups, keyboard ownership.
- `nalu-scaffold-structure` — tab bar (overflow "More" panel is a tab bar panel).
- `nalu-navigation` — `IEnteringAware`/`ILeavingAware`, intents, DI scopes for pages.
