# AgentClient (Microsoft.Maui.DevFlow.Driver) — API surface

Extracted from dotnet/maui-labs `main` (July 2026, preview.12). This is the public API our
`NaluApp` wrapper builds on. If the installed package diverges, trust the compiler over this
document — fix `NaluApp.cs`, then update this file.

Namespace: `Microsoft.Maui.DevFlow.Driver`. Per maui-labs AGENTS.md, `AgentClient` is the
supported public API of the Driver package ("method signature changes are binary and source
breaking for consumers"), i.e. the closest thing to a stable contract DevFlow has.

## Construction & lifecycle

```csharp
public AgentClient(string host = "localhost", int port = 9223)
public string BaseUrl { get; }
public int TransientFailureRetryCount { get; set; }
public bool RetryMutatingRequests { get; set; }
public TimeSpan TransientFailureRetryDelay { get; set; }
public void Dispose()
```

## Status & discovery

```csharp
Task<AgentStatus?> GetStatusAsync(int? window = null)            // health check / connect probe
Task<JsonElement> GetCapabilitiesAsync()
Task<Dictionary<string, ExtensionDescriptor>> GetExtensionsAsync()
```

## Visual tree & queries

```csharp
Task<List<ElementInfo>> GetTreeAsync(int maxDepth = 0, int? window = null)
Task<ElementInfo?> GetElementAsync(string id)
Task<List<ElementInfo>> QueryAsync(string? type = null, string? automationId = null, string? text = null)
Task<List<ElementInfo>> QueryCssAsync(string selector)           // CSS selector engine (Fizzler)
```

## Interactions

```csharp
Task<bool> TapAsync(string elementId)
Task<bool> FillAsync(string elementId, string text)
Task<bool> ClearAsync(string elementId)
Task<bool> FocusAsync(string elementId)
Task<bool> NavigateAsync(string route)                           // MAUI/Shell routing
Task<bool> BackAsync()
Task<bool> KeyAsync(string key, string? elementId = null, string? text = null)
Task<bool> GestureAsync(string type, string? elementId = null, string? direction = null,
                        double? distance = null, int? durationMs = null)
Task<bool> ScrollAsync(string? elementId = null, double deltaX = 0, double deltaY = 0,
                       bool animated = true, int? window = null, int? itemIndex = null,
                       int? groupIndex = null, string? scrollToPosition = null)
Task<bool> ResizeAsync(int width, int height, int? window = null)
Task<JsonElement> BatchAsync(IEnumerable<JsonObject> actions, bool continueOnError = false)
```

Note for virtualized lists: `ScrollAsync` accepts `itemIndex`/`groupIndex`/`scrollToPosition` —
promising for VirtualScroll tests (exact accepted values not yet verified; try
`itemIndex` first, fall back to delta-based scrolling loops).

## Screenshots & properties

```csharp
Task<byte[]?> ScreenshotAsync(int? window = null, string? elementId = null, string? selector = null,
                              int? maxWidth = null, string? scale = null)
Task<ScreenshotResult> ScreenshotResultAsync(...same args...)
Task<string?> GetPropertyAsync(string elementId, string propertyName)   // any MAUI property, e.g. "Text", "IsVisible"
Task<bool> SetPropertyAsync(string elementId, string propertyName, string value)
Task<string> HitTestAsync(double x, double y, int? window = null)
```

`GetPropertyAsync`/`SetPropertyAsync` are the superpower vs Appium: direct access to MAUI
bindable properties of live elements.

## Theme, logs, diagnostics

```csharp
Task<ThemeResult?> GetThemeAsync()
Task<ThemeResult> SetThemeAsync(DevFlowTheme theme)              // light/dark testing!
Task<string> GetLogsAsync(int limit = 100, int skip = 0, string? source = null)
Task<List<NetworkRequest>> GetNetworkRequestsAsync(int limit = 100, string? host = null, string? method = null)
Task<NetworkRequest?> GetNetworkRequestDetailAsync(string id)
Task<bool> ClearNetworkRequestsAsync()
```

Profiler (jank hunting on VirtualScroll): `GetProfilerCapabilitiesAsync`, `StartProfilerAsync`,
`StopProfilerAsync`, `GetProfilerSamplesAsync`, `GetProfilerHotspotsAsync(limit, minDurationMs, kind)`,
`PublishProfilerMarkerAsync(name, type, payloadJson)`.

## App state

```csharp
// Preferences / SecureStorage: Get/Set/Delete/Clear...PreferenceAsync, ...SecureStorageAsync
// Files: ListStorageRootsAsync, ListFilesAsync, DownloadFileAsync, UploadFileAsync, DeleteFileAsync
// Device: GetPlatformInfoAsync, GetGeolocationAsync, GetSensorsAsync, Start/StopSensorAsync,
//         GetJobsAsync, RunJobAsync
// App actions (app-registered): ListActionsAsync(), InvokeActionAsync(name, args)
// App extensions: CallExtensionToolAsync(method, path, parameters)  → /api/v1/ext/{ns}/...
```

`InvokeActionAsync`/`CallExtensionToolAsync` are the hook for app-side test helpers (e.g. a
future "reset" or "seed data" endpoint registered by the TestApp) — worth exploring when the
UI-based ResetButton overlay becomes limiting (e.g. Shell-based test pages).

## ElementInfo model

```csharp
class ElementInfo {
  string Id; string? ParentId; string Type; string FullType; string Framework;
  string? AutomationId; string? Text; string? Value; string? Role;
  bool IsVisible; bool IsEnabled; bool IsFocused; bool IsSelected; double Opacity;
  List<string>? Traits; ElementStateInfo State;         // Displayed/Enabled/Selected/Focused/Opacity
  BoundsInfo? Bounds; BoundsInfo? WindowBounds;         // X, Y, Width, Height (double)
  List<string>? Gestures; List<string>? StyleClass; ElementStyleInfo? Style;
  string? NativeType; Dictionary<string,string?>? NativeProperties; ElementNativeViewInfo? NativeView;
  Dictionary<string,string?>? FrameworkProperties; List<ElementInfo>? Children;
}
```

Useful assertions beyond visibility: `Bounds` (layout checks — Magnet/ViewBox!), `Text`,
`NativeType`/`NativeProperties` (verify the native control a handler produced),
`Children` count (materialized VirtualScroll items).
