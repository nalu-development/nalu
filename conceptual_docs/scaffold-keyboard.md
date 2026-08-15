# Scaffold & the Soft Keyboard

Everything the scaffold does about the on-screen keyboard, in one place: what happens out of the
box, the single knob that changes it (`Scaffold.KeyboardMode`), how pages, bottom sheets and
popups behave in each mode, and how to combine the scaffold's `None` mode with MAUI's own
`SafeAreaEdges` when you want per-view control.

## TL;DR

- Out of the box, **the keyboard takes room away from whatever hosts the focused input**
  (`Resize`): a page lays out above it exactly as it lays out above the home indicator, a bottom
  sheet pads its content above it, a popup is re-placed above it. Everything rides the keyboard
  animation on both platforms.
- **One surface owns the keyboard at a time**: the topmost presented sheet or popup, otherwise
  the page. Nothing else moves.
- `Scaffold.KeyboardMode` — declared on a page, on the `Scaffold` (app-wide default), or on
  sheet/popup content — switches a surface to `Pan` (slide, don't resize) or `None` (ignore).
- `None` on a page hands the keyboard back to MAUI: use `SafeAreaEdges="SoftInput"` on the
  layouts that should pad themselves.
- `Nalu.Maui.Core`'s `UseNaluSoftKeyboardManager` is not supported with the scaffold (analyzer
  error `NALU0104`).

## What the scaffold sets up at startup

`UseNaluScaffold()` wires the platform so the keyboard is a *geometry* the scaffold can read and
animate against:

| Platform | Setup | Why |
|---|---|---|
| iOS | MAUI's built-in `KeyboardAutoManagerScroll` is disconnected at `FinishedLaunching`. Keyboard geometry is read from `UIView.keyboardLayoutGuide` (a hidden tracker view pinned to the guide, observed through KVO). | The MAUI manager scrolls/pans the presented view controller under the keyboard and fights the scaffold's overlay layer; the guide gives the exact overlap — accessory bar included — and changes *inside* UIKit's keyboard animation, so surfaces re-placed from it move with the keyboard. |
| Android | `EdgeToEdge.enable()` on the activity; the window's soft-input mode is forced to `adjustResize` (MAUI's `Application.On<Android>().WindowSoftInputModeAdjust` default `Pan` is overridden). Keyboard geometry is read from the IME window insets, per animation frame. | The framework only reports IME insets under `adjustResize`, and only an edge-to-edge window keeps its size while they arrive. |

Because of these mechanisms the scaffold package targets **iOS 15+** and **Android API 30+**,
and requires MAUI **10.0.90+**.

> `HideSoftInputOnTapped` keeps working on scaffold pages (the scaffold raises the navigation
> events it is gated on), and navigating away hides the keyboard before the page swap on both
> platforms.

### Focused inputs inside scrollable content

The scaffold moves *surfaces*; revealing the focused input inside a `ScrollView`, a
`VirtualScroll` or a `CollectionView` is the platform's job, and it does it once the surface has
been resized:

- **Android** — `EditText` asks its ancestors to bring the caret on screen every time the caret
  moves (`adjustResize` semantics): entries and editors, single- or multi-line, are revealed and
  the caret line follows typing in any scroll container.
- **iOS** — UIKit reveals a `UITextField` (`Entry`) through its ancestor scroll views by itself. A
  `UITextView` (`Editor`) only gets the same treatment when it cannot scroll on its own; MAUI leaves
  `scrollEnabled` on for auto-sizing editors, so UIKit "scrolls the caret into view" inside the
  editor and never scrolls the page. `UseNaluScaffold()` appends an `Editor` mapper that turns
  `scrollEnabled` off for `AutoSize="TextChanges"` editors without a `MaximumHeightRequest`
  (those can still overflow and keep scrolling) — after which the caret line of a growing editor is
  kept above the keyboard exactly like an entry.

Under `Pan`, the scaffold itself follows the caret: the surface slides for the **caret line** of a
multi-line editor (not the editor's whole height, which may exceed the room above the keyboard) and
re-pans as lines are added (iOS reacts to text changes; moving the caret by tapping inside a
tall editor does not re-pan on iOS — UIKit offers no selection-change notification).

## The vocabulary: `Scaffold.KeyboardMode`

```csharp
public enum ScaffoldKeyboardMode { Resize, Pan, None }
```

One attached property, three places to declare it:

```xml
<!-- App-wide default for pages: on the Scaffold itself -->
<nalu:Scaffold nalu:Scaffold.KeyboardMode="Resize" ...>

<!-- Per page -->
<ContentPage nalu:Scaffold.KeyboardMode="None" ...>

<!-- Per sheet / popup: on the CONTENT you present -->
<VerticalStackLayout nalu:Scaffold.KeyboardMode="Pan">
    <Entry ... />
</VerticalStackLayout>
```

Resolution:

- **Page**: page value → `Scaffold` value → `Resize`.
- **Sheet / popup**: call-site option (`ScaffoldBottomSheetOptions.KeyboardMode`,
  `ScaffoldPopupOptions.KeyboardMode`) → attached value on the content → `Resize`.

The values are read live — changing the attached property on a presented page takes effect at
the next keyboard change.

## Who owns the keyboard

Exactly one surface reacts to the keyboard at any time:

1. the **topmost presented bottom sheet or popup**, if any;
2. otherwise the **current page**.

The others keep their resting geometry: a page never resizes or pans under an overlay's
keyboard, a sheet under a popup does not move. Ownership hands over when an overlay opens or
closes while the keyboard is up (the new owner reacts, the previous one goes back to rest), and
`Pan` follows the *focused input* within the owner (see below).

## Pages

| Mode | Behavior | Use it for |
|---|---|---|
| `Resize` (default) | The keyboard becomes the page's **bottom safe-area inset** — iOS: the page controller's `AdditionalSafeAreaInsets`; Android: the keyboard is folded into the page's system-bars inset. The page lays out above the keyboard the way it lays out above the home indicator / navigation bar; the tab bar footprint and the keyboard replace each other rather than adding up. `ScrollView`s shrink and keep the focused entry reachable, `Grid` star rows shrink, bottom-docked toolbars rise above the keyboard. Animated. | Forms, chat-like layouts, anything with a docked bottom bar — the mainstream behavior. |
| `Pan` | The whole page **slides up** by the *least* that keeps the focused input — the caret line of a multi-line editor — above the keyboard (its own overlap when no focused input can be located), never resizing anything; it follows the focus (tab to the next field re-pans) and the caret (typing into a growing editor re-pans), and slides back when the keyboard hides. Content at the top goes under the nav bar / status bar — Android's `adjustPan` semantics. | Fixed layouts whose upper part must not reflow (a full-bleed map with a search field, a canvas with a caption). |
| `None` | The scaffold does nothing. On Android the IME window insets reach the page untouched, on iOS MAUI's own keyboard observers run — i.e. **MAUI semantics apply** (see next section). | Pages that manage the keyboard themselves. |

### `None` + MAUI's `SafeAreaEdges="SoftInput"`

MAUI 10 has its own per-view keyboard handling: a layout whose bottom edge is
`SafeAreaEdges.SoftInput` (or `All`) pads itself by its overlap with the keyboard, and only that
layout — MAUI's `Default` for layouts is `Container` (system bars only), which is why a plain MAUI
app "does nothing" until you opt every page in.

Under the scaffold's default `Resize` the whole page is already padded, so **do not** add
`SoftInput` edges inside it — you would pad twice. Set the page to `None` when you want MAUI's
per-view behavior instead:

```xml
<ContentPage nalu:Scaffold.KeyboardMode="None" ...>
    <Grid RowDefinitions="*,Auto">
        <!-- a full-bleed map that stays put -->
        <maps:Map />

        <!-- only the composer pads itself above the keyboard -->
        <Grid Grid.Row="1" SafeAreaEdges="Container,None,Container,SoftInput" Padding="12">
            <Entry Placeholder="Message" />
        </Grid>
    </Grid>
</ContentPage>
```

What you get, honestly stated:

- Only the `SoftInput` layout moves; everything else stays under the keyboard. That is the
  point of this mode (per-view lifting), and also its limitation — content below the padded layout
  is covered.
- Android: the padding is applied when the window dispatches the insets (with the keyboard's
  final geometry, at the start of its animation) — it does not ride the animation frame by frame.
- iOS: MAUI applies it from the `UIKeyboardWillShow` notification with a plain layout pass — the
  layout **snaps** into place while the keyboard is still rising (that is MAUI's behavior, not the
  scaffold's; the scaffold's own modes animate).

Prefer `Resize` (page-wide) or `Pan` when you can; reach for `None` + `SoftInput` when you need
exactly one region to react.

## Bottom sheets

| Mode | Behavior |
|---|---|
| `Resize` (default) | The keyboard is treated as a **bigger bottom inset** of the sheet: the sheet surface stays anchored to the window's bottom edge (continuous behind the keyboard — no gap, no floating), its content is padded up to the keyboard's top edge. **Detents keep resolving against the window height**: a `Fraction`/`Height` detent keeps its size and its *content area* shrinks; a `Content` detent grows by the keyboard. Animated, both ways. |
| `Pan` | The sheet keeps its size and detent geometry, and slides up by the least that keeps the focused input (the caret line of a multi-line editor) above the keyboard, clamped so its top edge never passes the top inset. Follows focus changes and the caret while typing. Detent drags still work on the slid sheet. |
| `None` | The sheet ignores the keyboard (its content may be covered). |

Practical notes:

- Put scrollable forms in a `ScrollView`: under `Resize` the content area shrinks and the
  platform brings the focused entry into view; the tall-sheet-with-a-bottom-entry case is
  covered by the harness tests.
- The sheet's own bottom safe-area padding (home indicator / navigation bar) is replaced by the
  keyboard while it is up — the keyboard covers that region.
- On iOS 26 the keyboard frame (and the layout guide) includes MAUI's transparent text-input
  accessory band; the sheet content is padded above it, the sheet surface runs behind it.
- Inside a sheet the scaffold owns the inset math: do not set `SoftInput` edges on the sheet's
  content (on Android the IME insets are deliberately not delivered to overlay subtrees).

## Popups

| Mode | Behavior |
|---|---|
| `Resize` (default) | The placement area's bottom is the keyboard's top edge: a **centered** popup re-centers in what is left, an **anchored** popup flips/clamps into it (an anchor that ended up under the keyboard is still respected — the popup lands right above the keyboard), and a popup taller than the area gets shorter. An `IScaffoldPopupPlacer` simply receives the smaller area. Animated. |
| `Pan` | The popup is placed as if there were no keyboard, then slides up by the least that keeps the focused input (the caret line of a multi-line editor) above it (never resizing, never re-placing); follows focus and the caret while typing; slides back when the keyboard hides. |
| `None` | The popup ignores the keyboard. |

Popups over sheets: the popup, being topmost, owns the keyboard — the sheet stays put.

## Choosing a mode

- **Forms, dialogs with a couple of fields, chat composers** → `Resize` (default). Everything the
  user needs stays reachable.
- **A fixed-size surface whose top part must not move** (map + search bar, media picker +
  caption) → `Pan`, accepting that content below the focused field may be covered.
- **You already handle the keyboard, or need exactly one region to react** → `None` (page-level:
  combine with MAUI's `SafeAreaEdges="SoftInput"`).

## Testing & troubleshooting

- The TestApp harness page **"Scaffold Keyboard Overlay Tests"** exercises every combination
  (page Resize/Pan/None, sheets, tall sheets, centered/anchored/pan popups) and the
  `ScaffoldKeyboardOverlayChromeTests` suite asserts the geometry against a platform probe of the
  keyboard's real overlap (`SoftKeyboardProbe.CreateHeightLabel`). **"Scaffold Keyboard Content
  Tests"** + `ScaffoldKeyboardContentTests` cover entries and growing multi-line editors inside a
  `ScrollView` and a `VirtualScroll` under Resize and Pan (caret line stays above the keyboard
  while typing).
- **iOS simulator shows only a 44pt bar**: "Connect Hardware Keyboard" is on (⇧⌘K). The scaffold
  still treats that bar as the keyboard.
- **Android: nothing reacts and the whole window pans**: the window is not in `adjustResize` —
  check `adb shell dumpsys window windows | grep sim=`; the scaffold forces it through the MAUI
  window mapper, so something else is setting the mode afterwards.
- **`NALU0104`**: remove `UseNaluSoftKeyboardManager` — its iOS/Android hooks conflict with the
  scaffold's (it re-pads the page controller and rewrites the soft-input mode).
