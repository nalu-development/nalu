# nalu-scaffold-structure — reference

Long tail for `SKILL.md`. Namespace `Nalu`, `xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"`.

## 1. Structure details

- `Scaffold` is `[ContentProperty(Areas)]`; `ScaffoldArea`/`ScaffoldTabBar` are `[ContentProperty(Roots)]`.
  A `ScaffoldRoot` placed directly under `Scaffold` is implicitly wrapped in a single-root `ScaffoldArea`.
- `ScaffoldRoot.PageType` accepts the page type OR its registered page-model type. Root pages are created
  lazily through Nalu navigation (own DI scope, page-model lifecycle) on first selection.
- Absolute navigation resolves its destination root from the page type (no routes) → skill `nalu-navigation`.
- Selection state: `Scaffold.CurrentArea`, `ScaffoldArea.CurrentRoot`, `ScaffoldArea.IsSelected`,
  `ScaffoldRoot.IsSelected` — read-only bindables. `Scaffold.CurrentPage` = top of the current stack.
- `Scaffold.InitialRootPageType` (page or page-model type; must match a root or startup throws) and
  `Scaffold.InitialIntent` (delivered to that root's model via `IEnteringAware<TIntent>` /
  `IAppearingAware<TIntent>`). Default start: first root of the first area, `IsVisible` ignored.
- Runtime structure: `Areas` and `Roots` are observable and mutable. Added roots appear in chrome and are
  navigable; removing the CURRENT root navigates to a fallback root before removal completes. XAML hot
  reload re-inflates the scaffold and re-adopts existing stacks by segment (page state survives);
  `TabBarView` swaps apply live.
- `ScaffoldTabBar.SelectRootAsync(root)` → `Task<bool>` (false: guard canceled, another selection in
  flight, or not hosted). `ScaffoldRoot.SelectCommand.CanExecute` reports the same scaffold-wide gate.
- Helpers: `element.GetScaffold()` / `element.GetScaffoldOrDefault()` (`ScaffoldElementExtensions`),
  `Scaffold.FindNavBarContext(element)`, `Scaffold.NavigationEvent` (lifecycle events).
- Windows/Mac Catalyst: `UseNaluScaffold()` is callable and `IOverlayService`/`IScaffoldFlyoutController`
  resolve as no-ops, but hosting a `Scaffold` there throws `PlatformNotSupportedException` (iOS 15+ /
  Android API 30+ only).

## 2. Default tab bar — styling tables

Style with implicit styles (`Resources/Styles/Styles.xaml`). Defaults in parentheses.

`ScaffoldTabBarView` (the pill container + layout input):

| Property | Default | Notes |
|---|---|---|
| `BarBackground` (Brush) | #F2FFFFFF | Pill surface. |
| `BarCornerRadius` | 26 | |
| `BarMargin` (Thickness) | 10,0,10,10 | Around the pill, relative to the safe area (bottom measured from the top of the system inset). Part of the footprint. Style THIS, not `Padding`. |
| `BarPadding` | 6 | Inside the pill. |
| `BarShadow` (Shadow) | soft shadow | |
| `ItemWidth` | 76 | Single layout input: as many items as fit are shown, rest → overflow. Bar hugs `shown × ItemWidth + padding`, centered. |
| `OverflowIcon` (ImageSource) | drawn ••• glyph | |
| `OverflowTitle` | "More" | |
| attached `ScaffoldTabBarView.BadgeText` | null | Set on a `ScaffoldRoot`; null/empty hides the badge. Bindable. |

`ScaffoldTabBarItemView` (bar items AND overflow rows; instances created by the template):

| Property | Default |
|---|---|
| `IconSize` | 26 |
| `TextColor` / `SelectedTextColor` | #3A3A40 / #2C479D |
| `FontFamily` / `FontSize` | null / 11 |
| `SelectionPillBackground` (Brush) / `SelectionPillCornerRadius` | accent @ 12% / 20 |
| `BadgeBackground` (Brush) / `BadgeTextColor` / `BadgeFontSize` | accent / White / 11 |

`ScaffoldTabBarOverflowView` (the "More" panel):

| Property | Default |
|---|---|
| `PanelBackground` (Brush) | #FAFFFFFF |
| `PanelCornerRadius` | 22 |
| `PanelShadow` (Shadow) | soft shadow |
| `Scrim` (Brush) | black @ 45% |

Icons render untinted (avatars work); tint monochrome glyphs on the `ImageSource` itself.
Item content builds once the bar is parented to a `ScaffoldTabBar`; `OverflowIcon` changes rebuild the
More item.

## 3. Custom tab bar

```xml
<nalu:ScaffoldTabBar>
    <nalu:ScaffoldTabBar.TabBarView>
        <local:MyTabBar />   <!-- BindingContext = the ScaffoldTabBar -->
    </nalu:ScaffoldTabBar.TabBarView>
    <nalu:ScaffoldRoot ... />
</nalu:ScaffoldTabBar>
```

Inside `MyTabBar` (`x:DataType="nalu:ScaffoldTabBar"`): a `BindableLayout.ItemsSource="{Binding Roots}"`
with `x:DataType="nalu:ScaffoldRoot"` items using `Title`, `CurrentIcon`, `IsSelected`, `IsVisible`,
and a `TapGestureRecognizer Command="{Binding SelectCommand}"`. Selection stays engine-routed.

Safe area contract: the strip is sized to the bar's measured height and the bar extends into the bottom
system inset. Consume the inset on a CHILD (`SafeAreaEdges="None,None,None,Container"` on the element that
must clear the system bar), never on the bar's root view (its safe-area size change does not propagate on
iOS). A full-bleed background can then reach the screen bottom while content stays clear.

Panels: `tabBar.ShowPanelAsync(view, scrim?, closeIfOpened)` / `scaffold.ShowTabBarPanelAsync(...)` /
`scaffold.CloseTabBarPanelAsync()` / `scaffold.HasTabBarPanel` present a panel above the bottom chrome
with the bar kept interactive (the primitive behind "More"). Full contract → skill `nalu-scaffold-overlays`.

Tab bar visibility per page: `nalu:Scaffold.TabBarVisibility` `Visible` (default) / `Auto` (hidden while
the current stack has pushed pages, animated with push/pop) / `Hidden`. Changes reach the page as a
safe-area inset change, not a relayout. Modal pages always cover the tab bar.

## 4. Flyout (drawer) details

Resolution of content (`FlyoutStart`/`FlyoutEnd`) and mode (`FlyoutStartMode`/`FlyoutEndMode`), most
specific SET value wins: topmost pushed page that set it → older pushed pages → root page → current
`ScaffoldArea` → `Scaffold`. Options (`FlyoutStartOptions`/`FlyoutEndOptions`) are scaffold-level only.

| `ScaffoldFlyoutMode` | Behavior |
|---|---|
| `Disabled` (default) | No drawer, no button, even with content. |
| `Auto` | Available at stack roots only. |
| `Flyout` | Available on every page. |

`ScaffoldFlyoutOptions`: `Width` (-1 = ratio-based; ≥ 0 wins), `WidthRatio` (0.85 of window width),
`MaximumWidth` (360, caps the ratio), `Scrim` (Brush, gradients ok; null = built-in translucent black).

```xml
<nalu:Scaffold.FlyoutStartOptions>
    <nalu:ScaffoldFlyoutOptions Width="300" Scrim="#66000000" />
</nalu:Scaffold.FlyoutStartOptions>
```

Behavior: scrim fades, drawer slides from its edge (RTL-aware: `ScaffoldFlyoutSide.Start` = leading);
scrim tap and system back close it; any navigation closes it first; `OpenFlyoutAsync` no-ops when the
drawer does not exist now, when the scaffold is not presented yet, or when a flyout is already open.
State: `IsFlyoutStartOpen` / `IsFlyoutEndOpen` (read-only bindables), events `FlyoutStartOpened`,
`FlyoutStartClosed`, `FlyoutEndOpened`, `FlyoutEndClosed`. Page-level flyout content is a logical child
of the page (inherits its `BindingContext`) and is cleaned up when the page leaves the stack.

Nav-bar drawer buttons: `nalu:Scaffold.FlyoutStartButtonVisibility` / `FlyoutEndButtonVisibility`
(`Auto` = roots only, `Visible` = also next to the back button on pushed pages, `Hidden`). The button
never shows when the drawer is unavailable, nor on modal pages. Custom bars: bind
`IsFlyoutStartButtonVisible` / `OpenFlyoutStartCommand` (and End) from `ScaffoldNavBarContext`.

`ScaffoldFlyoutMenuView` (a `ScrollView`): renders the structure — one flat entry per VISIBLE root of a
single-root area, a `ScaffoldFlyoutMenuGroupHeader` (area `Title`) followed by entries for multi-root
areas. `ScaffoldTabBar` areas are excluded unless `IsTabBarDisplayed="True"`. Selection = `SelectCommand`
(guards run; active root pops to root).

| `ScaffoldFlyoutMenuView` | Default | Notes |
|---|---|---|
| `PanelBackground` (Brush) | White | Drawer surface — style THIS, not `Background`. |
| `ContentPadding` | 12,16 | |
| `ItemSpacing` | 2 | |
| `HeaderView` / `FooterView` | null | Scroll with the menu. |
| `ItemTemplate` (DataTemplate) | null → `ScaffoldFlyoutMenuItemView` | Item `BindingContext` = `ScaffoldRoot`; wrapped in a tappable host riding `SelectCommand`. |
| `IsTabBarDisplayed` | false | |

| `ScaffoldFlyoutMenuItemView` | Default |
|---|---|
| `SelectionBackground` (Brush) / `SelectionCornerRadius` | gray @ 18% / 10 |
| `IconSize` | 22 |
| `TextColor` / `FontFamily` / `FontSize` | #1C1C1E / null / 15 |
| `ItemPadding` / `Spacing` | 12,10 / 12 |

| `ScaffoldFlyoutMenuGroupHeader` | Default |
|---|---|
| `TextColor` / `FontFamily` / `FontSize` / `FontAttributes` | Gray / null / 13 / Bold |
| `HeaderPadding` | 12,12,12,4 |

Custom drawer content: any `View`; build menus with `ScaffoldRoot.SelectCommand`, close via
`IScaffoldFlyoutController.CloseAsync()` (page-scoped DI service registered by `UseNaluScaffold()`;
no-op when not scaffold-hosted).

## 5. Nav bar details

Default `ScaffoldNavBarView` (a `Grid`, sealed): slots start-drawer button, back button, title
(or `TitleView`), end-drawer button, close button. Owns ONLY strip metrics: `BarHeight` (48, excludes
status inset), `BarPadding` (8,0), `Spacing` (8, gap around the title column; icon buttons sit flush,
44dp tap targets). It consumes the top safe area itself (`SafeAreaEdges=Container`) — a custom bar must do
the same. The strip BACKGROUND is not the bar's: it comes from the resolved `Scaffold.NavBarBackground`.

The template's `AppScaffold.xaml` sets `nalu:Scaffold.NavBarTemplate` to a `DataTemplate` of `ScaffoldNavBarView` explicitly
(same as the default; the place to set `BarHeight` etc. by instance). `{x:Null}` removes the bar
scaffold-wide; per page prefer `nalu:Scaffold.IsNavBarVisible="False"` (animated, inset change).

Primitives (public, style directly; the same styles apply inside custom bars):

| Type | Properties (default) | Notes |
|---|---|---|
| `ScaffoldNavBarButtonBase` (abstract `Border`) | `Icon` (null = drawn glyph, rendered untinted), `IconColor` (#1C1C1E), `PressedBrush` | Style all buttons: `TargetType="nalu:ScaffoldNavBarButtonBase" ApplyToDerivedTypes="True"`. Visibility + command bound to the context. |
| `ScaffoldBackButton` | — | Shown when `CanNavigateBack`; runs `BackCommand`. |
| `ScaffoldCloseButton` | — | Shown for `DismissableModal` pages. |
| `ScaffoldFlyoutButton` | `Side` (`ScaffoldFlyoutSide`) | Shown per `IsFlyoutStart/EndButtonVisible`. |
| `ScaffoldNavBarTitle` (`Grid`) | `TextColor` (#1C1C1E), `FontFamily`, `FontSize` (17), `FontAttributes` (Bold) | Shows `TitleView` when set, else `Title`. |

Color precedence for title/buttons: explicitly set `TextColor`/`IconColor` (style or instance) →
appearance chain via the context (title: level-wise `TitleForeground` ?? `Foreground` — the first level
setting either wins; buttons: `Foreground`) →
built-in default. The template pins neither primitive (scaffold-level `Foreground` = accent,
`TitleForeground` = text-primary), so page-level appearances recolor title and buttons — together with
`Foreground` alone, or separately with both; a styled `TextColor`/`IconColor` would pin that primitive.

The nav bar appearance properties are attached to the element they are set on (so they inherit its `BindingContext`
to): `Background` (Brush, default #F7FFFFFF), `Foreground` (Color), `TitleForeground` (Color, title only; per level
falls back to that level's `Foreground`), `Opacity` (1), `OffsetY` (0). Each
property resolves independently page → area → scaffold → defaults. Bind or animate `Opacity`/`OffsetY`
(hide-on-scroll) or a `SolidColorBrush.Color` inside `Background`; changes apply per frame.

Custom nav bar (`nalu:Scaffold.NavBarTemplate` on page/area/scaffold): `BindingContext` =
`ScaffoldNavBarContext`; drop in the primitives freely (they bind the inherited context). Read
page-specific state through `PageBindingContext.X` (reflection binding) or bind `TitleView`.
`PageBindingContext` is what the title slot hands to `TitleView` content.

`ScaffoldNavBarContext` members: `Title`, `TitleView`, `CurrentPage`, `PageBindingContext`,
`Foreground`, `TitleForeground`, `ScrollOffset`, `IsScrolledUnder`, `ScrollRampStart`, `ScrollRampEnd`, `CanNavigateBack`,
`IsFlyoutStartButtonVisible`, `IsFlyoutEndButtonVisible`, `IsModal`, `IsCloseButtonVisible`,
`BackCommand` (pop through the engine; disabled while a pop is in flight), `OpenFlyoutStartCommand`,
`OpenFlyoutEndCommand`.

`{nalu:NavBarBinding Path=…, Mode, Converter, ConverterParameter, StringFormat}` binds against the
context from anywhere in the scaffold tree. Code: `NavBarBindings.Create(target, path, mode, converter,
converterParameter, stringFormat)` (string path, "." = the context) or typed
`label.SetBinding(Label.TextProperty, NavBarBindings.Create(label, "ScrollOffset"))`
(trimming/AOT-safe).

## 6. Scroll channel

Moved to skill `nalu-scaffold-scroll` (tracker, ramps, `ScrollValue`/`ThemeScrollValue`, parallax, materializing bar).

## 7. Safe areas & system bars

- Insets reaching a page: system insets + nav bar footprint (top, unless `NavBarOverlapsContent`) + tab
  bar footprint (bottom, while visible). Bar visibility changes arrive as animated inset changes.
- Standard MAUI `SafeAreaEdges` per edge decides consumption. Template pages: page-level
  `SafeAreaEdges="None"` so the inner `ScrollView` pads its content (scrolls under the floating tab bar,
  rests clear of it) — needs the Android `SetClipToPadding(false)` mapper in `MauiProgram.cs`
  (upstream MAUI issue; keep it until fixed).
- Full-bleed page: `nalu:Scaffold.NavBarOverlapsContent="True"` + transparent page-level appearance
  `Background` + `ScrollView SafeAreaEdges="None,None,None,Default"` (top under the bar, bottom still
  clears the system bar).
- Popups/bottom sheets ignore the tab bar footprint (system insets only) → skill `nalu-scaffold-overlays`.
- Keyboard becomes a bottom inset by default (`KeyboardMode=Resize`) → skill `nalu-scaffold-keyboard`.

`nalu:Scaffold.SystemBarStyle` `Auto` resolution order: open flyout surface → nav bar (visible and opaque
enough, by luminance) → your declaration (page → area → scaffold) → pixel sample of the rendered strip
under the status bar (taken when presentation settles, debounced) → page background (or its top-spanning
first child's) → app theme. `LightContent` = white icons (dark surface); `DarkContent` = black icons.
Also fixes Android stale status-bar icon / `navigationBarColor` after system theme toggles.

Platform: iOS via `preferredStatusBarStyle` (UIKit cross-fades; no Info.plist changes); Android via
`WindowInsetsControllerCompat` for status + navigation bar (pixel sampler uses `PixelCopy`, API 26+).
