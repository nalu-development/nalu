# Scaffold Structure & Tab Bar

The Scaffold describes your whole application as a tree:

```
Scaffold
 ├─ ScaffoldArea / ScaffoldTabBar     (areas — a tab bar is an area whose roots show as tabs)
 │   ├─ ScaffoldRoot                  (one root = one navigation stack)
 │   └─ ScaffoldRoot
 └─ ScaffoldRoot                      (a bare root can sit directly in the scaffold)
```

## Roots

A `ScaffoldRoot` binds a root **page type** to an independent navigation stack:

```xml
<nalu:ScaffoldRoot Title="Today"
                   PageType="{x:Type pages:TodayPage}" />
```

| Property | Purpose |
|----------|---------|
| `PageType` | The root page (must be registered in Nalu navigation). |
| `Title` | Shown by the default tab bar / flyout menu items. |
| `Icon` / `SelectedIcon` | Tab imagery; `CurrentIcon` (read-only) resolves per selection state. |
| `IsVisible` | Hides the root from chrome (tab bar / flyout) without removing its stack. |
| `IsSelected` (read-only) | Whether this root is the current one. |
| `SelectCommand` | Bindable command performing the engine-routed selection — the building block for custom tab bars and menus. |

Selecting the **already-active** root pops its stack to the root page (the familiar
"tap the active tab to go home" behavior). Selecting another root preserves the outgoing
stack — state (scroll positions, entries, view models) survives tab switches and is restored
when you come back.

`Scaffold.CurrentArea` and `ScaffoldArea.CurrentRoot` are read-only bindables reflecting the
engine-owned selection.

## The tab bar

`ScaffoldTabBar` is an area whose roots render in the bottom tab bar. The default bar is a
centered pill with icon+label items and **automatic overflow**: when items don't fit at the
configured item width, the bar shows a "More" item opening a wrap-grid panel with the remaining
roots (reusing the same item template).

The default bar is styled with plain MAUI implicit styles (`ScaffoldTabBarView`,
`ScaffoldTabBarItemView`, `ScaffoldTabBarOverflowView` are public types) — colors, pill
background, spacing and fonts are all standard setters.

### Replacing the bar entirely

Set `ScaffoldTabBar.TabBarView` to any MAUI view:

```xml
<nalu:ScaffoldTabBar>
    <nalu:ScaffoldTabBar.TabBarView>
        <local:MyTabBar />
    </nalu:ScaffoldTabBar.TabBarView>
    ...roots...
</nalu:ScaffoldTabBar>
```

Inside a custom bar, bind the area's `Roots` and use each root's `Title`, `CurrentIcon`,
`IsSelected` and `SelectCommand` — selection stays engine-routed (guards and lifecycle fire).

The bar view owns its bottom safe-area behavior: the strip is sized to the bar's measured
height and the bar extends into the bottom inset. Consume the inset inside your bar with
`SafeAreaEdges` (e.g. `None,None,None,Container` on the element that should clear the system
bar) — a full-bleed background can reach the very bottom edge while its content stays clear.

### Per-page tab bar visibility

```xml
<ContentPage nalu:Scaffold.TabBarVisibility="Hidden"> <!-- Auto | Visible | Hidden -->
```

`Auto` shows the bar on root pages and hides it on pushed pages. Visibility changes animate
(slide), concurrently with the page transition.

## Initial selection

Set `Scaffold.InitialRootPageType` to boot on a specific root; the default is the first
visible root.

## Runtime structure changes & XAML hot reload

`Areas` and `Roots` are observable and may be mutated at runtime: added roots appear in the
chrome and are navigable; removing the **current** root automatically navigates to a fallback
root before the removal completes. XAML hot reload is supported end to end — re-inflating the
scaffold adopts the existing navigation stacks by segment, so page state survives a reload;
`TabBarView` swaps apply live.

## Non-tab areas

A plain `ScaffoldArea` groups roots without tab chrome — useful with a
[flyout menu](scaffold-flyout.md) as the switcher, or for roots reachable only
programmatically.
