---
name: nalu-scaffold-keyboard
description: Soft keyboard under Nalu.Maui.Scaffold — Scaffold.KeyboardMode (Resize/Pan/None) for pages, sheets and popups, keyboard ownership, {nalu:KeyboardBinding} to KeyboardState (IsVisible/Height), MAUI SafeAreaEdges interplay, NALU0104. Load for any input/keyboard layout work.
---
# Nalu Scaffold — Soft Keyboard

Package `Nalu.Maui.Scaffold`, namespace `Nalu`, XAML `xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"`.
Mental model: `UseNaluScaffold()` turns the keyboard into a *geometry* the scaffold reads and animates
against; **exactly one surface** reacts to it (topmost presented sheet/popup, otherwise the page), and one
attached property, `Scaffold.KeyboardMode`, picks how (`Resize` default, `Pan`, `None`).
Presenting sheets/popups themselves → see skill `nalu-scaffold-overlays`.

## Quick reference

| Item | Value / behavior |
|---|---|
| `enum ScaffoldKeyboardMode` | `Resize`, `Pan`, `None` |
| `nalu:Scaffold.KeyboardMode` (attached, `ScaffoldKeyboardMode?`) | On a `Page` (its policy); on the `Scaffold` (app-wide page default); on sheet/popup CONTENT (that overlay's policy). Read live — changes apply at the next keyboard change |
| `ScaffoldBottomSheetOptions.KeyboardMode`, `ScaffoldPopupOptions.KeyboardMode` | Call-site override for an overlay |
| Resolution — page | page value → `Scaffold` value → `Resize` |
| Resolution — sheet/popup | call-site option → attached value on content → `Resize` |
| Owner | topmost presented sheet or popup; otherwise the current page. Others keep resting geometry. Ownership hands over when an overlay opens/closes with the keyboard up |
| `Scaffold.KeyboardState` (`ScaffoldKeyboardState`: `IsVisible`, `Height` dp) | One observable per scaffold, fed by the platform geometry per animation frame; global (says whether the keyboard is up, not who owns it) |
| `{nalu:KeyboardBinding Path, Converter, ConverterParameter, StringFormat, Mode}` / `KeyboardBindings.Create(...)` / `KeyboardBindings.ScaffoldAncestor` | Bind to the state from any element inside the scaffold tree (pages, sheet/popup content, chrome) |
| Platform floors | iOS 15+, Android API 30+, MAUI 10.0.90+ |
| Analyzer `NALU0104` (error) | `UseNaluSoftKeyboardManager` (Nalu.Maui.Core) is not supported with the scaffold — remove it |

What `UseNaluScaffold()` sets up:

| Platform | Setup |
|---|---|
| iOS | MAUI's `KeyboardAutoManagerScroll` disconnected at launch; keyboard geometry read from `UIView.keyboardLayoutGuide` (accessory bar included, changes inside UIKit's animation). An `Editor` mapper turns `scrollEnabled` off for `AutoSize="TextChanges"` editors without `MaximumHeightRequest` so UIKit reveals the caret through ancestor scroll views |
| Android | `EdgeToEdge.enable()`; window soft-input mode forced to `adjustResize` (overrides `Application.On<Android>().WindowSoftInputModeAdjust`); keyboard geometry from IME window insets per animation frame |

Per-surface behavior:

| Mode | Page | Bottom sheet | Popup |
|---|---|---|---|
| `Resize` (default) | Keyboard becomes the page's bottom safe-area inset (iOS `AdditionalSafeAreaInsets`, Android folded into system-bars inset). Page lays out above it as above the home indicator; tab bar footprint and keyboard replace each other. ScrollViews shrink, `*` rows shrink, bottom-docked bars rise. Animated | Bigger bottom inset: surface stays anchored to the window bottom (runs behind the keyboard), content padded to the keyboard's top. Detents still resolve against window height: `Fraction`/`Height` keep size, content area shrinks; `Content` grows. Animated | Placement area's bottom = keyboard top: centered popup re-centers, anchored popup flips/clamps above the keyboard (anchor under the keyboard still respected), too-tall popup gets shorter; `IScaffoldPopupPlacer` receives the smaller area. Animated |
| `Pan` | Whole page slides up by the LEAST that keeps the focused input (caret line of a multi-line editor) above the keyboard; follows focus and caret; never resizes; top content goes under nav/status bar (`adjustPan` semantics) | Keeps size/detents, slides up the least needed, clamped so its top never passes the top inset; detent drags still work | Placed as if no keyboard, then slides up the least needed; never re-placed/resized |
| `None` | Scaffold does nothing → MAUI semantics (Android IME insets reach the page; iOS MAUI keyboard observers run). Combine with `SafeAreaEdges="SoftInput"` | Ignores the keyboard (content may be covered) | Ignores the keyboard |

## Patterns

**App-wide default + per-page override**
```xml
<!-- AppScaffold.xaml -->
<nalu:Scaffold nalu:Scaffold.KeyboardMode="Resize" ...>

<!-- Pages/MapPage.xaml: fixed layout whose top must not reflow -->
<ContentPage xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"
             nalu:Scaffold.KeyboardMode="Pan" ...>
```

**`None` + MAUI per-view lifting (only one region reacts)**
```xml
<ContentPage nalu:Scaffold.KeyboardMode="None" ...>
    <Grid RowDefinitions="*,Auto">
        <maps:Map />
        <Grid Grid.Row="1" SafeAreaEdges="Container,None,Container,SoftInput" Padding="12">
            <Entry Placeholder="Message" />
        </Grid>
    </Grid>
</ContentPage>
```

**Sheet / popup content declaring its mode, or the call site**
```xml
<VerticalStackLayout nalu:Scaffold.KeyboardMode="Pan">
    <Entry Placeholder="Search" />
</VerticalStackLayout>
```
```csharp
await overlays.ShowBottomSheetAsync<NoteSheetModel>(options: new ScaffoldBottomSheetOptions
{
    Detents = [ScaffoldSheetDetent.Fraction(0.9)],
    KeyboardMode = ScaffoldKeyboardMode.Resize
});
```

**React to the keyboard: collapse a banner, size a spacer**
```xml
<Border IsVisible="{nalu:KeyboardBinding IsVisible, Converter={StaticResource InvertedBool}}" ... />
<BoxView HeightRequest="{nalu:KeyboardBinding Height}" />
<Label Text="Tap outside to dismiss" IsVisible="{nalu:KeyboardBinding IsVisible}" />
```
```csharp
banner.SetBinding(VisualElement.IsVisibleProperty, KeyboardBindings.Create("IsVisible", converter: new InvertedBoolConverter()));
spacer.SetBinding(VisualElement.HeightRequestProperty, static (Scaffold s) => s.KeyboardState.Height, source: KeyboardBindings.ScaffoldAncestor); // typed, AOT-safe
```

**Chat-like page (default `Resize`, nothing to declare)**
```xml
<Grid RowDefinitions="*,Auto">
    <CollectionView ... />
    <Grid Grid.Row="1" Padding="12"><Entry Placeholder="Message" /></Grid>   <!-- rises above the keyboard -->
</Grid>
```

## Rules & gotchas

- Under `Resize` the whole page is already padded — do NOT add `SafeAreaEdges="SoftInput"` (or `All`)
  inside it: you would pad twice. Use `SoftInput` only with `Scaffold.KeyboardMode="None"` on the page.
- Inside a sheet the scaffold owns the inset math: never set `SoftInput` edges on sheet content (Android
  IME insets are deliberately not delivered to overlay subtrees).
- One owner: a page never resizes/pans under an overlay's keyboard; a sheet under a popup does not move
  (the popup owns it). `Pan` follows the focused input WITHIN the owner.
- `None` + `SoftInput` honestly: only the `SoftInput` layout moves, content below it is covered; Android
  applies the padding at inset dispatch (final geometry, not per frame); iOS MAUI snaps the layout on
  `UIKeyboardWillShow` (no animation). The scaffold's own modes animate on both platforms.
- Focused input inside `ScrollView` / `VirtualScroll` / `CollectionView`: the scaffold moves surfaces, the
  platform reveals the caret after the resize. Android: always (`EditText` requests caret visibility).
  iOS: `Entry` always; `Editor` only when it cannot scroll — the scaffold's mapper handles
  `AutoSize="TextChanges"` editors WITHOUT `MaximumHeightRequest`; editors with a max height keep
  scrolling internally and are not revealed by the page.
- `Pan` follows the caret LINE of a multi-line editor (not its whole height) and re-pans as lines are added.
  iOS: tapping to move the caret inside a tall editor does not re-pan (no selection-change notification).
- Put scrollable forms in a `ScrollView`: under `Resize` the content area shrinks and the platform brings
  the focused entry into view (also for a tall sheet with a bottom entry).
- Sheet under `Resize`: the sheet's own bottom safe-area padding is replaced by the keyboard while it is up.
  iOS 26: the keyboard frame includes MAUI's transparent input-accessory band — content is padded above it.
- `HideSoftInputOnTapped` keeps working on scaffold pages; navigating away hides the keyboard before the
  page swap on both platforms.
- `KeyboardState` is for CONTENT decisions (hide/show, alternate layouts, spacers under Pan/None): under
  `Resize` the owner is already padded — do not add keyboard-height padding on top. `IsVisible` is
  `Height > 0` (iOS hardware-keyboard accessory bar counts). Do not use `Nalu.Maui.Core`'s
  `SoftKeyboardManager.State` in a scaffold app.
- Do not call `UseNaluSoftKeyboardManager` (NALU0104): its hooks re-pad the page controller and rewrite
  the soft-input mode, fighting the scaffold. Do not set `WindowSoftInputModeAdjust` yourself either.
- Choosing: forms/dialogs/chat composers → `Resize`; fixed surface whose top must not move (map + search,
  media + caption) → `Pan` (content below the field may be covered); you handle the keyboard or need
  exactly one region to react → `None` (+ `SoftInput` on pages).

Troubleshooting:
- iOS simulator shows only a 44pt bar: "Connect Hardware Keyboard" is on (Shift+Cmd+K); the scaffold treats
  that bar as the keyboard.
- Android: nothing reacts and the whole window pans → the window is not in `adjustResize` (check
  `adb shell dumpsys window windows | grep sim=`); something sets the soft-input mode after the scaffold's
  window mapper — remove it.
- Layout padded twice / gap above keyboard → a `SoftInput` edge under `Resize`, or a sheet content with
  `SafeAreaEdges` set.
- A surface does not react → it is not the owner (only the topmost presented sheet/popup, else the page,
  reacts) or its resolved mode is `None`.

## See also

- `nalu-scaffold-overlays` — presenting sheets/popups, options and attached properties.
