## Core [![Nalu.Maui.Core NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Core.svg)](https://www.nuget.org/packages/Nalu.Maui.Core/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Core)](https://www.nuget.org/packages/Nalu.Maui.Core/)

The core library is intended to provide a set of common use utilities.

### Soft keyboard handling

An alternative soft keyboard management for iOS and Android mobile platforms in order to standardize the UX.

This can be enabled via `builder.UseNaluSoftKeyboardManager(defaultAdjustMode: SoftKeyboardAdjustMode.Resize)`.

You can then customize the keyboard behavior depending on where the text entry lives.

```xml
<ContentPage>
    <Grid>
        <VerticalStackLayout naluCore:SoftKeyboardManager.SoftKeyboardAdjustMode="Pan">
            <Entry />
```

When the keyboard is about to be shown, the system will loop through ancestors and seek for the first specified keyboard adjust mode.
If not found, it will fall back to what specified in `UseNaluSoftKeyboardManager`.

`SoftKeyboardManager` also provides a `State` observable object which provides keyboard state information.
This can be useful if you want to bind the visibility of an element to the visibility of the keyboard (very useful when you want to hide some areas in `Resize` mode when the keyboard is visible.

This is less flexible than the MAUI's iOS `KeyboardAutoManagerScroll` but it is more consistent and probably convenient thanks to the keyboard-state bindable properties.

### Background iOS HttpClient requests

Have you ever noticed that when the user backgrounds the app on iOS, the app is suspended, and the network requests fails due to `The network connection was lost` exception?

```csharp
var client = new HttpClient();
var responseTask = client.GetAsync("https://myservice.foo/my-endpoint");
// --> i.e. App goes to background because user receives a phone call
// ...
// --> After a while the user comes back to the app
using var response = await responseTask; // <-- Exception: The network connection was lost
var content = await response.Content.ReadAsStringAsync();
```

This is really annoying: it forces us to implement complex retry logic, especially considering that **the request may have already hit the server** and potentially made changes there.

To solve this issue, we provide a `NSUrlBackgroundSessionHttpMessageHandler` to be used in your `HttpClient` to allow http request to continue even when the app is in the background.

```csharp
#if IOS
    HttpClient client = DeviceInfo.DeviceType == DeviceType.Virtual
        ? new() // iOS Simulator doesn't support background sessions
        : new(new NSUrlBackgroundSessionHttpMessageHandler());
#else
    HttpClient client = new();
#endif
```

To make this work, you need to change your `AppDelegate` as follows:
```csharp
[Export("application:handleEventsForBackgroundURLSession:completionHandler:")]
public virtual void HandleEventsForBackgroundUrl(UIApplication application, string sessionIdentifier, Action completionHandler)
    => NSUrlBackgroundSessionHttpMessageHandler.HandleEventsForBackgroundUrl(application, sessionIdentifier, completionHandler);
```

If your app is in the background and the response is ready, the system will wake up the app complete the request.
```csharp
using var response = await responseTask; // <-- No exception :)
```

So we can finally process our response:
```csharp
var content = await response.Content.ReadAsStringAsync();
// do something with the content 
```

Make sure to close the `using` block to acknowledge the system that the response has been processed.
This needs to be completed within a few seconds while the app is in the background, or iOS will terminate the app.

**What if my app crashes (or is terminated by the system) while a request is in progress?**

No worries! The system will automatically continue your request and restart your app in the background to handle the response.
Problem is: given the app is not running, there's no `await` to wait for the response, and the system will discard it.

To handle this use case we need to do a couple of things:
1. We need a way to identify the request that was in progress
2. We need a way to react to the lost response

To identify the request, we have to add a special header to the request:
```csharp
httpRequestMessage.Headers.Add(NSUrlBackgroundSessionHttpMessageHandler.RequestIdentifierHeaderName, requestIdentifier);
```

We can then register a singleton service implementing the `INSUrlBackgroundSessionLostMessageHandler` interface to handle the lost response.

```csharp
public class NSUrlBackgroundSessionLostMessageHandler : INSUrlBackgroundSessionLostMessageHandler
{
    public async Task HandleLostMessageAsync(NSUrlBackgroundResponseHandle responseHandle)
    {
        try {
            var requestIdentifier = responseHandle.RequestIdentifier;
            using var response = await responseHandle.GetResponseAsync();
            var content = await response.Content.ReadAsStringAsync();
            // store content in a safe place related to the requestIdentifier
        } catch (Exception ex) {
            // handle exception
        }
    }
}
```
