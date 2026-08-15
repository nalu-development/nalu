# Nalu navigation lifecycle — reference

Read when SKILL.md does not cover the case: exact sequences per navigation kind, restore internals,
event monitoring, DI scope details, unit-test recipes.

## 1. Interfaces (namespace `Nalu`)

| Interface | Member | Notes |
|---|---|---|
| `IEnteringAware` | `ValueTask OnEnteringAsync()` | Once per stack entry (engine tracks an `Entered` flag; re-sends are no-ops) |
| `IEnteringAware<in TIntent>` | `ValueTask OnEnteringAsync(TIntent intent)` | Contravariant; matched by assignability of the runtime intent type |
| `IAppearingAware` | `ValueTask OnAppearingAsync()` | Skipped if the page is already "appeared" |
| `IAppearingAware<in TIntent>` | `ValueTask OnAppearingAsync(TIntent intent)` | Receives push intents on the target and pop result intents on the revealed page |
| `IDisappearingAware` | `ValueTask OnDisappearingAsync()` | Skipped if the page is not currently "appeared" |
| `ILeavingAware` | `ValueTask OnLeavingAsync()` | Once, only if the page had entered |
| `ILeavingGuard` | `ValueTask<bool> CanLeaveAsync()` | Evaluated only when the page is about to be removed |
| `IDisposable` | `Dispose()` | Called by the DI scope disposal, after the navigation's animation completes |
| `IIntentHydrator<in TIntent>` | `ValueTask HydrateAsync(TIntent intent)` | Restore only: fills `[JsonIgnore]` members before the intent is replayed |
| `INavigationRestore` | `ForgetAsync()`, `RestoreWithIntentAsync(object)`, `TryStopRestoreAsync()` | Injectable everywhere; inert when restore is off |

Target resolution: `page.IsSet(BindingContextProperty) ? page.BindingContext ?? page : page`. Only one
handler runs per event per page. In DEBUG builds the engine writes `Entering/Appearing/... <Type>` to the console.

Intent handler lookup: any instance method named `OnEnteringAsync`/`OnAppearingAsync` returning `ValueTask`
with one parameter the intent is assignable to (first match wins). If none matches: `Fallthrough` (default)
runs the untyped method; `Strict` runs nothing. `NavigationHelper` asserts before dispatch that the target
implements a typed Entering OR Appearing interface for the intent — otherwise `InvalidOperationException`
("must implement either IEnteringAware<T> or IAppearingAware<T> to receive intent"). Only the LAST segment
of a navigation carries the intent (`Push<A>().Push<B>().WithIntent(x)` → B).

## 2. Sequences per navigation kind

Legend: `D` = Disappearing, `E` = Entering, `A` = Appearing, `L` = Leaving, `G` = CanLeaveAsync,
`X` = scope disposed (Dispose) — always after the animation, in the finally block of the navigation.

| Navigation | Sequence |
|---|---|
| Push A → B (`Nav.Push<B>(i)`) | A.D → B ctor (own scope, parent scope = A) → B.E(i) → push/animate → commit → B.A(i) |
| Pop B → A (`Nav.Pop(r)`) | B.G → B.D → B.L → pop → commit → A.A(r) → B.X |
| Pop when B has no guard | B.D → B.L → pop → commit → A.A(r) → B.X |
| `Pop().Pop()` on [A,B,C], B guarded | C.G → C.D → C.L → commit (C gone) → B.A → B.G; if `false`: stop, return `false` (C stays popped, disposed). Else B.D → B.L → A.A → C.X, B.X |
| `Push<B>().Push<C>()` | A.D → B.E → C.E → C.A (B has Entered but never Appeared) |
| `Pop().Push<D>()` (replace) | B.G → B.D → B.L → D.E → D.A → B.X |
| `Root<T>()` same root already selected | Converted to relative pops (like `Pop()` × n) |
| `Root<T>()` other root, same area, default behavior | current top.D → target root page ctor+E (first visit only) → select root → top of the target's preserved stack.A (intent only if that top is the root page) |
| `Root<T>()` other root with `PopAllPagesOnSectionChange` | for the leaving root: pops (G/D/L each) → root page: G → D → L → disposed; then target E/A |
| `Root<T>()` root in another area (default `PopAllPagesOnItemChange`) | for every root of the left area with a live page (current first): pops (G/D/L each) → root page G/D/L → disposed; then target root E (first time) → A |
| `Root<T>().Add<U>()` | as above, then relative pushes on the target stack; U.A |
| Modal pages on the stack during any absolute navigation | modals popped first (with guards) before the root switch |
| App start (`Scaffold.InitialRootPageType`) | root page E (MUST be synchronous) → A → pending restore replay |
| Guard blocked (`false`) | remaining segments skipped; `NavigationCanceled` event; `GoToAsync` returns `false` |
| Callback throws | `NavigationFailed` event (`Data` = exception), exception rethrown to the `GoToAsync` caller; dispose bag still processed |
| Queued navigation whose start state moved | `NavigationIgnored` event; returns `false`; no callbacks |

Notes:
- The commit (visual push/pop) happens BEFORE `OnAppearingAsync`; that is why a navigation dispatched from
  Appearing is safe while one dispatched from Entering can be ignored.
- The intermediate `B.A` before `B.G` exists so a guard prompt can be shown on a visible page.
- `Nav.Absolute(NavigationBehavior.Immediate)` skips the tap-feedback delay only; sequences are unchanged.
- Predictive/edge back gestures and the nav-bar back button route through the same engine, so `G` runs there too.

## 3. Threading and re-entrancy (lifecycle view)

- All callbacks run on the UI thread inside the navigation flow. `await` freely; do not block.
- A `GoToAsync` issued in the same async flow as a running navigation throws
  `InvalidNavigationException` ("Cannot trigger a navigation from within a navigation"). Detection flows via
  `AsyncLocal`, so `Task.Run(() => nav.GoToAsync(...))` from a callback throws too. Break the flow with
  `IDispatcher.DispatchAsync(...)`/`DispatchDelayed`.
- Prefer redirects from `OnAppearingAsync`; from `OnEnteringAsync` the location is not committed yet.

## 4. Monitoring events

`Scaffold.NavigationEvent` (`AppScaffold` derives from `Scaffold`) raises `NavigationLifecycleEventArgs`:

| Member | Content |
|---|---|
| `EventType` (`NavigationLifecycleEventType`) | `NavigationRequested`, `NavigationCompleted`, `NavigationCanceled`, `NavigationIgnored`, `NavigationFailed`, `Entering`, `Leaving`, `Appearing`, `Disappearing`, `LeavingGuard` |
| `Target` | For `Navigation*`: a `NavigationLifecycleInfo` (`Navigation`, `RequestedNavigation`, `TargetState`, `CurrentState`); for page events: the lifecycle target (page model or page) |
| `Handling` (`NavigationLifecycleHandling`) | `NotHandled` / `Handled` / `HandledWithIntent` — whether the target implemented the interface |
| `Data` | The intent for Entering/Appearing, the exception for `NavigationFailed` |

```csharp
public AppScaffold()
{
    InitializeComponent();
#if DEBUG
    NavigationEvent += (_, e) => System.Diagnostics.Debug.WriteLine($"[NAV] {e.EventType} {e.Target.GetType().Name} {e.Handling}");
#endif
}
```

## 5. DI scope and navigation-scoped services

- `CreatePage` = `ServiceProvider.CreateScope()` → resolves the PAGE from the scope (the model is resolved as
  its ctor dependency, so both are scoped instances). Scoped services (`builder.Services.AddScoped<...>`)
  are per navigated page; singletons are app-wide; transients are new per resolution.
- The scope is disposed via `IServiceScope.Dispose()` (synchronous) when the page is removed; MS DI throws for
  services implementing `IAsyncDisposable` without `IDisposable`.
- Disposal also disconnects handlers and hands the page to the leak detector
  (`nav.WithLeakDetectorState(NavigationLeakDetectorState.Disabled|EnabledWithDebugger|Enabled)`), which
  alerts if the page is not collected after a delay. Usual culprits: event subscriptions not removed in
  `OnLeavingAsync`, timers not disposed, static references, background tasks capturing `this` without a CTS.
- `INavigationServiceProvider` (inject in page/model ctor; scoped, `IServiceProvider` + `IDisposable`):
  `AddNavigationScoped<T>(instance)` publishes to this page and all pages pushed above it; `GetService`/
  `GetRequiredService<T>()` walk parent pages only (not the container); `ContextPage` is the owning page.
  Nested scopes compose (child adds its own). Instances registered this way are disposed with the owning
  page's scope if they implement `IDisposable`.

## 6. State restoration

Enable: `.UseNaluNavigationRestore(Action<NavigationRestoreOptions>? configure = null)` next to
`UseNaluNavigation` (order irrelevant; throws at build if navigation is not configured).

`NavigationRestoreOptions`: `Enabled` (default `true`), `MaxAge` (`TimeSpan?`, older snapshots discarded),
`IntentSerializerContext` (`JsonSerializerContext?` for NativeAOT; every registered intent must be in it),
`AddIntent<T>(string? typeId = null)` (id defaults to `T.Name`; duplicate id → `InvalidOperationException`),
generated `AddIntents()` (registers every intent type received through `IEnteringAware<T>` /
`IAppearingAware<T>` in this assembly, honoring `[AutoNavigationIntent]`; `AwaitableIntent` subclasses are
skipped; two intents with the same short name → compile error NALU0005 until one gets an explicit id).

`[AutoNavigationIntent(string? typeId = null) { Enabled = true }]` on class/struct intents:
`[AutoNavigationIntent("product-detail")]` stable id; `[AutoNavigationIntent(Enabled = false)]` never replay.

Capture (automatic after every successful navigation, debounced, flushed on app background): current root
(+ its intent), the pushed stack, and each page's ENTERING intent serialized at navigation time (JSON via
`IIntentSerializer`, default System.Text.Json reflection). A page is restorable when reached with no intent or
with a registered intent; an unregistered/unserializable intent truncates the restorable stack at that page.
Pop result intents are never captured. Modal pages restore as normal stack entries.

Boot: snapshot read AND deleted → validated (schema, app version/build, route-table hash, `MaxAge`) → the
configured initial root boots normally → after its first `OnAppearingAsync` the replay runs: one navigation
selecting the captured root (with intent), then chunked pushes so each intent rides its own navigation
(regular Entering/Appearing, animations included) → snapshot re-captured. Fail-open: unknown segment/intent
truncates; any error discards and boots normally. Runs once per process.

Suppression window: while the replay is pending/in flight, navigations not issued by it return `false` and
raise `NavigationIgnored` — including ones a restored page dispatches from its lifecycle. The window lifts
before the LAST replayed destination. `INavigationRestore.TryStopRestoreAsync()` returns `true` if it dropped
a pending/in-flight restore; call it before an auth redirect or deep link and then navigate normally.

Per-page controls (deduce the page from the running lifecycle callback, else the current page; both persist
before returning): `ForgetAsync()` — exclude this page instance and everything above (on a root: that whole
root for the session); `RestoreWithIntentAsync(intent)` — set/replace the intent replayed for this page
(throws for unregistered/unserializable types).

Hydration: intents may carry `[JsonIgnore]` members; before replaying such an intent the engine walks the
already-restored stack top→root and awaits the first `IIntentHydrator<TIntent>.HydrateAsync(intent)` found.

Customization via DI: replace `IIntentSerializer` (`Serialize(object)`, `Deserialize(Type, string)`) or
`INavigationRestoreStore` (`ReadAndDelete()`, `WriteAsync(string, CancellationToken)`; default = JSON file in
the cache directory).

Not restored: view state (scroll, entry text), forgotten pages and anything above them, non-current roots'
stacks, transient overlays (popups/sheets).

Deep links: parse yourself, then `await restore.TryStopRestoreAsync(); await navigation.GoToAsync(Nav.Root<X>().Add<Y>().WithIntent(...))`.
On cold start stash the URI and run the handler after the initial root's first appearing (dispatched).

## 7. Unit testing page models

Setup: a `net10.0` xunit project referencing the app project. The app csproj needs a plain TFM for that:
add `net10.0` to `TargetFrameworks` and guard `<OutputType Condition="'$(TargetFramework)' != 'net10.0'">Exe</OutputType>`
(and `#if ANDROID/IOS` around platform code). `Nav.*` builders derive from `BindableObject`, so MAUI must be
referenced (`UseMaui` on the app project provides it). Packages: `xunit.v3`, `NSubstitute`, optionally
`FluentAssertions`. Global usings are per project: add `global using Nalu; global using Nav = Nalu.Navigation;`
to the test project too. No Nalu test double is shipped; page models are plain classes.

Recipes:

```csharp
// 1. Navigation issued by a command
var nav = Substitute.For<INavigationService>();
nav.GoToAsync(Arg.Any<INavigationInfo>()).Returns(true);
var vm = new HomePageModel(nav);
await vm.OpenDetailCommand.ExecuteAsync(null);
await nav.Received(1).GoToAsync(Arg.Is<INavigationInfo>(n => n.Matches(Nav.Push<DetailPageModel>())));
// Structural checks: n.IsAbsolute, n.Count, n[0].Type == typeof(DetailPageModel), n[0] is NavigationPop,
// n.Intent is DetailIntent { Id: 42 }, n.Behavior == NavigationBehavior.IgnoreGuards
// Custom intent equality: n.Matches(expected, (DetailIntent a, DetailIntent b) => a.Id == b.Id)

// 2. Lifecycle callbacks are ordinary methods
var items = Substitute.For<IItemService>();
items.GetAsync(42, Arg.Any<CancellationToken>()).Returns(new Item(42));
var detail = new DetailPageModel(items, dispatcher);
await detail.OnEnteringAsync(new DetailIntent(42));
await detail.OnAppearingAsync();
Assert.Equal(42, detail.Item!.Id);
await detail.OnDisappearingAsync(); await detail.OnLeavingAsync(); detail.Dispose();   // full pass, no throw

// 3. Guards
var edit = new EditPageModel(nav); edit.MarkDirty();
Assert.False(await edit.CanLeaveAsync());          // inject the confirm dialog behind an interface for testability

// 4. Dispatched navigation from OnAppearingAsync — DispatchAsync is an extension over IDispatcher.Dispatch(Action):
//    make Dispatch run the action inline, then let the continuation flush
var dispatcher = Substitute.For<IDispatcher>();
dispatcher.Dispatch(Arg.Any<Action>()).Returns(ci => { ci.Arg<Action>()(); return true; });
await new StartupPageModel(nav, dispatcher, session).OnAppearingAsync();
await nav.Received().GoToAsync(Arg.Is<INavigationInfo>(n => n.IsAbsolute && n[0].Type == typeof(LoginPageModel)));

// 5. Result intents: assert the pop navigation carries the result
await selection.SelectCommand.ExecuteAsync(contact);
await nav.Received().GoToAsync(Arg.Is<INavigationInfo>(n => n[0] is NavigationPop && n.Intent is ContactSelected c && c.Contact == contact));
```

Awaitable intents (`AwaitableIntent<T>`): `SetResult`/`SetException` only store; the task completes when the
engine disposes the target page (internal controller). Against a mocked `INavigationService`, `await intent`
and `ResolveIntentAsync` never complete and `ResolveIntentAsync` is an extension method (not mockable). Test
the callee by asserting `SetResult` inputs through your own seam (e.g. keep the result computation in a
testable method) and the caller by hiding the resolve call behind an app-owned interface you substitute.

Guard dialogs: `Application.Current.Windows[0].Page.DisplayAlert` is untestable — inject an `IDialogService`.
Do not `await` real `Task.Delay`/timers in callbacks under test; inject `TimeProvider`.
