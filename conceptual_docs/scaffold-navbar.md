# Scaffold Nav Bar

The nav bar is a plain MAUI view strip: same layout, same styling, same behavior on iOS and
Android. The default bar is mounted out of the box — set `Scaffold.NavBarView` only to
restyle-by-instance, replace it with a custom view, or remove it with `{x:Null}`.

The default bar composes three columns: leading (start-drawer button, back button), center
(title, or the page's `TitleView` in its place), trailing (end-drawer button, close button).
All primitives are public, individually styleable types: `ScaffoldBackButton`,
`ScaffoldCloseButton`, `ScaffoldFlyoutButton`, `ScaffoldNavBarTitle` — plus their shared base
`ScaffoldNavBarButtonBase` (`Icon`, `IconColor`, `PressedBrush`): style it with
`ApplyToDerivedTypes="True"` to theme all three buttons at once. Default metrics:
`BarHeight` 48, `BarPadding` 8,0, `Spacing` 8.

## Titles and TitleView

The bar title comes from the page's `Title`. For arbitrary content, attach a `TitleView` to
the page:

```xml
<nalu:Scaffold.TitleView>
    <Label Text="Weather" FontSize="20" VerticalTextAlignment="Center" />
</nalu:Scaffold.TitleView>
```

The `TitleView`'s `BindingContext` is the **page model** — bind your own state directly. To
read nav-bar ambient state (foreground color, scroll offset) use the `NavBarBinding` markup
extension, which binds against the scaffold's `ScaffoldNavBarContext`:

```xml
<Label Text="{nalu:NavBarBinding Path=Title}"
       TextColor="{nalu:NavBarBinding Path=Foreground}" />
```

In code-behind, the `NavBarBindings` utility is the counterpart. Pass the element the binding
is applied to — it is what the page is resolved from:
`label.SetBinding(Label.TextProperty, NavBarBindings.Create(label, "Title"))`.

## Appearance — a per-property merge chain

`ScaffoldNavBarAppearance` styles the bar *surface* (never the mounted bar view's own
properties). Attach it at scaffold, area, or page level; **each property resolves
independently** through page → area → scaffold — a page-level appearance is a delta, not a
replacement:

```xml
<!-- Scaffold-wide surface -->
<nalu:Scaffold.NavBarAppearance>
    <nalu:ScaffoldNavBarAppearance Foreground="{StaticResource Accent}"
                                   TitleForeground="{AppThemeBinding Light={StaticResource TextPrimaryLight},
                                                                     Dark={StaticResource TextPrimaryDark}}">
        <nalu:ScaffoldNavBarAppearance.Background>
            <SolidColorBrush Color="{AppThemeBinding Light={StaticResource BackgroundLight},
                                                     Dark={StaticResource BackgroundDark}}" />
        </nalu:ScaffoldNavBarAppearance.Background>
    </nalu:ScaffoldNavBarAppearance>
</nalu:Scaffold.NavBarAppearance>
```

| Property | Effect |
|----------|--------|
| `Background` | The strip surface brush. |
| `Foreground` | Flows to the default primitives (chevron, flyout/close icons — and the title unless `TitleForeground` is set) via the context. |
| `TitleForeground` | Title-only color (`ScaffoldNavBarTitle`). Resolved level by level with `Foreground`: the first appearance (page → area → scaffold) that sets either wins, its `TitleForeground` beating its `Foreground`. So the scaffold can give buttons and title different colors, and a page still recolors the whole bar with `Foreground` alone (or the two apart by setting both). |
| `Opacity` | Whole-surface opacity. |
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
         then the themed background, theme-aware on both ends. -->
    <nalu:Scaffold.NavBarAppearance>
        <nalu:ScaffoldNavBarAppearance
            Background="{nalu:ThemeScrollValue FromLight=Transparent,
                                               ToLight={StaticResource BackgroundLight},
                                               ToDark={StaticResource BackgroundDark}}"
            Foreground="{nalu:ThemeScrollValue FromLight=White,
                                               ToLight={StaticResource TextPrimaryLight},
                                               ToDark={StaticResource TextPrimaryDark}}" />
    </nalu:Scaffold.NavBarAppearance>

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

Replace the bar per scaffold, area, or page with `Scaffold.NavBarView`. Custom bars bind the
`ScaffoldNavBarContext` — `Title`, `Foreground`, `TitleForeground`, `CanNavigateBack`, `BackCommand`,
`ScrollOffset`, `IsScrolledUnder`, `IsModal`/`IsCloseButtonVisible`, flyout-button visibility
and commands — and can reuse the public primitives. The bar view owns its top safe-area
behavior (the default bar consumes the status-bar inset itself).
