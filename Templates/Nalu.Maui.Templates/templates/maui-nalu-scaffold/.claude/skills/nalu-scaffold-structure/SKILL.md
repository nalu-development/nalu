---
name: nalu-scaffold-structure
description: Nalu.Maui.Scaffold app shell — Scaffold/areas/roots, tab bar (icons, badges, overflow, visibility), flyout drawers, nav bar (title/TitleView, appearance, custom bar), safe areas & system bars, scroll-driven chrome; load when editing AppScaffold.xaml or page chrome.
---
# Scaffold structure, tab bar, flyout, nav bar, system bars, scroll chrome

Package `Nalu.Maui.Scaffold`, namespace `Nalu`, XAML `xmlns:nalu="https://nalu-development.github.com/nalu/scaffold"`.
`AppScaffold.xaml` is a `nalu:Scaffold` (window page, singleton in `MauiProgram.cs` next to
`.UseNaluNavigation<App>(nav => nav.AddPages()).UseNaluScaffold()`).
Tree: `Scaffold` → `ScaffoldArea` / `ScaffoldTabBar` → `ScaffoldRoot` (one root = one navigation stack,
`PageType` = a registered page or page-model type). All chrome is MAUI views; every tab tap / drawer
selection / back routes through the Nalu navigation engine (guards + lifecycle run). Per-page knobs are
attached properties `nalu:Scaffold.*` on the `ContentPage`; most resolve page → area → scaffold.

## Quick reference

| API | Purpose | Notes |
|-----|---------|-------|
| `ScaffoldRoot` `PageType`, `Title`, `Icon`, `SelectedIcon`, `IsVisible` | A tab / stack root | `IsSelected`, `CurrentIcon` read-only; `SelectCommand` = engine-routed select (custom bars/menus). |
| `ScaffoldTabBar` (: `ScaffoldArea`) | Area rendered as bottom tab bar | `TabBarView` (default `ScaffoldTabBarView`), `SelectRootAsync(root)`, `ShowPanelAsync(view, scrim?, closeIfOpened)`. |
| `ScaffoldArea` | Roots without tab chrome | `Title` (flyout group header), `Roots`, `CurrentRoot` (ro). A bare `ScaffoldRoot` inside `Scaffold` = single-root area. |
| `Scaffold` `InitialRootPageType`, `InitialIntent` | Startup root (default: first root of first area) | Plain properties, not bindable. |
| `Scaffold` `CurrentArea`, `CurrentPage`, `Areas` | Read-only state | Observable; `Areas`/`Roots` mutable at runtime. |
| `nalu:Scaffold.TabBarVisibility` (page) | `Visible` (default) / `Auto` (hidden on pushed pages) / `Hidden` | Animated slide with the page transition. |
| `ScaffoldTabBarView` | Default pill bar; implicit-style it | `ItemWidth` 76, `OverflowIcon`/`OverflowTitle` ("More"), `BarBackground`, `BarCornerRadius`, `BarMargin`, `BarPadding`, `BarShadow`; attached `ScaffoldTabBarView.BadgeText` on a `ScaffoldRoot`. |
| `ScaffoldTabBarItemView`, `ScaffoldTabBarOverflowView` | Item / overflow-panel styling | See reference.md tables. |
| `nalu:Scaffold.FlyoutStart` / `FlyoutEnd` (View) + `FlyoutStartMode` / `FlyoutEndMode` | Drawers; page → area → scaffold | Mode `Disabled` (default) / `Auto` (roots only) / `Flyout`. Content alone does nothing. |
| `ScaffoldFlyoutMenuView` | Ready-made menu of areas/roots | Set `IsTabBarDisplayed="True"` to list tab-bar roots (else empty in a tab-only app). |
| `Scaffold.FlyoutStartOptions` / `FlyoutEndOptions` (`ScaffoldFlyoutOptions`) | `Width`, `WidthRatio` 0.85, `MaximumWidth` 360, `Scrim` | Scaffold-level only. |
| `IScaffoldFlyoutController` (DI, page-scoped) | `OpenAsync(ScaffoldFlyoutSide)`, `CloseAsync()` | Also `scaffold.OpenFlyoutAsync/CloseFlyoutAsync`, `IsFlyoutStartOpen/EndOpen`, `FlyoutStartOpened/Closed` events. |
| `nalu:Scaffold.FlyoutStartButtonVisibility` / `End…` | Nav-bar drawer button: `Auto` (roots) / `Visible` / `Hidden` | page → area → scaffold. |
| Page `Title` / `nalu:Scaffold.TitleView` | Nav bar center | `TitleView.BindingContext` = page model. |
| `nalu:Scaffold.NavBarView` | Bar view; page → area → scaffold | Default `ScaffoldNavBarView` (`BarHeight` 48, `BarPadding` 8,0, `Spacing` 8). `{x:Null}` = no bar. |
| `nalu:Scaffold.NavBarAppearance` (`ScaffoldNavBarAppearance`) | Strip surface: `Background`, `Foreground`, `Opacity`, `OffsetY` | Each property resolves independently page → area → scaffold (delta). Live/bindable. |
| `nalu:Scaffold.IsNavBarVisible` (bool) / `NavBarOverlapsContent` (bool) | Hide bar / draw bar OVER content | Overlap = full-bleed header recipe. |
| Primitives `ScaffoldNavBarTitle`, `ScaffoldBackButton`, `ScaffoldCloseButton`, `ScaffoldFlyoutButton` (`Side`), base `ScaffoldNavBarButtonBase` (`Icon`, `IconColor`, `PressedBrush`) | Style via implicit styles | Same styles apply in custom bars. |
| `ScaffoldNavBarContext` (`{nalu:NavBarBinding Path=…}`) | Ambient bar state | `Title`, `TitleView`, `Foreground`, `ScrollOffset`, `IsScrolledUnder`, `ScrollRampStart/End`, `CanNavigateBack`, `BackCommand`, `IsFlyoutStart/EndButtonVisible`, `OpenFlyoutStart/EndCommand`, `IsModal`, `IsCloseButtonVisible`, `CurrentPage`, `PageBindingContext`. |
| `NavBarBindings.Create(path, …)` / `NavBarBindings.ScaffoldAncestor` / `Scaffold.FindNavBarContext(element)` | Code-behind counterparts | Typed: `SetBinding(prop, static (Scaffold s) => s.NavBarContext.X, source: NavBarBindings.ScaffoldAncestor)`. |
| `nalu:Scaffold.SystemBarStyle` | `Auto` (default) / `LightContent` (white icons) / `DarkContent` | page → area → scaffold; describes the PAGE surface. |
| `nalu:Scaffold.ScrollTracker` (page, `{x:Reference}`) | Feeds `ScrollOffset`/`IsScrolledUnder` | ScrollView/CollectionView/VirtualScroll or any view wrapping a native scroller (≤ 3 levels deep). |
| `nalu:Scaffold.ScrollRampStart` / `ScrollRampEnd` | Page-wide default ramp (0 / 100) | page → area → scaffold. |
| `{nalu:ScrollValue From, To, RampStart?, RampEnd?, Extrapolate, Easing}` | Bind numeric/Color/solid Brush to scroll | `Extrapolate`: `Clamp` (default) / `Extend`. |
| `{nalu:ThemeScrollValue FromLight, ToLight, FromDark?, ToDark?, …}` | Theme-aware endpoints | Dark falls back to light; re-evaluates on theme change. |
| `nalu:Scaffold.PageMode`, `PageTransition`, `TransitionName` | Modals / transitions | → skill `nalu-scaffold-transitions`. |
| `nalu:Scaffold.KeyboardMode` | Soft keyboard | → skill `nalu-scaffold-keyboard`. |

## Patterns

New tab (in `AppScaffold.xaml`, inside `<nalu:ScaffoldTabBar>`):

```xml
<nalu:ScaffoldRoot Title="Orders" PageType="{x:Type pages:OrdersPage}"
                   nalu:ScaffoldTabBarView.BadgeText="{Binding PendingCount}">
    <nalu:ScaffoldRoot.Icon><FontImageSource FontFamily="Material" Glyph="&#xe8cc;" Color="{StaticResource TabIcon}" Size="24" /></nalu:ScaffoldRoot.Icon>
    <nalu:ScaffoldRoot.SelectedIcon><FontImageSource FontFamily="Material" Glyph="&#xe8cc;" Color="{StaticResource Accent}" Size="24" /></nalu:ScaffoldRoot.SelectedIcon>
</nalu:ScaffoldRoot>
```

Start drawer with the built-in menu (scaffold-level; page/area may override content and mode):

```xml
<nalu:Scaffold nalu:Scaffold.FlyoutStartMode="Flyout">
    <nalu:Scaffold.FlyoutStart>
        <nalu:ScaffoldFlyoutMenuView IsTabBarDisplayed="True">
            <nalu:ScaffoldFlyoutMenuView.HeaderView><Label Text="MyApp" Padding="16" /></nalu:ScaffoldFlyoutMenuView.HeaderView>
        </nalu:ScaffoldFlyoutMenuView>
    </nalu:Scaffold.FlyoutStart>
    ...
</nalu:Scaffold>
```

```csharp
public partial class HomePageModel(IScaffoldFlyoutController flyout) : ObservableObject
{
    [RelayCommand] private Task OpenMenu() => flyout.OpenAsync(ScaffoldFlyoutSide.Start);
}
```

Nav bar: TitleView + per-page appearance delta + hidden tab bar on a pushed page:

```xml
<ContentPage nalu:Scaffold.TabBarVisibility="Auto" Title="Order">
    <nalu:Scaffold.TitleView>
        <Label Text="{Binding OrderNumber}" TextColor="{nalu:NavBarBinding Path=Foreground}" VerticalTextAlignment="Center" />
    </nalu:Scaffold.TitleView>
    <nalu:Scaffold.NavBarAppearance>
        <nalu:ScaffoldNavBarAppearance Foreground="White" Background="{StaticResource Accent}" />
    </nalu:Scaffold.NavBarAppearance>
```

Full-bleed header, materializing bar, fading title, parallax (`Extend` = speed factor `To/RampEnd`):

```xml
<ContentPage nalu:Scaffold.NavBarOverlapsContent="True"
             nalu:Scaffold.SystemBarStyle="LightContent"
             nalu:Scaffold.ScrollTracker="{x:Reference Scroll}"
             nalu:Scaffold.ScrollRampStart="100" nalu:Scaffold.ScrollRampEnd="200" Title="Weather">
    <nalu:Scaffold.NavBarAppearance>
        <nalu:ScaffoldNavBarAppearance
            Background="{nalu:ThemeScrollValue FromLight=Transparent, ToLight={StaticResource BackgroundLight}, ToDark={StaticResource BackgroundDark}}"
            Foreground="{nalu:ThemeScrollValue FromLight=White, ToLight={StaticResource TextPrimaryLight}, ToDark={StaticResource TextPrimaryDark}}" />
    </nalu:Scaffold.NavBarAppearance>
    <nalu:Scaffold.TitleView><Label Text="Weather" Opacity="{nalu:ScrollValue From=0, To=1}" /></nalu:Scaffold.TitleView>
    <ScrollView x:Name="Scroll" SafeAreaEdges="None,None,None,Default">
        <VerticalStackLayout>
            <Grid HeightRequest="360">
                <Image Source="hero.jpg" Aspect="AspectFill" Margin="0,-120,0,0"
                       TranslationY="{nalu:ScrollValue RampStart=0, RampEnd=100, From=0, To=50, Extrapolate=Extend}" />
            </Grid>
            ...
        </VerticalStackLayout>
    </ScrollView>
</ContentPage>
```

Chrome theming = implicit styles in `Resources/Styles/Styles.xaml` (template already has entries):

```xml
<Style TargetType="nalu:ScaffoldNavBarButtonBase" ApplyToDerivedTypes="True"><Setter Property="IconColor" Value="{StaticResource Accent}" /></Style>
<Style TargetType="nalu:ScaffoldTabBarView"><Setter Property="BarBackground" Value="{StaticResource CardLight}" /></Style>
```

## Rules & gotchas

- `PageType` must be a registered page (or page-model) type; unset/unknown throws at startup. Same for `InitialRootPageType`.
- Selecting the active root pops its stack to root; selecting another root preserves the outgoing stack. Removing the current root at runtime auto-navigates to a fallback first.
- Tab bar icons render untinted: put color on the `ImageSource` (`FontImageSource.Color`) — hence the template's separate `Icon`/`SelectedIcon`.
- Overflow "More" is automatic when roots × `ItemWidth` exceed the width; the panel reuses the item template. `IsVisible="False"` hides a root from bar/menu but keeps its stack navigable.
- Custom `TabBarView`: `BindingContext` is the `ScaffoldTabBar` (bind `Roots`, use `Title`/`CurrentIcon`/`IsSelected`/`SelectCommand`). Consume the bottom inset on a CHILD of your bar (`SafeAreaEdges="None,None,None,Container"`), never on the bar root (iOS won't relayout).
- Chrome footprints (nav bar incl. status inset, tab bar) reach the page as safe-area insets. Template pages set page-level `SafeAreaEdges="None"` so the `ScrollView` pads instead: content scrolls UNDER the floating tab bar and rests clear of it; keep the Android clip-to-padding workaround in `MauiProgram.cs`. `NavBarOverlapsContent` removes only the top footprint; the page's `SafeAreaEdges` then rules the raw system insets.
- Drawer = content AND a non-`Disabled` mode. `OpenFlyoutAsync`/`OpenAsync` no-op when unavailable (safe to call). Navigation closes an open drawer; scrim tap / system back close it. Options (`Width`, `Scrim`) are scaffold-level only.
- A page-level `FlyoutStart` inherits that page's `BindingContext` and is cleaned up when the page leaves; pushes that do not set it keep the older page's/area's/scaffold's drawer.
- `NavBarAppearance` never writes into view properties; `Foreground` is only the primitives' color FALLBACK — an explicit `IconColor`/`TextColor` (the template's `ScaffoldNavBarTitle` style sets `TextColor`) wins; drop that setter if page `Foreground` must recolor the title. Defaults: `Background` #F7FFFFFF, `Opacity` 1, `OffsetY` 0. An appearance inside a shared `Style` is one object for all pages — constants only.
- `TitleView` binds the page model, NOT the context; use `{nalu:NavBarBinding}` for `Foreground`/`ScrollOffset`/`Title`. Custom nav bars get `ScaffoldNavBarContext` as `BindingContext` and own the top safe area themselves (default bar consumes the status inset).
- `ScrollValue`/`ThemeScrollValue` must target a bindable property directly (not in `Style` setters); numeric, `Color` or solid `Brush` only; `Extend` applies to numeric targets (colors/brushes always clamp). Prefer `TranslationY`/`Opacity`/colors over size-affecting properties. Page without a tracker reads offset 0 (values sit at `From`).
- Android recycler-backed trackers (`CollectionView`, `VirtualScroll`) accumulate deltas: thresholds fine, tight pixel parallax may drift.
- `SystemBarStyle` describes your content only: an opaque nav bar or open drawer covering the status bar always wins with its own luminance. `Auto` samples real pixels — declare `LightContent` on photo headers with white chrome so the sky doesn't flip icons.
- Never use `Shell.*`/`NavigationPage.*` attached properties (`Shell.TabBarIsVisible`, `NavigationPage.HasNavigationBar`, `Shell.TitleView`…): use the `nalu:Scaffold.*` equivalents.
- Read `reference.md` for: styling property tables (tab bar item/overflow, flyout menu, nav bar primitives), custom tab bar / nav bar recipes, tab bar panels, runtime structure changes, platform notes.

## See also

- `nalu-navigation` — pushing/popping pages, tab switching from code, intents.
- `nalu-scaffold-transitions` — `PageTransition`, `TransitionName`, `PageMode` modals.
- `nalu-scaffold-overlays` — popups, bottom sheets, `IOverlayService`.
- `nalu-scaffold-keyboard` — `KeyboardMode`, `SafeAreaEdges="SoftInput"`.
