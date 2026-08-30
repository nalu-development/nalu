# Live Activities

`Nalu.Maui.LiveActivities` shows your app's **live, glanceable state on the system surfaces**
of both platforms from one C# API:

- **iOS**: a real ActivityKit **Live Activity** — Lock Screen banner plus the Dynamic Island
  (compact chip, expanded card) — rendered by a widget extension the package **builds and
  embeds automatically** (you write no Swift and touch no Xcode).
- **Android 16+**: a **Live Update** — the promoted ongoing notification with the status-bar
  chip and the floating card. Android 8–15 degrades gracefully to a plain ongoing
  notification with a classic progress bar and chronometer.

The design principle is a **semantic content model**: you describe *what the activity says*
— title, chip, progress, a ticking timer, actions — and each platform renders it natively in
its own visual language. Both platforms end up with the same information in the same roles,
because the model *is* the intersection of what they can render.

```csharp
var activity = await liveActivities.StartAsync("delivery", new LiveActivityContent
{
    Title = "Pizza on the way",
    Subtitle = "Preparing your order",
    ChipText = "10%",
    AccentColor = "#4C7DF0",
    Progress = new LiveActivityProgress { Value = 0.1 },
    Timer = LiveActivityTimer.CountDown(order.Eta),
});

await activity.UpdateAsync(c =>
{
    c.Subtitle = "On the way";
    c.ChipText = "60%";
    c.Progress!.Value = 0.6;
});

await activity.EndAsync(c =>
{
    c.Title = "Delivered";
    c.Subtitle = "Enjoy!";
    c.Progress = null;
    c.Timer = null;
});
```

## Setup

```csharp
builder.UseNaluLiveActivities(live => live
    .AddKind("delivery", "Order tracking")   // Android notification channel name
);
```

Resolve `ILiveActivityManager` from DI wherever you need it.

That is the whole setup on iOS: the NuGet package injects `NSSupportsLiveActivities` into
your compiled `Info.plist` and builds + embeds a generic WidgetKit extension rendering the
content model — Lock Screen, Dynamic Island, everything. On Android, the package's manifest
declares the notification permissions; you only trigger the runtime prompt:

```csharp
var allowed = await liveActivities.RequestPermissionAsync();
```

> [!NOTE]
> On Android this requests `POST_NOTIFICATIONS` (Android 13+). On iOS there is no runtime
> prompt — the user controls Live Activities per app in Settings, and the call simply
> reflects that switch.

### Support levels

```csharp
switch (liveActivities.Support)
{
    case LiveActivitySupport.Full:        // iOS 16.2+ · Android 16+ (chip + floating card)
    case LiveActivitySupport.Degraded:    // Android 8–15: plain ongoing notification, no chip
    case LiveActivitySupport.Unavailable: // iOS < 16.2, Mac Catalyst, Windows, or user-disabled
}
```

Calls are **never** platform-branched in your code: on `Unavailable` surfaces `StartAsync`
returns an inert handle and every call is a no-op, so the same code path runs everywhere.
Set `DisableAndroidFallback = true` in the options if the chip is essential to your feature
and a chip-less notification would mislead.

## The API shape

The handle is **write-mostly** and every mutation goes through a *patch lambda*:

```csharp
public interface ILiveActivity
{
    string Id { get; }
    string Kind { get; }
    LiveActivityState State { get; }          // Active, Stale, Ended
    ILiveActivityContent Content { get; }     // read-only view of the last applied snapshot

    Task UpdateAsync(Action<LiveActivityContent> patch, LiveActivityAlert? alert = null);
    Task EndAsync(Action<LiveActivityContent>? finalPatch = null,
                  LiveActivityDismissal dismissal = LiveActivityDismissal.Default);
}
```

Why lambdas instead of passing content objects around:

- The library deep-clones the current snapshot into a **draft**, runs your patch on it under
  the handle's lock, and applies the result. Reading and writing happen on the freshest
  state, concurrent updates serialize cleanly, and the patch must be synchronous.
- A patch that produces **identical content is skipped entirely** — both OSes budget
  live-activity updates, so no-op suppression is built in.
- `Content` is the read-only view of the last applied snapshot (its main use is
  [reconciliation](#activities-outlive-your-app)); your own model stays the source of truth.

Updates are **silent by default**. Pass an alert to draw attention (iOS Live Activity alert,
Android re-notify):

```csharp
await activity.UpdateAsync(
    c => c.Subtitle = "Driver is at the door",
    new LiveActivityAlert("Driver arrived", "Meet them at the door"));
```

Ending mirrors iOS semantics on both platforms: the **default dismissal keeps the final
content visible** for a while (Android converts it to a regular swipeable notification),
while `LiveActivityDismissal.Immediate` removes it instantly.

## The content model, mapped

| Property | Android (Live Update) | iOS (widget) |
|---|---|---|
| `Title` / `Subtitle` | notification title / text | card headline / secondary line |
| `ChipText` | status-bar chip text (`setShortCriticalText`) | Dynamic Island compact pill; expanded-card corner caption |
| `ChipIcon` | — (chip shows the small icon) | identity glyph (SF Symbol name) in card + minimal island |
| `AccentColor` | small-icon tint + progress bar color | progress track + identity glyph tint — **nothing else**, see below |
| `ImageName` | large icon (drawable name) | — (reserved) |
| `Progress` | `ProgressStyle` bar: segments, points, tracker icon | segmented capsule track with milestone dots |
| `Timer` | native chronometer (header) | native ticking text (`Text(timerInterval:)`) |
| `DeepLink` | tap intent | `widgetURL` |
| `Actions` | notification action buttons | capsule `Link` buttons |
| `StaleAt` | *(marks handle `Stale` on rehydration)* | ActivityKit `staleDate` — also the [zero-crossing trigger](liveactivities-timers.md#when-the-countdown-reaches-zero) |
| `Custom` | ignored | forwarded verbatim to [custom widget UIs](#custom-ios-ui) |

Keep `ChipText` under ~7 characters ("12 min", "3–2", "60%") — it lives in the tiny
always-visible surface on both platforms.

### Progress with steps

Yes — progress is not just a fraction. The model adopts Android 16's `ProgressStyle` shape,
and the iOS widget renders the same structure:

- **`Segments`** — weighted, individually colorable stretches of the bar (phases of a
  journey: *preparing · driving · delivering*);
- **`Points`** — milestone markers at positions along the bar, filled once passed;
- **`TrackerIcon`** — an icon travelling with the progress (Android only);
- **`Indeterminate`** — a waiting bar while the real extent is unknown.

See the [delivery example](liveactivities-examples.md#delivery-with-steps) for the full
pattern.

### Colors are deliberately constrained

Android's Live Update is a **system template**: text, chip and card background are always
system-colored (and adapt to dark/light on their own); the app only colors the progress bar
and tints its identity icon. The iOS widget **enforces the same contract** — `AccentColor`
applies to the progress track and the identity glyph, nothing else — so the customization
surface is identical on both platforms and both inherit dark/light adaptivity from the
system. If you need more than that on iOS, bring [your own widget UI](#custom-ios-ui).

## Activities outlive your app

This is a feature, not a leak: a delivery keeps its Live Activity when the app dies, on both
platforms. The consequence: **reconcile on startup**. `ILiveActivityManager.Activities`
rehydrates surviving activities (from ActivityKit on iOS, from the notification's own extras
on Android) — adopt yours instead of starting a duplicate next to it:

```csharp
var existing = liveActivities.Activities
    .LastOrDefault(a => a.Kind == "delivery" && a.State != LiveActivityState.Ended);

if (existing is not null)
{
    _activity = existing;                                 // continue updating it
    _progress = existing.Content.Progress?.Value ?? 0;    // restore what you need
}
```

An adopted handle is fully functional — `UpdateAsync` continues the same notification /
activity seamlessly.

## Custom iOS UI

The bundled widget is a deliberately clean default. When your activity deserves bespoke
SwiftUI, copy the package's widget project and point the build at it:

```xml
<PropertyGroup>
  <NaluLiveActivitiesWidgetProject>$(MSBuildProjectDirectory)/MyWidget/MyWidget.xcodeproj</NaluLiveActivitiesWidgetProject>
</PropertyGroup>
```

Rules of the road: keep the `NaluLiveActivityAttributes` struct **exactly as shipped**
(ActivityKit matches activities to widgets by that type), decode the same JSON payload, and
switch layouts on `kind`. The `Custom` dictionary travels untouched for anything your UI
needs beyond the standard model. Other knobs: `NaluLiveActivitiesWidget=false` disables the
automatic widget entirely; `NaluLiveActivitiesWidgetBundleSuffix` and
`...WidgetDisplayName` tune identity.

> [!IMPORTANT]
> Device and App Store builds need the usual per-bundle-id provisioning for the widget
> extension (`$(ApplicationId).widget` by default) on the Apple developer portal — that is
> an Apple requirement for any app extension, not something the package can automate.

## Actions

Actions are **deep links** in v1 — a label, an optional icon, and a URL your app handles:

```csharp
Actions =
[
    new LiveActivityAction { Id = "track", Label = "Track", Icon = "location.fill", DeepLink = "myapp://track" },
    new LiveActivityAction { Id = "help",  Label = "Help",  DeepLink = "myapp://help" },
]
```

Both platforms render them as buttons; tapping opens the app at the link (register the
scheme: `CFBundleURLTypes` on iOS, an `IntentFilter` on Android). There are no in-process
callbacks yet: an action with an `Id` but **no `DeepLink` is valid to declare today but not
rendered** — it is reserved for the upcoming direct-callback support, which will report taps
back through that `Id` without opening the app.

## Where to next

- [Timers](liveactivities-timers.md) — the OS ticks, you don't: count-downs, count-ups,
  pausing, and what happens at zero.
- [Use cases](liveactivities-examples.md) — delivery with steps, appointments that run
  over, workouts, downloads, live scores.
