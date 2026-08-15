---
name: nalu-scaffold-scroll
description: Nalu.Maui.Scaffold scroll-driven effects — Scaffold.ScrollTracker + ramp, {nalu:ScrollValue}/{nalu:ThemeScrollValue} bindings for parallax headers, materializing nav bars, fading titles; load when a value must follow the page scroll offset.
---
# Scaffold scroll-driven effects (ScrollValue / parallax)

Package `Nalu.Maui.Scaffold`, namespace `Nalu`, XAML `xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"`.
Mental model — three parts: a **tracker** (page-attached property naming the page's scrollable) publishes
the live vertical offset into the ambient `ScaffoldNavBarContext`; a **ramp** (`ScrollRampStart`→`ScrollRampEnd`,
page-wide default) is the offset window over which effects interpolate; the **`{nalu:ScrollValue}`** /
**`{nalu:ThemeScrollValue}`** markup extensions bind any numeric, `Color` or solid-`Brush` bindable property —
on page content OR nav bar chrome — to that offset. No code, no scroll handlers. Nav bar / appearance
properties themselves → skill `nalu-scaffold-structure`.

## Quick reference

| API | Purpose | Notes |
|-----|---------|-------|
| `nalu:Scaffold.ScrollTracker="{x:Reference X}"` (page) | Connect the page's scrollable | `ScrollView`, `CollectionView`, `VirtualScroll`, or any view whose platform tree has a native scroll container ≤ 3 levels deep. One per page. |
| `nalu:Scaffold.ScrollRampStart` / `ScrollRampEnd` (page → area → scaffold) | Page-wide ramp, default 0 / 100 | Every `ScrollValue` without its own `RampStart/RampEnd` rides it. |
| `{nalu:ScrollValue From, To, RampStart?, RampEnd?, Extrapolate?, Easing?}` | Offset → value | `From`/`To`: numeric, `Color`, or solid `Brush` (types must match the target). |
| `{nalu:ThemeScrollValue FromLight, ToLight, FromDark?, ToDark?, RampStart?, RampEnd?, Extrapolate?, Easing?}` | Theme-aware endpoints | Dark values fall back to the light ones; theme change re-evaluates immediately. |
| `Extrapolate` (`ScrollValueExtrapolation`) | `Clamp` (default: hold endpoints outside the ramp) / `Extend` (continue linearly) | `Extend` on numeric targets only; colors/brushes always clamp. |
| `Easing` | Shapes the ramp interior | `Easing="{x:Static Easing.CubicOut}"`. |
| `ScaffoldNavBarContext.ScrollOffset` / `IsScrolledUnder` | Raw channel values | `{nalu:NavBarBinding Path=ScrollOffset}` in XAML; `IsScrolledUnder` for threshold checks (e.g. a divider). |
| `NavBarBindings.Create("ScrollOffset", stringFormat: …)` / `SetBinding(prop, static (Scaffold s) => s.NavBarContext.ScrollOffset, source: NavBarBindings.ScaffoldAncestor)` | Code-behind counterparts | Typed form is trim/AOT-safe. |

Math: `t = ease((offset − RampStart) / (RampEnd − RampStart))`, clamped to 0..1 unless `Extend`;
value = `From + (To − From) × t`. With `Extend`, `To/(RampEnd − RampStart)` is a **speed factor**;
`Easing` shapes only the interior (extrapolated values stay linear). `RampStart == RampEnd` = a step at that offset.

## Patterns

Materializing nav bar + fading title (page-wide ramp drives the chrome):

```xml
<ContentPage xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"
             nalu:Scaffold.ScrollTracker="{x:Reference Scroll}"
             nalu:Scaffold.ScrollRampStart="40" nalu:Scaffold.ScrollRampEnd="160" Title="Home">
    <nalu:Scaffold.NavBarAppearance>
        <nalu:ScaffoldNavBarAppearance
            Background="{nalu:ThemeScrollValue FromLight={StaticResource BackgroundLight}, ToLight={StaticResource CardLight},
                                               FromDark={StaticResource BackgroundDark}, ToDark={StaticResource CardDark}}" />
    </nalu:Scaffold.NavBarAppearance>
    <nalu:Scaffold.TitleView>
        <Label Text="Home" Opacity="{nalu:ScrollValue From=0, To=1}" />
    </nalu:Scaffold.TitleView>
    <ScrollView x:Name="Scroll">...</ScrollView>
</ContentPage>
```

Parallax header — the value declares its OWN ramp so it is a pure speed ratio (`To/RampEnd` = 0.5 →
half scroll speed, forever), independent of the page ramp above:

```xml
<Grid HeightRequest="360">
    <!-- backdrop moves at half speed; negative top margin bleeds it so the slower layer never shows a gap -->
    <Grid Margin="0,-120,0,0"
          TranslationY="{nalu:ScrollValue RampStart=0, RampEnd=100, From=0, To=50, Extrapolate=Extend}">
        <Image Source="hero.jpg" Aspect="AspectFill" />
        <BoxView Color="Black" Opacity="0.32" InputTransparent="True" />
    </Grid>
    <VerticalStackLayout VerticalOptions="End">...</VerticalStackLayout>   <!-- normal speed -->
</Grid>
```

Full-bleed photo header (transparent bar over the photo, materializes on scroll, icons/title recolor):

```xml
<ContentPage nalu:Scaffold.NavBarOverlapsContent="True"
             nalu:Scaffold.SystemBarStyle="LightContent"
             nalu:Scaffold.ScrollTracker="{x:Reference Scroll}"
             nalu:Scaffold.ScrollRampStart="100" nalu:Scaffold.ScrollRampEnd="200">
    <nalu:Scaffold.NavBarAppearance>
        <nalu:ScaffoldNavBarAppearance
            Background="{nalu:ThemeScrollValue FromLight=Transparent, ToLight={StaticResource BackgroundLight}, ToDark={StaticResource BackgroundDark}}"
            Foreground="{nalu:ThemeScrollValue FromLight=White, ToLight={StaticResource TextPrimaryLight}, ToDark={StaticResource TextPrimaryDark}}" />
    </nalu:Scaffold.NavBarAppearance>
    <ScrollView x:Name="Scroll" SafeAreaEdges="None,None,None,Default">
        <VerticalStackLayout>
            <!-- parallax hero as above -->
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

Fade-out + drift of a hero element in normal content (template `HomePage.xaml`):

```xml
<Image TranslationY="{nalu:ScrollValue RampStart=0, RampEnd=100, From=0, To=50, Extrapolate=Extend}"
       Opacity="{nalu:ScrollValue RampStart=0, RampEnd=220, From=1, To=0}" />
```

Threshold-style effects: `IsVisible="{nalu:NavBarBinding Path=IsScrolledUnder}"` on a divider under a
custom bar; or `Opacity="{nalu:ScrollValue RampStart=0, RampEnd=1, From=0, To=1}"` for a hard switch.

## Rules & gotchas

- The extensions must target a **bindable property directly** on an element — not inside a `Style`
  setter, not on plain CLR properties. Works on any element in the scaffold's tree (page content, `TitleView`,
  custom nav/tab bars) and on `ScaffoldNavBarAppearance` properties.
- Endpoint types must match the target: numeric ↔ `double`/`int` properties, `Color` ↔ `Color`,
  `Brush` ↔ `Brush` (solid only). `From=Transparent, To={StaticResource X}` on a `Brush` property works
  (color literals convert to solid brushes).
- No tracker on the page → offset is 0 → every value sits at `From` (chrome looks like the "top" state).
  Only ONE tracker per page; each page has its own channel — navigating away and back rebinds it.
- Nested/wrapped scrollables: the native scroll container is searched at most 3 levels below the tracked
  view; point the tracker at the scrollable itself when in doubt.
- Android recycler-backed trackers (`CollectionView`, `VirtualScroll`) accumulate deltas (no absolute
  offset): thresholds and materialization are fine; tight pixel-exact parallax may drift with variable
  item heights. Use a `ScrollView` for pixel-perfect parallax.
- Speed factors: with `RampStart=0, RampEnd=100, Extrapolate=Extend`, `To=100` pins the layer to the viewport
  (speed 1), `To=50` = half speed, `To=0` fixes it in place, `To` > 100 moves faster than the content. Stack
  several layers with different `To` for depth.
- Parallax layers moving slower than the content reveal a gap at the top on over-scroll/bounce — bleed the
  backdrop with a negative top `Margin` (≥ the max upward drift) as in the pattern.
- Prefer `TranslationY` / `Opacity` / colors as targets: they apply per scroll frame without layout passes.
  Avoid size-affecting properties (`HeightRequest`, `Margin`) — they re-layout every frame.
- Page-level ramp defaults (0/100) resolve page → area → scaffold; declare the page ramp once and let chrome
  values ride it; give parallax values their own ramp so both can coexist on one page.
- Materializing bar over a photo: also set `nalu:Scaffold.SystemBarStyle="LightContent"` for the white-chrome
  state; the scaffold flips status-bar icons automatically as the bar becomes opaque (→ `nalu-scaffold-structure`).
- Nothing to dispose: bindings live with the page. Do not add your own `Scrolled` handlers for the same job.

## See also

- `nalu-scaffold-structure` — `NavBarAppearance`, `NavBarOverlapsContent`, `TitleView`, `SystemBarStyle`, `NavBarBinding`.
- `nalu-scaffold-transitions` — `TransitionName` shared elements pair well with parallax hero images.
