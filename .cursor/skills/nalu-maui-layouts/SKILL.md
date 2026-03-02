---
name: nalu-maui-layouts
description: Nalu.Maui.Layouts: ViewBox, TemplateBox, ToggleTemplate, ExpanderViewBox, Wrap layouts, Popups, Magnet. Use when scoping BindingContext, conditional templates, expandable content, flow layouts, modal popups, or constraint-based layout in MAUI XAML.
---

# Nalu.Maui.Layouts

Templates, binding context scoping, expanders, wrap layouts, popups, and constraint layout.

## Setup

```csharp
builder.UseNaluLayouts();
```

## When to use

- **ViewBox / TemplateBox / ToggleTemplate**: Content binding context, template selection, conditional UI (if-in-XAML).
- **ExpanderViewBox**: Animated expand/collapse (accordion-style).
- **HorizontalWrapLayout / VerticalWrapLayout**: Tags, chips, flowing content.
- **Popups**: Modal pages with scrim; style via `PopupScrim` and `PopupContainer`.
- **Magnet**: Constraint-based layout (alpha); use for complex layouts where Grid is cumbersome.

## ViewBox, TemplateBox, ToggleTemplate

**ViewBox**: Lightweight replacement for `ContentView`; `ContentBindingContext` scopes content binding; `IsClippedToBounds` for clipping.

```xml
<nalu:ViewBox ContentBindingContext="{Binding SelectedAnimal}">
    <views:AnimalView x:DataType="models:Animal" />
</nalu:ViewBox>
```

**TemplateBox**: Holds view from `DataTemplate` or `DataTemplateSelector`; `ContentBindingContext`; optional `TemplateContentPresenter` for projected content.

**ToggleTemplate**: Shows one of two templates by boolean; avoids keeping both in visual tree (better than toggling `IsVisible` when there are multiple nested nodes).

```xml
<nalu:ToggleTemplate Value="{Binding HasPermission}"
                     WhenTrue="{StaticResource AdminFormTemplate}"
                     WhenFalse="{StaticResource PermissionRequestTemplate}" />
```

## ExpanderViewBox

Animated expand/collapse. Properties: `IsExpanded`, `CollapsedHeight` (use `+Infinity` for size-change animation only), read-only `CanCollapse`.

```xml
<nalu:ExpanderViewBox CollapsedHeight="100" IsExpanded="{Binding IsExpanded}">
    <Label Text="{Binding LongDescription}" />
</nalu:ExpanderViewBox>
```

## Wrap layouts

- **HorizontalWrapLayout**: Left-to-right, wrap to next row. Properties: `HorizontalSpacing`, `VerticalSpacing`, `ExpandMode`, `ItemsAlignment`.
- **VerticalWrapLayout**: Top-to-bottom, wrap to next column; needs constrained height.

```xml
<nalu:HorizontalWrapLayout HorizontalSpacing="8" VerticalSpacing="8">
    <Label Text="Tag 1" BackgroundColor="LightBlue" Padding="8,4" />
    <Label Text="Tag 2" BackgroundColor="LightGreen" Padding="8,4" />
</nalu:HorizontalWrapLayout>
```

## Popups

Inherit from `PopupPageBase`; set content via `PopupContent`. Style `PopupScrim` and `PopupContainer` in `Styles.xaml`. Property: `CloseOnScrimTapped`. Works with Nalu Navigation (register popup page/model like other pages).

## Magnet

Constraint-based layout (alpha). Good for complex or responsive layouts; avoid inside frequently recycled templates. See docs for API.

## Caveats

- Magnet is alpha; API may change. In CollectionView item templates, reference `MagnetStage` from Resources, not inside the template.

## Additional context

- [Layouts](https://nalu-development.github.io/nalu/layouts.html) · [ViewBox & Templates](https://nalu-development.github.io/nalu/layouts-viewbox.html) · [ExpanderViewBox](https://nalu-development.github.io/nalu/layouts-expander.html) · [Wrap](https://nalu-development.github.io/nalu/layouts-wrap.html) · [Popups](https://nalu-development.github.io/nalu/layouts-popup.html) · [Magnet](https://nalu-development.github.io/nalu/layouts-magnet.html)
- Code: [Source/Nalu.Maui.Layouts/](Source/Nalu.Maui.Layouts/)
