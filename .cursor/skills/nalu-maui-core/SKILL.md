---
name: nalu-maui-core
description: Nalu.Maui.Core utilities for soft keyboard handling and background iOS HTTP. Use when configuring soft keyboard (Resize/Pan), binding to keyboard state, or handling HTTP when the app is backgrounded on iOS.
---

# Nalu.Maui.Core

Utilities for soft keyboard management and iOS background HTTP requests.

## When to use

- Soft keyboard: consistent Resize/Pan behavior on Android and iOS; bind UI visibility to keyboard state.
- Background HTTP: avoid "The network connection was lost" when the app is backgrounded on iOS.

## Setup

No explicit `UseNalu*` in MauiProgram for Core; use the types and attachables below. For background HTTP on iOS, configure `AppDelegate` (see Caveats).

## Soft keyboard

- Enable default: `builder.UseNaluSoftKeyboardManager(defaultAdjustMode: SoftKeyboardAdjustMode.Resize)`.
- Per-layout override in XAML: `naluCore:SoftKeyboardManager.SoftKeyboardAdjustMode="Pan"` (ancestors are searched for first specified mode).
- Bind to keyboard state: `SoftKeyboardManager.State` (e.g. `IsHidden`) to show/hide areas when keyboard is visible.

```xml
<VerticalStackLayout naluCore:SoftKeyboardManager.SoftKeyboardAdjustMode="Pan">
    <Entry />
</VerticalStackLayout>
<VerticalStackLayout.IsVisible>
    <Binding Path="IsHidden" Source="{x:Static nalu:SoftKeyboardManager.State}" x:DataType="nalu:SoftKeyboardState" />
</VerticalStackLayout.IsVisible>
```

## Background iOS HTTP

Use `NSUrlBackgroundSessionHttpMessageHandler` so requests continue when the app is backgrounded:

```csharp
#if IOS
    HttpClient client = DeviceInfo.DeviceType == DeviceType.Virtual
        ? new()
        : new(new NSUrlBackgroundSessionHttpMessageHandler());
#else
    HttpClient client = new();
#endif
```

In `AppDelegate`:

```csharp
[Export("application:handleEventsForBackgroundURLSession:completionHandler:")]
public virtual void HandleEventsForBackgroundUrl(UIApplication application, string sessionIdentifier, Action completionHandler)
    => NSUrlBackgroundSessionHttpMessageHandler.HandleEventsForBackgroundUrl(application, sessionIdentifier, completionHandler);
```

For app crash/termination during a request: add header `NSUrlBackgroundSessionHttpMessageHandler.RequestIdentifierHeaderName` and implement `INSUrlBackgroundSessionLostMessageHandler` to handle the lost response.

## Caveats

- **Do not** use soft keyboard manager with MAUI's `CollectionView` that contains input fields (resize causes jumps). Use the **VirtualScroll** component instead (Nalu.Maui.VirtualScroll).
- iOS Simulator does not support background sessions; use a physical device.
- Process response and close the `using` block within a few seconds in background or iOS may terminate the app.

## Additional context

- Full docs: [Core](https://nalu-development.github.io/nalu/core.html)
- Code: [Source/Nalu.Maui.Core/](Source/Nalu.Maui.Core/)
