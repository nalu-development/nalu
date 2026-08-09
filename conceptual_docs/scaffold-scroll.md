# Scaffold Scroll-Driven Effects

The Scaffold has a built-in **scroll channel**: one page-attached property connects a
scrollable to the scaffold, and from that moment any numeric, `Color`, or solid-`Brush`
property — on chrome *or* page content — can be driven from the live scroll offset with
plain markup. Materializing nav bars, fading titles, and parallax headers all ride the same
three concepts: a **tracker**, a **ramp**, and the **`ScrollValue`** extensions.

<img src="assets/images/scaffold-scroll-chrome.gif" width="340" alt="Nav bar materializing and title fading in as the page scrolls, photo parallaxing behind" />

*The Daily Helper detail page: transparent bar over the parallaxing photo; scrolling
materializes the themed bar, fades the title in — and the status-bar icons flip to match.*

## 1. The tracker

```xml
<ContentPage nalu:Scaffold.ScrollTracker="{x:Reference DetailScroll}">
    <ScrollView x:Name="DetailScroll">...</ScrollView>
</ContentPage>
```

`Scaffold.ScrollTracker` points at the page's scrollable — a `ScrollView`, `CollectionView`,
`VirtualScroll`, or any view whose platform tree contains a native scroll container. The
scaffold observes it natively (KVO on iOS, scroll listeners on Android) and publishes the
vertical offset into the ambient `ScaffoldNavBarContext`:

| Context property | Meaning |
|------------------|---------|
| `ScrollOffset` | The live offset in device-independent units. |
| `IsScrolledUnder` | Whether content has scrolled under the nav bar (threshold-style checks). |

Each page starts its own scroll story: navigating away and back rebinds the channel, and a
page without a tracker reads offset 0 — bindings settle at their `From` endpoints.

Two practical caveats: the platform scroll container is searched at most 3 levels below the
tracked view, and on Android a recycler-backed tracker (`CollectionView`, `VirtualScroll`)
has no absolute offset — the value accumulates scroll deltas, so with variable item heights
thresholds stay reliable but exact pixel mapping (tight parallax) may drift.

## 2. The ramp

Effects interpolate inside a **window** of scroll offsets. Declare the page-wide default once:

```xml
<ContentPage nalu:Scaffold.ScrollRampStart="100"
             nalu:Scaffold.ScrollRampEnd="200">
```

Every `ScrollValue` on the page rides this ramp unless it overrides it with its own
`RampStart=`/`RampEnd=` — which is exactly what parallax does (see below).

## 3. `ScrollValue` and `ThemeScrollValue`

```xml
Opacity="{nalu:ScrollValue From=0, To=1}"
TextColor="{nalu:ScrollValue From=White, To=Black, Easing={x:Static Easing.CubicOut}}"
Background="{nalu:ThemeScrollValue FromLight=Transparent,
                                   ToLight={StaticResource BackgroundLight},
                                   ToDark={StaticResource BackgroundDark}}"
```

| Parameter | Purpose |
|-----------|---------|
| `From` / `To` | Endpoint values (numeric, `Color`, or a solid `Brush`). |
| `FromLight/ToLight/FromDark/ToDark` | Theme-aware endpoints (`ThemeScrollValue`); dark values fall back to the light ones, and a theme change re-evaluates immediately. |
| `RampStart` / `RampEnd` | Per-value ramp override (defaults to the page-level ramp). |
| `Extrapolate` | `Clamp` (default: hold endpoints outside the window) or `Extend` (keep going linearly). |
| `Easing` | Shapes the ramp interior. |

They work on any element inside the scaffold's tree **and** on
[`ScaffoldNavBarAppearance`](scaffold-navbar.md) properties, and must target a bindable
property directly (styles/setters are not supported). In code-behind, bind the ambient values
with the `NavBarBindings` utility — string path or fully typed:

```csharp
offsetLabel.SetBinding(Label.TextProperty, NavBarBindings.Create("ScrollOffset", stringFormat: "{0:F0}"));

// Typed and compiled (trimming/AOT-safe):
offsetLabel.SetBinding(Label.TextProperty,
    static (Scaffold s) => s.NavBarContext.ScrollOffset,
    source: NavBarBindings.ScaffoldAncestor);
```

## Recipe: parallax header

`Extrapolate=Extend` turns a range mapping into a **speed factor**: mapping scroll `0→100`
onto translation `0→50` moves the target at *half scroll speed, forever* — which is parallax.
The Daily Helper hero combines that with an off-screen bleed so the slower-moving photo never
reveals a gap:

```xml
<Grid HeightRequest="360">
    <!-- Photo + darkening scrims parallax TOGETHER at half speed. The 120dp negative top
         margin bleeds the backdrop above the hero, so the iOS top bounce (and the upward
         parallax drift) reveals more photo instead of a hole. -->
    <Grid Margin="0,-120,0,0"
          TranslationY="{nalu:ScrollValue RampStart=0, RampEnd=100, From=0, To=50, Extrapolate=Extend}">
        <Image nalu:Scaffold.TransitionName="weather-photo" Source="{Binding Current.Photo}" Aspect="AspectFill" />
        <BoxView Color="Black" Opacity="0.32" InputTransparent="True" />
    </Grid>

    <!-- Foreground content scrolls at normal speed over the slower backdrop. -->
    <VerticalStackLayout VerticalOptions="End">...</VerticalStackLayout>
</Grid>
```

Notes:

- The parallax value declares its **own ramp** (`RampStart=0, RampEnd=100`) so it is a pure
  speed ratio (`To/RampEnd` = 0.5), independent of the page-level ramp that drives the bar
  materialization on the same page.
- `From=0, To=100, Extrapolate=Extend` over ramp `0→100` would pin the backdrop to the
  viewport (speed 1); smaller `To` values slow it down; `To=0` fixes it in place.
- Multiple layers with different ratios produce depth stacks — each layer is just another
  `TranslationY="{nalu:ScrollValue ...}"`.

## Recipe: materializing nav bar + fading title

The full-bleed header pattern, combining the channel with
[`NavBarOverlapsContent`](scaffold-navbar.md):

```xml
<ContentPage nalu:Scaffold.NavBarOverlapsContent="True"
             nalu:Scaffold.ScrollTracker="{x:Reference DetailScroll}"
             nalu:Scaffold.ScrollRampStart="100"
             nalu:Scaffold.ScrollRampEnd="200">

    <nalu:Scaffold.NavBarAppearance>
        <nalu:ScaffoldNavBarAppearance
            Background="{nalu:ThemeScrollValue FromLight=Transparent,
                                               ToLight={StaticResource BackgroundLight},
                                               ToDark={StaticResource BackgroundDark}}"
            Foreground="{nalu:ThemeScrollValue FromLight=White,
                                               ToLight={StaticResource TextPrimaryLight},
                                               ToDark={StaticResource TextPrimaryDark}}" />
    </nalu:Scaffold.NavBarAppearance>

    <nalu:Scaffold.TitleView>
        <Label Text="Weather" Opacity="{nalu:ScrollValue From=0, To=1}" />
    </nalu:Scaffold.TitleView>

    <ScrollView x:Name="DetailScroll" SafeAreaEdges="None,None,None,Default">...</ScrollView>
</ContentPage>
```

As the bar becomes opaque, the [system-bar icons flip automatically](scaffold-systembars.md)
to contrast with it — the scroll channel, the appearance chain and the system bars are one
coordinated pipeline.

## Performance

Values apply per scroll frame through plain bindings onto native-backed properties — the same
cost as any MAUI property change, with no layout passes for transform/opacity/color targets.
Prefer `TranslationY`/`Opacity`/colors over size-affecting properties inside scroll effects.
