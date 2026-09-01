## Migrating from Magnet 1.x to Magnet 2.0

Magnet 2.0 replaces the Cassowary solver with a compiled constraint engine and revises the public API. XAML written
for 1.x does not compile against 2.0; this page maps every 1.x concept to its 2.0 counterpart.

### Namespaces and types

| Magnet 1.x | Magnet 2.0 |
|---|---|
| `Nalu.MagnetLayout.*` (`MagnetStage`, `MagnetView`, `HorizontalBarrier`, …) | everything lives in `Nalu` (`MagnetDefinition`, `MagnetView`, `MagnetBarrier`, `MagnetGuideline`, `MagnetChain`) |
| `Magnet.Stage` (`IMagnetStage` / `MagnetStage`) | `Magnet.Definition` (`MagnetDefinition`) — created lazily when not assigned |
| `Magnet.StageId="x"` attached property | `Magnet.MagnetId="x"` |
| `Id` on elements | `MagnetId` (mandatory, unique; no auto-generated ids) |
| `"Stage"` reserved id | `"parent"` (`MagnetAnchor.Parent`) |
| `HorizontalPullTarget` / `VerticalPullTarget` (`"Stage.Left"`, `"a.Right!"`) | `MagnetAnchor` (`"parent.Left"`, `"a.Right,12,gone:0"`); no `!` traction |
| `Margin` / `CollapsedMargin` (`Thickness` on the view element) | per-anchor `Margin` and `GoneMargin` (`"a.Right,12,gone:0"`) |
| `Width`/`Height` (`SizeValue`: `"1"`, `"*"`, `"50%"`, `"1r"`, `"1~"`) | `WidthSizing`/`HeightSizing` (`MagnetSizing`: `"48"`, `"*"`, `"50%"`, otherwise `{nalu:MagnetSizing 1.5, Unit=Ratio}` / `Unit=StagePercent` / `Unit=Measured` / `Min`/`Max`) |
| `SizeUnit.Stage` (`"50%"` = 50% of the stage) | `{nalu:MagnetSizing 0.5, Unit=StagePercent}`; note that `"50%"` now means 50% of the span between the view's two anchors |
| `SizeUnit.Measured` coefficient (`"1.5"` = 1.5 × measured) | `{nalu:MagnetSizing 1.5, Unit=Measured}`; a bare number is now a **fixed** size |
| `"*"` with a single anchor (as wide as the stage) | `{nalu:MagnetSizing 1, Unit=StagePercent}` |
| `SizeBehavior.Shrink` (`~`) | measured views are measured with their available span, so a wrapping view already fits; use `max:` for hard limits |
| `HorizontalBarrier` / `VerticalBarrier` (`Elements="a,b"`, `Pole`) | `MagnetBarrier` (`Direction`, `Nodes="a,b"` or `<x:String>` content) |
| `HorizontalGuideline` / `VerticalGuideline` (`FractionalPosition`, `Position`) | `MagnetGuideline` (`Orientation`, `Percent`, `Position`) |
| implicit chains (mutual anchors `a.RightTo=b.Left` + `b.LeftTo=a.Right`, `Traction.Strong`) | explicit `MagnetChain` node (`Style`, `Weights`) |

### Declaring constraints

1.x declared every view inside the stage and referenced it by `StageId`:

```xml
<nalu:Magnet>
  <nalu:Magnet.Stage>
    <nalu:MagnetStage>
      <nalu:MagnetView Id="title" LeftTo="avatar.Right" TopTo="Stage.Top" Margin="12,0,0,0" />
    </nalu:MagnetStage>
  </nalu:Magnet.Stage>
  <Label nalu:Magnet.StageId="title" />
</nalu:Magnet>
```

2.0 lets you write the constraints **on the view** (preferred), or keep a `MagnetView` in the definition bound by id:

```xml
<nalu:Magnet>
  <Label nalu:Magnet.MagnetId="title" nalu:Magnet.LeftTo="avatar.Right,12" nalu:Magnet.TopTo="parent.Top" />
</nalu:Magnet>
```

Do not do both for the same id: it is an error (`MagnetId 'title' is defined both in the MagnetDefinition and
inline on a child view.`).

### Chains

Replace mutual anchors with a chain node. Margins between members go on the anchors to the adjacent member:

```xml
<nalu:Magnet.Definition>
  <nalu:MagnetDefinition>
    <nalu:MagnetChain MagnetId="row" Style="Packed">
      <x:String>a</x:String><x:String>b</x:String>
    </nalu:MagnetChain>
  </nalu:MagnetDefinition>
</nalu:Magnet.Definition>
<Label nalu:Magnet.MagnetId="a" nalu:Magnet.LeftTo="parent.Left" nalu:Magnet.HorizontalBias="0" />
<Label nalu:Magnet.MagnetId="b" nalu:Magnet.LeftTo="a.Right,8" nalu:Magnet.RightTo="parent.Right" />
```

### Visibility

1.x tracked `Collapsed` on the element; 2.0 reads `IView.Visibility` of the bound view (`IsVisible="False"`
collapses). A node can also *declare* `ApplyVisibility="Hide"/"Show"` (`ConstraintSet`-style scenes): a one-shot
action stamped onto the view's `IsVisible` when the definition is applied — see the Scenes section of the Magnet page.

### Sharing definitions

1.x recommended sharing a `MagnetStage` from resources for `CollectionView` templates. 2.0 forbids sharing a
`MagnetDefinition` across layouts (it throws): declare it inline in the template — inflation (compile included) costs
a few tens of microseconds per instance.

### Semantics that changed on purpose

- Measure hugs the content: every view (including views hanging outside via negative offsets) fits between the stage
  edges; `*`-sized children contribute their margins only.
- A collapsed view drops its own margins (ConstraintLayout semantics).
- Barriers ignore collapsed members.
- Guideline `Position` is added to the `Percent`-based position.
- A `Ratio` width fed by a `*`/`%` height is resolved with a single cross-axis feedback iteration (1.x iterated the solver to convergence).
