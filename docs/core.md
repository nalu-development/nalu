## Core [![Nalu.Maui.Core NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Core.svg)](https://www.nuget.org/packages/Nalu.Maui.Core/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Core)](https://www.nuget.org/packages/Nalu.Maui.Core/)

The core library is intended to provide a set of common use utilities.

### Background iOS HttpClient requests

Have you ever noticed that when the user backgrounds the app on iOS, the app is suspended, and the network requests fails due to `The network connection was lost` exception?

```csharp
var client = new HttpClient();
var responseTask = client.GetAsync("https://myservice.foo/my-endpoint");
// --> i.e. App goes to background because user receives a phone call
// ...
// --> After a while the user comes back to the app
var response = await responseTask; // <-- Exception: The network connection was lost
```

This is really annoying: it forces us to implement complex retry logic, especially considering that **the request may have already hit the server** and potentially made changes there.

To solve this issue, we provide a `NSUrlBackgroundSessionHttpMessageHandler` to be used in your `HttpClient` to allow http request to continue even when the app is in the background.

```csharp
#if IOS
    var client = new HttpClient(new NSUrlBackgroundSessionHttpMessageHandler());
#else
    var client = new HttpClient();
#endif
```

To make this work, you need to change your `AppDelegate` as follows:
```csharp
[Export("application:handleEventsForBackgroundURLSession:completionHandler:")]
public virtual void HandleEventsForBackgroundUrl(UIApplication application, string sessionIdentifier, Action completionHandler)
    => NSUrlBackgroundSessionHttpMessageHandler.HandleEventsForBackgroundUrl(application, sessionIdentifier, completionHandler);
```

Right after receiving the response `await responseTask` you have a small timeframe of 250ms to eventually start a new request.
This can happen even if the app is in the background, making it possible to process a queue of requests with your custom queue logic.

Please note that if this occurs while the app is in the background, iOS will process new requests after some time, which is beyond our control.

Foregrounding the app will immediately process the pending requests.
