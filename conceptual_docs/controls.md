## Controls [![Nalu.Maui.Controls NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Controls.svg)](https://www.nuget.org/packages/Nalu.Maui.Controls/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Controls)](https://www.nuget.org/packages/Nalu.Maui.Controls/)

The controls library provides a set of cross-platform controls to simplify your development.

### `InteractableCanvasView` - Touch-enabled `SKCavasView`

A `InteractableCanvasView` which is a `SkiaSharp` `SKCanvasView` with touch-events support where you can choose to stop touch event propagation to avoid interaction with ancestors (like `ScrollView`).

Note: this API is Experimental and may be subject to change in the future.

By inheriting from this class you'll have access to overridable methods that let's you react to touch events.

```
/// <summary>
/// Invoked when the pointer/tap is pressed.
/// </summary>
protected virtual void OnTouchPressed(TouchEventArgs p) { }

/// <summary>
/// Invoked when the pointer/tap is released.
/// </summary>
protected virtual void OnTouchReleased(TouchEventArgs p) { }

/// <summary>
/// Invoked when the pointer/tap moves.
/// </summary>
protected virtual void OnTouchMoved(TouchEventArgs p) { }
```

You can use the `TouchEventArgs` parameter to:
- Retrieve the touch location: `var position = args.Position;`
- Stop the propagation (very useful to avoid scrolling on parent `ScrollView`): `args.StopPropagation();`

### `DurationWheel` - Ask the user for a duration!

A `TimeSpan?` edit control named `DurationWheel` which allows the user to enter a duration by spinning a wheel.

<video width="400" height="400" autoplay loop muted playsinline controls>
  <source src="https://github.com/user-attachments/assets/921dc279-f5ee-4da5-87d5-8b947df29a16" type="video/mp4">
  Your browser does not support the video tag.
</video>

You can fully theme this control on every single aspect, from colors to duration-edit behaviors.

```xml
<nalu:DurationWheel
    ValueWidth="16"
    OuterBackgroundColor="{AppThemeBinding Light={StaticResource Gray300}, Dark={StaticResource Gray950}}"
    InnerBackgroundColor="{AppThemeBinding Light={StaticResource White}, Dark={StaticResource Gray750}}"
    MarkersColor="{AppThemeBinding Light={StaticResource Black}, Dark={StaticResource Gray300}}"
    HighValueColor="{StaticResource Secondary}"
    LowValueColor="{StaticResource PrimaryDark}"
    Duration="{Binding Duration}"
    WholeDuration="{Binding WholeDuration}"
    MaximumDuration="{Binding MaxDuration}"
    RotationEnded="DurationWheel_OnRotationEnded"
    RotationStarted="DurationWheel_OnRotationStarted" />
```

As an example, you can see how to implement a `DurationWheel` popup [here](https://github.com/nalu-development/nalu/blob/main/Samples/Nalu.Maui.Weather/Popups/DurationEditPopup.xaml.cs).
