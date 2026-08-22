---
name: nalu-scaffold-structure
description: Nalu.Maui.Scaffold app shell — Scaffold/areas/roots, tab bar (icons, badges, overflow, visibility), flyout drawers, nav bar (title/TitleView, appearance, custom bar), safe areas & system bars; load when editing AppScaffold.xaml or page chrome (tab bar, nav bar, drawer).
---
# Scaffold structure, tab bar, flyout, nav bar, system bars

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
| `ScaffoldTabBarView` | Default pill bar; implicit-style it | `ItemWidth` 68, `OverflowIcon`/`OverflowTitle` ("More"), `BarBackground`, `BarCornerRadius`, `BarMargin`, `BarPadding`, `BarShadow`; attached `ScaffoldTabBarView.BadgeText` on a `ScaffoldRoot`. |
| `ScaffoldTabBarItemView`, `ScaffoldTabBarOverflowView` | Item / overflow-panel styling | See reference.md tables. |
| `nalu:Scaffold.FlyoutStart` / `FlyoutEnd` (View) + `FlyoutStartMode` / `FlyoutEndMode` | Drawers; page → area → scaffold | Mode `Disabled` (default) / `Auto` (roots only) / `Flyout`. Content alone does nothing. |
| `ScaffoldFlyoutMenuView` | Ready-made menu of areas/roots | Set `IsTabBarDisplayed="True"` to list tab-bar roots (else empty in a tab-only app). |
| `Scaffold.FlyoutStartOptions` / `FlyoutEndOptions` (`ScaffoldFlyoutOptions`) | `Width`, `WidthRatio` 0.85, `MaximumWidth` 360, `Scrim` | Scaffold-level only. |
| `IScaffoldFlyoutController` (DI, page-scoped) | `OpenAsync(ScaffoldFlyoutSide)`, `CloseAsync()` | Also `scaffold.OpenFlyoutAsync/CloseFlyoutAsync`, `IsFlyoutStartOpen/EndOpen`, `FlyoutStartOpened/Closed` events. |
| `nalu:Scaffold.FlyoutStartButtonVisibility` / `End…` | Nav-bar drawer button: `Auto` (roots) / `Visible` / `Hidden` | page → area → scaffold. |
| Page `Title` / `nalu:Scaffold.TitleView` | Nav bar center | `TitleView.BindingContext` = page model. |
| `nalu:Scaffold.NavBarTemplate` | Bar TEMPLATE; page → area → scaffold | Default a template of `ScaffoldNavBarView` (`BarHeight` 48, `BarPadding` 8,0, `Spacing` 8). `{x:Null}` on the scaffold = no bar. A template, not a view: every page realizes its own bar, because the bar travels with its page and two are on screen during a transition. |
| `nalu:Scaffold.NavBarBackground` / `NavBarForeground` / `NavBarTitleForeground` / `NavBarOpacity` / `NavBarOffsetY` | Strip surface (foreground = buttons + title fallback; title foreground = title only) | Attached properties on real elements: each resolves independently page → area → scaffold (delta), and binds, themes and scroll-animates with no extra machinery. A `Style` setter gives every element its own value. |
| `nalu:Scaffold.IsNavBarVisible` (bool) / `NavBarOverlapsContent` (bool) | Hide bar / draw bar OVER content | Overlap = full-bleed header recipe. |
| Primitives `ScaffoldNavBarTitle`, `ScaffoldBackButton`, `ScaffoldCloseButton`, `ScaffoldFlyoutButton` (`Side`), base `ScaffoldNavBarButtonBase` (`Icon`, `IconColor`, `PressedBrush`) | Style via implicit styles | Same styles apply in custom bars. |
| `ScaffoldNavBarContext` (`{nalu:NavBarBinding Path=…}`) | Ambient bar state | `Title`, `TitleView`, `Foreground`, `TitleForeground`, `ScrollOffset`, `IsScrolledUnder`, `ScrollRampStart/End`, `CanNavigateBack`, `BackCommand`, `IsFlyoutStart/EndButtonVisible`, `OpenFlyoutStart/EndCommand`, `IsModal`, `IsCloseButtonVisible`, `CurrentPage`, `PageBindingContext`. |
| `NavBarBindings.Create(target, path, …)` / `Scaffold.FindNavBarContext(element)` | Code-behind counterparts | Pass the element the binding is applied to — it is what the PAGE is resolved from, so the binding reads that element's own page. |
| `nalu:Scaffold.SystemBarStyle` | `Auto` (default) / `LightContent` (white icons) / `DarkContent` | page → area → scaffold; describes the PAGE surface. |
| `nalu:Scaffold.ScrollTracker`, `ScrollRampStart/End`, `{nalu:ScrollValue}`, `{nalu:ThemeScrollValue}` | Scroll-driven chrome / parallax | → skill `nalu-scaffold-scroll`. |
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
    nalu:Scaffold.NavBarForeground="White"
    nalu:Scaffold.NavBarTitleForeground="White"
    nalu:Scaffold.NavBarBackground="{StaticResource Accent}"
```

Full-bleed / materializing nav bar and parallax headers → skill `nalu-scaffold-scroll` (uses `NavBarOverlapsContent`, the nav bar appearance properties and `SystemBarStyle` from here).

Chrome theming = implicit styles in `Resources/Styles/Styles.xaml` (template already has entries):

```xml
<!-- ApplyToDerivedTypes is REQUIRED here: AppScaffold DERIVES from Scaffold and MAUI matches
     implicit styles on the exact type, so without it the whole style is silently skipped and
     the bar keeps the library defaults — theme changes included. -->
<Style TargetType="nalu:Scaffold" ApplyToDerivedTypes="True"><Setter Property="nalu:Scaffold.NavBarForeground" Value="{StaticResource Accent}" /></Style>
<Style TargetType="nalu:ScaffoldNavBarButtonBase" ApplyToDerivedTypes="True"><Setter Property="IconColor" Value="{StaticResource Accent}" /></Style>
<Style TargetType="nalu:ScaffoldTabBarView"><Setter Property="BarBackground" Value="{StaticResource CardLight}" /></Style>
```

The same applies to any chrome type you subclass; the ones above with no opt-in are instantiated
by the library itself, so an exact-type match is what you want there.

## Rules & gotchas

- An implicit `Style TargetType="nalu:Scaffold"` needs `ApplyToDerivedTypes="True"` — `AppScaffold` derives from `Scaffold`. Symptom when missing: page content follows the app theme but the nav bar never does, and flipping the theme changes nothing.
- `PageType` must be a registered page (or page-model) type; unset/unknown throws at startup. Same for `InitialRootPageType`.
- Selecting the active root pops its stack to root; selecting another root preserves the outgoing stack. Removing the current root at runtime auto-navigates to a fallback first.
- Tab bar icons render untinted: put color on the `ImageSource` (`FontImageSource.Color`) — hence the template's separate `Icon`/`SelectedIcon`.
- Overflow "More" is automatic when roots × `ItemWidth` exceed the width; the panel reuses the item template. `IsVisible="False"` hides a root from bar/menu but keeps its stack navigable.
- Custom chrome takes touches only where it DRAWS. A bar's strip spans the full width whatever the bar paints, and the rest belongs to the page: give the drawn surface a `TapGestureRecognizer` (a MAUI view carrying one consumes touches on both platforms, and it travels with the view — a bar moved by `NavBarOffsetY` stops claiming the band), and mark the layouts that merely position `InputTransparent="True" CascadeInputTransparent="False"` so they stop swallowing while their content keeps its touches. Symptom when missing: a dead strip beside a floating pill, or a visible bar operating the content behind it.
- On a WINDOWED iPad (iPadOS 26) the system draws its window controls over the window's top-leading corner. The scaffold clears them for you: the nav bar strip gets a LEADING safe-area inset and a left drawer a TOP one, so any bar or drawer content that consumes the container safe area (`SafeAreaEdges="Container"`, which you want anyway for the status bar) moves out of the way. Content that opts out of safe areas to draw edge-to-edge stays under them — same as it already does for the status bar.
- Custom `TabBarView`: `BindingContext` is the `ScaffoldTabBar` (bind `Roots`, use `Title`/`CurrentIcon`/`IsSelected`/`SelectCommand`). Consume the bottom inset on a CHILD of your bar (`SafeAreaEdges="None,None,None,Container"`), never on the bar root (iOS won't relayout).
- Chrome footprints (nav bar incl. status inset, tab bar) reach the page as safe-area insets. Template pages set page-level `SafeAreaEdges="None"` so the `ScrollView` pads instead: content scrolls UNDER the floating tab bar and rests clear of it; keep the Android clip-to-padding workaround in `MauiProgram.cs`. `NavBarOverlapsContent` removes only the top footprint; the page's `SafeAreaEdges` then rules the raw system insets.
- Drawer = content AND a non-`Disabled` mode. `OpenFlyoutAsync`/`OpenAsync` no-op when unavailable (safe to call). Navigation closes an open drawer; scrim tap / system back close it. Options (`Width`, `Scrim`) are scaffold-level only.
- A page-level `FlyoutStart` inherits that page's `BindingContext` and is cleaned up when the page leaves; pushes that do not set it keep the older page's/area's/scaffold's drawer.
- The nav bar appearance properties never write into view properties; their colors are the primitives' FALLBACK: buttons ← `NavBarForeground`; title ← level-wise `NavBarTitleForeground` ?? `NavBarForeground` (the first of page/area/scaffold that sets either wins, so a page's `NavBarForeground` alone recolors the title too). An explicit (or styled) `IconColor`/`TextColor` on a primitive wins and PINS it — the template styles neither (scaffold-level `NavBarForeground` = accent for buttons, `NavBarTitleForeground` = text-primary for the title), so a page can recolor both with its own values. Do not add `TextColor`/`IconColor` setters to `ScaffoldNavBarTitle`/`ScaffoldNavBarButtonBase` styles unless the color must never follow the page. Defaults: `NavBarBackground` #F7FFFFFF, `NavBarOpacity` 1, `NavBarOffsetY` 0. A `Style` setter is fine for any of them — each element gets its own VALUE, nothing is shared.
- `TitleView` binds the page model, NOT the context; use `{nalu:NavBarBinding}` for `Foreground`/`ScrollOffset`/`Title`. Custom nav bars get `ScaffoldNavBarContext` as `BindingContext` and own the top safe area themselves (default bar consumes the status inset).
- `SystemBarStyle` describes your content only: an opaque nav bar or open drawer covering the status bar always wins with its own luminance. `Auto` samples real pixels — declare `LightContent` on photo headers with white chrome so the sky doesn't flip icons.
- Never use `Shell.*`/`NavigationPage.*` attached properties (`Shell.TabBarIsVisible`, `NavigationPage.HasNavigationBar`, `Shell.TitleView`…): use the `nalu:Scaffold.*` equivalents.
- Read `reference.md` for: styling property tables (tab bar item/overflow, flyout menu, nav bar primitives), custom tab bar / nav bar recipes, tab bar panels, runtime structure changes, platform notes.

## See also

- `nalu-navigation` — pushing/popping pages, tab switching from code, intents.
- `nalu-scaffold-scroll` — `ScrollTracker`, ramps, `{nalu:ScrollValue}` parallax / materializing bar.
- `nalu-scaffold-transitions` — `PageTransition`, `TransitionName`, `PageMode` modals.
- `nalu-scaffold-overlays` — popups, bottom sheets, `IOverlayService`.
- `nalu-scaffold-keyboard` — `KeyboardMode`, `SafeAreaEdges="SoftInput"`.
