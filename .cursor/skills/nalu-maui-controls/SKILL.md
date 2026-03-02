---
name: nalu-maui-controls
description: Nalu.Maui.Controls: InteractableCanvasView (touch-enabled SkiaSharp) and DurationWheel (TimeSpan? picker). Use when adding touch handling to a canvas or a duration input control.
---

# Nalu.Maui.Controls

Cross-platform controls: touch-enabled canvas and duration picker.

## When to use

- **InteractableCanvasView**: SkiaSharp drawing with touch events; stop propagation so parent (e.g. ScrollView) does not scroll.
- **DurationWheel**: Let the user enter a duration by spinning a wheel (e.g. in a popup).

## InteractableCanvasView

Inherits from SkiaSharp `SKCanvasView`; overridable touch handlers and optional propagation stop.

Override in subclass:

- `OnTouchPressed(TouchEventArgs p)`
- `OnTouchReleased(TouchEventArgs p)`
- `OnTouchMoved(TouchEventArgs p)`

In handlers: `args.Position` for location; `args.StopPropagation()` to prevent parent (e.g. ScrollView) from handling the gesture.

**Note:** API is experimental and may change.

## DurationWheel

`TimeSpan?` control; user spins a wheel to set duration. Bindable: `Duration`, `WholeDuration`, `MaximumDuration`. Theming: colors (e.g. `OuterBackgroundColor`, `InnerBackgroundColor`, `MarkersColor`, `HighValueColor`, `LowValueColor`), `ValueWidth`. Events: `RotationStarted`, `RotationEnded`.

```xml
<nalu:DurationWheel
    Duration="{Binding Duration}"
    WholeDuration="{Binding WholeDuration}"
    MaximumDuration="{Binding MaxDuration}"
    RotationEnded="DurationWheel_OnRotationEnded"
    RotationStarted="DurationWheel_OnRotationStarted" />
```

## Additional context

- Full docs: [Controls](https://nalu-development.github.io/nalu/controls.html)
- Code: [Source/Nalu.Maui.Controls/](Source/Nalu.Maui.Controls/)
