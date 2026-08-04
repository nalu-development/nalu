# Scaffold Nav Bar

The nav bar is a Nalu-drawn strip: same layout, same styling, same behavior on iOS and
Android. Enable the default bar scaffold-wide:

```xml
<nalu:Scaffold nalu:Scaffold.NavBarView="{nalu:ScaffoldNavBarView}">
```

The default bar composes four slots: flyout button(s), back/close button, title, and the
page's `TitleView`. All primitives are public, individually styleable types:
`ScaffoldBackButton`, `ScaffoldCloseButton`, `ScaffoldFlyoutButton`, `ScaffoldNavBarTitle`.

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

## Appearance — a per-property merge chain

`ScaffoldNavBarAppearance` styles the bar *surface* (never the mounted bar view's own
properties). Attach it at scaffold, area, or page level; **each property resolves
independently** through page → area → scaffold — a page-level appearance is a delta, not a
replacement:

```xml
<!-- Scaffold-wide surface -->
<nalu:Scaffold.NavBarAppearance>
    <nalu:ScaffoldNavBarAppearance Foreground="{StaticResource Accent}">
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
| `Foreground` | Flows to the default primitives (title, chevron, flyout icon) via the context. |
| `Opacity` | Whole-surface opacity. |
| `OffsetY` | Vertical offset (hide-on-scroll effects). |

Appearance objects are live: mutating a property (or animating it via bindings) applies
immediately, per frame.

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
`ScaffoldNavBarContext` — `Title`, `Foreground`, `CanNavigateBack`, `BackCommand`,
`ScrollOffset`, `IsScrolledUnder`, `IsModal`/`IsCloseButtonVisible`, flyout-button visibility
and commands — and can reuse the public primitives. The bar view owns its top safe-area
behavior (the default bar consumes the status-bar inset itself).
