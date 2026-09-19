# Scaffold Nav Bar

The nav bar is a plain MAUI view strip: same layout, same styling, same behavior on iOS and
Android. It belongs to the **page**, not to the scaffold: every page realizes its own bar and
that bar travels with it through every motion the scaffold performs — push and pop slides,
custom transition specs, shared elements, the interactive edge pop, Android predictive back.
During a transition two bars are on screen, each showing its own page's state.

The default bar is mounted out of the box. Set `Scaffold.NavBarTemplate` only to replace it
with a custom bar, or clear it with `{x:Null}` on the scaffold to remove the bar entirely:

```xml
<nalu:Scaffold.NavBarTemplate>
    <DataTemplate>
        <nalu:ScaffoldNavBarView />
    </DataTemplate>
</nalu:Scaffold.NavBarTemplate>
```

It is a **template**, not a view, precisely because the bar belongs to the page: a single view
instance cannot be in two places at once, and MAUI would re-parent it to whichever page mounted
it last, blanking the other page's bar. Resolution is unchanged — page → current area →
scaffold, most specific wins.

The default bar composes three columns: leading (start-drawer button, back button), center
(title, or the page's `TitleView` in its place), trailing (the page's [toolbar items](#toolbar-items),
end-drawer button, close button). All primitives are public, individually styleable types:
`ScaffoldBackButton`, `ScaffoldCloseButton`, `ScaffoldFlyoutButton`, `ScaffoldNavBarTitle`,
`ScaffoldToolbarItemsView` (with `ScaffoldToolbarItemButton`, `ScaffoldToolbarOverflowButton`
and `ScaffoldToolbarOverflowView`) — plus the glyph buttons' shared base
`ScaffoldNavBarButtonBase` (`Icon`, `IconColor`, `PressedBrush`): style it with
`ApplyToDerivedTypes="True"` to theme every glyph button at once, the overflow one included.
Default metrics: `BarHeight` 48, `BarPadding` 8,0, `Spacing` 8; when [toolbar items](#toolbar-items)
compete for the row the title is kept whole (`KeepTitleWhole`) above a `MinimumTitleWidth` of 96.

## Titles and TitleView

The bar title comes from the page's `Title`. For arbitrary content, attach a `TitleView` to
the page:

```xml
<nalu:Scaffold.TitleView>
    <Label Text="Weather" FontSize="20" VerticalTextAlignment="Center" />
</nalu:Scaffold.TitleView>
```

The `TitleView`'s `BindingContext` is the **page model** — bind your own state directly. To
read nav-bar state (foreground color, scroll offset) use the `NavBarBinding` markup extension.
It binds against the `ScaffoldNavBarContext` of the page the target element belongs to — page
content through its page, bar content (a hosted title view included) through the bar it is
mounted in — so during a transition each of the two live pages reads its OWN state:

```xml
<Label Text="{nalu:NavBarBinding Path=Title}"
       TextColor="{nalu:NavBarBinding Path=Foreground}" />
```

In code-behind, the `NavBarBindings` utility is the counterpart. Pass the element the binding
is applied to — it is what the page is resolved from:
`label.SetBinding(Label.TextProperty, NavBarBindings.Create(label, "Title"))`.

Paths naming a `ScaffoldNavBarContext` property compile to a typed binding (no reflection, so
they survive trimming); deeper paths such as `PageBindingContext.SaveCommand` are
evaluated by reflection. `{nalu:NavBarBinding}` is not supported inside a `Style` setter: one
binding instance serves every styled element, so there is no single target to resolve a page
from.

`Scaffold.NavBarContext` remains available and means "what the bar shows now" — the CURRENT
page's context. Prefer the per-page resolution above; the scaffold-level one is only correct
while a single page is on screen.

## Toolbar items

The default bar renders the page's standard MAUI `ToolbarItems` — nothing to migrate, nothing
to bind:

```xml
<ContentPage Title="Weather">
    <ContentPage.ToolbarItems>
        <ToolbarItem Text="Save" Command="{Binding SaveCommand}" />
        <ToolbarItem Text="Share" Priority="1"
                     IconImageSource="{FontImageSource FontFamily=Material, Glyph=&#xe80d;}" />
        <ToolbarItem Text="Settings" Order="Secondary" Clicked="OnSettingsClicked" />
    </ContentPage.ToolbarItems>
</ContentPage>
```

The semantics are the native toolbar's, identical on iOS and Android:

- **Primary** items (`Order` `Primary` or `Default`) are buttons in the trailing slot, before the
  end-drawer and close buttons, in ascending `Priority` (declaration order breaks ties). An item
  with an `IconImageSource` shows the icon — 24dp, centered in a 44dp tap target — and its `Text`
  becomes the accessibility description; without one it shows the text.
- **As many as fit — the title first.** The bar measures its fixed parts first, reserves the
  title's own natural width (never less than `MinimumTitleWidth`, 96 by default), and offers
  the items what is left: items are taken in priority order while they fit, and the first one
  that doesn't stops the intake — the rest fold into the overflow menu, listed ahead of the
  secondary items. Items fold; the title never truncates because of them. This is Android's
  "if room" behavior with the title on top; MAUI's native bars show every item and let a long
  list eat the title. Prefer that trade-off on a page (an editor whose "Save" must always
  show)? Set `KeepTitleWhole="False"` on the bar — via a style, or a page-level
  `NavBarTemplate` — and the reservation drops to the floor (lower `MinimumTitleWidth` too for
  a bar that favors items outright). Rotation and runtime changes re-fit. A `TitleView` is
  treated the same way, by its natural width: a label or an icon-plus-text block reserves what
  it needs, while a fill-style control (a search bar, an `Entry`) has a small intrinsic width,
  reserves only that or the floor, and stretches into what the items leave — give it a
  `WidthRequest` or raise `MinimumTitleWidth` when it needs room.
- **Secondary** items live behind a ⋮ overflow button that only appears while some exist (or
  something folded). The menu drops down from the button, end-aligned so it opens inward, lists
  the items by priority (icon when set, then text), and a row dismisses it first, then activates
  the item. The scrim and the Android back close it; a navigation closes it like any overlay.
- A tap runs the item's `Command` (with `CommandParameter`) and raises `Clicked`, as the native
  bars do. `IsEnabled="False"` — or a command whose `CanExecute` is false — dims the item and
  makes it inert.
- The collection is **live**: items added, removed, reordered, re-prioritized or moved between
  surfaces after the page is shown are reflected at once (MAUI's native toolbars warn that such
  changes are ignored).
- The items inherit the page's `BindingContext`, as on every MAUI page — `Command="{Binding …}"`
  reaches the page model.
- An item's `AutomationId` lands on the button (or menu row) rendered for it.

Icons follow the bar's foreground the way native template glyphs do: a `FontImageSource` declared
**without** a `Color` is tinted with the effective `NavBarForeground` (see the merge chain below),
so it recolors with the rest of the bar — over a photo header, through a scroll-driven ramp,
across themes. A font icon with an explicit `Color`, or any bitmap, renders exactly as given.

Styling is ordinary MAUI, on public types:

| Type | Knobs |
|------|-------|
| `ScaffoldToolbarItemButton` | `TextColor` (also the tint of uncolored font icons), `FontFamily`, `FontSize`, `FontAttributes`, `PressedBrush`. |
| `ScaffoldToolbarOverflowButton` | A glyph button: `Icon` (replaces the ⋮), `IconColor`, `PressedBrush` — and it picks up a `ScaffoldNavBarButtonBase` style with `ApplyToDerivedTypes`. |
| `ScaffoldToolbarOverflowView` | `PanelBackground` and `TextColor` — **theme-following when unset** (light: white / near-black, dark: dark gray / near-white), a style pins them — `PanelCornerRadius`, `PanelShadow`, `Scrim` (transparent by default), `FontFamily`, `FontSize`. The panel hugs its widest row between `MinimumWidthRequest` (200) and `MaximumWidthRequest` (280) — both plain properties, style them to change the bounds. |

Color precedence on the bar items is the same as for every other primitive: an explicit or styled
`TextColor` / `IconColor` wins, then the `NavBarForeground` chain, then the built-in default — so
prefer the appearance channels, which keep the items following page-level appearances. The
overflow menu is a floating panel, not bar chrome: it deliberately does NOT inherit the bar's
foreground (a white-on-photo bar would give white text on a white menu) and follows the app
theme instead unless styled.

## Appearance — a per-property merge chain

Five attached properties style the bar *surface* — never the mounted bar view's own properties.
Set them on a page, an area or the scaffold; **each resolves independently** through page → area
→ scaffold, so a page-level value is a delta, not a replacement:

```xml
<!-- Scaffold-wide surface, typically in your Styles.xaml.
     ApplyToDerivedTypes is REQUIRED: your AppScaffold derives from Scaffold, and MAUI matches
     implicit styles on the exact type — without it this style never reaches your scaffold. -->
<Style TargetType="nalu:Scaffold" ApplyToDerivedTypes="True">
    <Setter Property="nalu:Scaffold.NavBarForeground" Value="{StaticResource Accent}" />
    <Setter Property="nalu:Scaffold.NavBarTitleForeground"
            Value="{AppThemeBinding Light={StaticResource TextPrimaryLight},
                                    Dark={StaticResource TextPrimaryDark}}" />
    <Setter Property="nalu:Scaffold.NavBarBackground">
        <Setter.Value>
            <SolidColorBrush Color="{AppThemeBinding Light={StaticResource BackgroundLight},
                                                     Dark={StaticResource BackgroundDark}}" />
        </Setter.Value>
    </Setter>
</Style>
```

They are attached properties on real elements, so they bind, resolve `StaticResource` and
`AppThemeBinding`, and animate from scroll with no machinery of their own — and a `Style` setter
gives every element its own value rather than sharing one object.

> [!WARNING]
> An implicit style targeting `nalu:Scaffold` needs `ApplyToDerivedTypes="True"`. A XAML
> `AppScaffold` *derives* from `Scaffold`, and MAUI matches implicit styles on the exact type, so
> without it the style is silently skipped and the bar keeps the built-in defaults — including
> through theme changes, which is what the symptom usually looks like: page content follows the
> theme (its `ContentPage` style has `ApplyToDerivedTypes`), the bar never does.

| Property | Effect |
|----------|--------|
| `NavBarBackground` | The strip surface brush. |
| `NavBarForeground` | Flows to the default primitives (chevron, flyout/close icons — and the title unless `NavBarTitleForeground` is set) via the context. |
| `NavBarTitleForeground` | Title-only color (`ScaffoldNavBarTitle`). Resolved level by level with `NavBarForeground`: the first level (page → area → scaffold) that sets either wins, its title color first. So the scaffold can give buttons and title different colors, and a page still recolors the whole bar with `NavBarForeground` alone. |
| `NavBarOpacity` | Whole-surface opacity. |
| `OffsetY` | Vertical offset (hide-on-scroll effects). |

`{AppThemeBinding}` / `{DynamicResource}` on these properties — and inside the `Background` brush —
stay live: the appearance is an `Element` parented to the element it is attached to (its brush to the
appearance), so app-theme and resource changes reach it even while another page's appearance is the
presented one.

Color precedence on a primitive: an explicit (or styled) `TextColor` / `IconColor` on the
primitive itself → the appearance chain (title: level-wise `TitleForeground` ?? `Foreground`;
buttons: `Foreground`) → the built-in default. Prefer the appearance channels over styling
`TextColor`/`IconColor`: a styled color is pinned and no longer follows page-level appearances
(photo headers, scroll-driven recolor).

Appearance objects are live: mutating a property (or animating it via bindings) applies
immediately, per frame. Careful with shared `Style`s: an appearance declared in a style is ONE
object attached to many elements — fine for constants, broken for per-page bindings. Defaults:
`Background` #F7FFFFFF, `Opacity` 1, `OffsetY` 0.

## Per-page visibility & overlap

```xml
<ContentPage nalu:Scaffold.IsNavBarVisible="False" />         <!-- no bar on this page -->
<ContentPage nalu:Scaffold.NavBarOverlapsContent="True" />    <!-- bar draws OVER content -->
```

`NavBarOverlapsContent` removes the bar's top inset from the page — content starts at the
very top edge and the bar floats above it: the full-bleed header recipe. Pair it with a
transparent page-level `Background` and a `ScrollView` whose safe areas are tuned per edge
(`SafeAreaEdges="None,None,None,Default"`: full-bleed top, but scrolled content still clears
the bottom system bar).

## Scroll-driven chrome

The scaffold has a built-in scroll channel: point `Scaffold.ScrollTracker` at the page's
scrollable, declare an interpolation ramp, and drive any numeric/Color/Brush property from the
live scroll offset with the `ScrollValue` / `ThemeScrollValue` markup extensions:

```xml
<ContentPage nalu:Scaffold.NavBarOverlapsContent="True"
             nalu:Scaffold.ScrollTracker="{x:Reference DetailScroll}"
             nalu:Scaffold.ScrollRampStart="100"
             nalu:Scaffold.ScrollRampEnd="200">

    <!-- The bar materializes as you scroll: transparent over the header photo,
         then the themed background, theme-aware on both ends. Declared on the page itself. -->

    <!-- The title fades in on the same page-level ramp. -->
    <nalu:Scaffold.TitleView>
        <Label Text="Weather" Opacity="{nalu:ScrollValue From=0, To=1}" />
    </nalu:Scaffold.TitleView>

    <ScrollView x:Name="DetailScroll">...</ScrollView>
</ContentPage>
```

This is one instance of the scaffold's general **scroll channel** — the same machinery drives
parallax headers, fading titles and any other scroll-bound property on chrome or page content.
Full API (tracker, ramps, `Extrapolate` semantics) and the parallax recipe:
**[Scroll-Driven Effects](scaffold-scroll.md)**.

The [system bar icons follow automatically](scaffold-systembars.md): when the materializing
bar becomes opaque, status-bar icons flip to contrast with it.

## Custom nav bars

Replace the bar per scaffold, area, or page with `Scaffold.NavBarTemplate`. Custom bars bind the
`ScaffoldNavBarContext` — `Title`, `Foreground`, `TitleForeground`, `CanNavigateBack`, `BackCommand`,
`ScrollOffset`, `IsScrolledUnder`, `IsModal`/`IsCloseButtonVisible`, flyout-button visibility
and commands, `ToolbarItems` (the page's live collection) — and can reuse the public primitives:
drop a `ScaffoldToolbarItemsView` anywhere in a custom bar to get the whole toolbar-items
behavior, or render the collection your own way. The bar view owns its top safe-area
behavior (the default bar consumes the status-bar inset itself).

### iPadOS 26: the system window controls

On iPadOS 26 every app is a resizable window, and while it IS a window the system draws its
window controls — the "traffic lights" — over the window's top-leading corner, on top of whatever
the app draws there. That corner belongs to the nav bar's leading buttons, and to the first entry
of a start-edge drawer.

The scaffold clears them for you, through the platform's own safe-area mechanism and per surface:

| surface | inset | why that edge |
|---|---|---|
| nav bar strip | **leading** | the bar is a band across the top; pushing its content right is all it needs |
| left-edge drawer | **top** | a drawer runs the full height — insetting its leading edge would waste the panel's width all the way down for controls that only cover its corner |

Each surface is its own view controller, so its inset reaches that surface alone: the page under a
bar does not move, and an end-side drawer (which never reaches the controls) gets nothing. Content
picks it up with no code as long as it consumes the container safe area — which the default bar,
`ScaffoldFlyoutMenuView`, and any bar that already handles the status bar all do:

```xml
<Grid SafeAreaEdges="Container">   <!-- clears the status bar AND the window controls -->
```

Content that deliberately opts OUT of safe areas to draw edge-to-edge stays under the controls —
exactly as it already stays under the status bar.

Two things worth knowing about the geometry:

- **Apps that opt out of the iOS 26 design get no inset.** `UIDesignRequiresCompatibility` in the
  Info.plist puts the app in the compatibility window chrome, where the system reserves a band at
  the top of the window (safe area top 32 instead of 10, measured on iPadOS 26.2 and 26.5) and
  draws the controls INSIDE it — they never reach the app's content, so insetting would only open
  a gap.
- **Full-screen windows get no inset.** There the controls are transient — they appear near the
  corner and hide again — and holding the band open permanently for something usually absent would
  cost every full-screen iPad app its leading space.
- **The footprint is a measured constant** (the controls occupy x 21..62, y 43..65 in window
  coordinates), because no API reports it: UIKit publishes no inset for them — a windowed scene
  reports a plain `L0 T32 R0 B20` while they are on screen, and they sit BELOW that top inset —
  they are hosted outside the app's window, so the app cannot find them in its own view tree, and
  iOS 26's `UISceneWindowingControlStyle` selects a style, never a frame. What each surface applies
  is the REMAINING distance: a drawer already inset 32pt by the status bar adds only the rest.

### Touches: what you draw is what you take

A bar lives in a strip that spans the FULL width, whatever the bar itself paints. Everything in
that strip you do not draw belongs to the page underneath, and must let touches through — a
floating pill with empty margins, a bar moved out of the band by `NavBarOffsetY`, the space beside
a short title. A strip that swallows those is an invisible dead zone, and nothing on screen
explains it to the person tapping.

Two rules keep a custom bar honest, and they mirror how the built-in chrome is built:

- **Whatever you DRAW should take its own touches.** A MAUI view carrying a gesture recognizer
  consumes them on both platforms, and the behavior travels with the view — a bar translated out of
  the way stops claiming the space it no longer occupies, with no platform code deciding where the
  bar "really" is. The default bar's surface carries an empty `TapGestureRecognizer` for exactly
  this: without it, a tap on a visible bar reaches the page behind it, which with
  `NavBarOverlapsContent` means operating content the user cannot see.
- **Whatever merely POSITIONS should be `InputTransparent`,** with `CascadeInputTransparent="False"`
  so your content keeps its own touches. Layouts that fill the strip to centre something — the tab
  bar's own grid is the clearest case — otherwise take every touch inside their bounds on iOS,
  padding included, while on Android they let everything fall through. Declaring it makes both
  platforms behave the same way.

```xml
<!-- a custom bar: the painted surface absorbs, the wrapper does not -->
<Grid InputTransparent="True" CascadeInputTransparent="False">
    <Border BackgroundColor="{StaticResource BarSurface}">
        <Border.GestureRecognizers>
            <TapGestureRecognizer />
        </Border.GestureRecognizers>
        <!-- title, buttons… -->
    </Border>
</Grid>
```
