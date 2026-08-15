---
name: nalu-navigation-lifecycle
description: Nalu page-model lifecycle (IEnteringAware/IAppearingAware/IDisappearingAware/ILeavingAware/ILeavingGuard/IDisposable), call order, DI scope, restore, unit tests; load when placing load/save code.
---
# Nalu navigation lifecycle

Package `Nalu.Maui.Navigation`, namespace `Nalu` (already `global using`). Every navigated page gets its own
DI scope; the engine calls async lifecycle methods on ONE target per page: the explicitly assigned
`BindingContext` (your page model in `PageModels/`) — or the page itself when no model is assigned.
All callbacks are `ValueTask`, awaited by the engine, run on the UI thread, and block the navigation
until they complete. Building navigations / passing intents → skill `nalu-navigation`.

## Quick reference

### Where does my code go?

| I need to… | Implement | Fires |
|---|---|---|
| Read the intent, seed state that must be ready before the page shows (fast, cached, < 30 ms) | `IEnteringAware` / `IEnteringAware<TIntent>` → `OnEnteringAsync([intent])` | Once per stack entry, before the push animation |
| Load slow data (network/DB) behind a spinner; refresh whenever the page is shown again | `IAppearingAware` → `OnAppearingAsync()` | After the animation, and again every time a child pops back to it or its tab is reselected |
| Receive the result of a popped child (`Nav.Pop(result)`) | `IAppearingAware<TResult>` → `OnAppearingAsync(result)` | When the child pops |
| Pause timers/streams while covered (kept for a possible return) | `IDisappearingAware` → `OnDisappearingAsync()` | Before push of a child, before pop, before a tab switch |
| Save drafts, cancel work, unsubscribe when the page is removed | `ILeavingAware` → `OnLeavingAsync()` | Once, right after Disappearing, before removal |
| Block "back"/pop with a confirm dialog | `ILeavingGuard` → `ValueTask<bool> CanLeaveAsync()` | Before the page is removed; `false` stops the navigation |
| Dispose ctor-created resources (timers, `HttpClient`) | `IDisposable.Dispose()` | After the pop animation, when the DI scope is disposed |
| Startup redirect / auto-navigate | `OnAppearingAsync` + `IDispatcher.DispatchAsync` | Never navigate inline from a callback |
| Share state with pages pushed above | `OnEnteringAsync` + `INavigationServiceProvider.AddNavigationScoped<T>(x)` | Scope lives while this page is in the stack |
| Keep a page out of restore / swap its restore intent | `INavigationRestore.ForgetAsync()` / `RestoreWithIntentAsync(intent)` in `OnEnteringAsync` | Persisted before the task completes |

Pairing rule: constructor ↔ `Dispose`; `OnEnteringAsync` ↔ `OnLeavingAsync` (stack lifetime);
`OnAppearingAsync` ↔ `OnDisappearingAsync` (visibility lifetime, may repeat).

### Order of calls (default behaviors)

| Navigation | Sequence |
|---|---|
| Push A → B | `A.Disappearing` → B created (new scope) → `B.Entering(intent)` → animation → `B.Appearing(intent)` |
| Pop B → A | `B.CanLeave` (if guard) → `B.Disappearing` → `B.Leaving` → animation → `A.Appearing(resultIntent)` → `B` scope disposed (`Dispose`) |
| `Pop().Pop()` [A,B,C] | `C.CanLeave` → `C.Disappearing/Leaving` → `B.Appearing` (only if B has a guard, to show its prompt) → `B.CanLeave` → `B.Disappearing/Leaving` → `A.Appearing`; C and B disposed |
| `Push<B>().Push<C>()` | `A.Disappearing` → `B.Entering` → `C.Entering` → `C.Appearing` (B never appears) |
| `Root<T>()` to another root, same `ScaffoldArea` (stack kept) | old top `.Disappearing` → target root `.Entering` (first time only) → top of target stack `.Appearing`. No guards, no Leaving |
| `Root<T>()` to a root in another `ScaffoldArea` | every page of every stack in the left area: guard → Disappearing → Leaving → disposed; then target Entering/Appearing |
| Tap the active tab | relative pops to root (see Pop) |
| App start | initial root: `Entering` (must complete synchronously) → `Appearing` → restore replay if pending |

Intents: the typed handler runs when the runtime intent type is assignable to `TIntent`, else the untyped one
(default `NavigationIntentBehavior.Fallthrough`; `Strict` = untyped never runs with an intent present). An intent
whose target implements neither `IEnteringAware<T>` nor `IAppearingAware<T>` throws `InvalidOperationException`.

## Patterns

Fast intent seed + slow refresh + visibility-scoped subscription:

```csharp
public partial class DetailPageModel(IItemService items, IDispatcher dispatcher)
    : ObservableObject, IEnteringAware<DetailIntent>, IAppearingAware, IDisappearingAware, ILeavingAware, IDisposable
{
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private Item? _item;
    private int _id;
    private IDisposable? _liveUpdates;
    private readonly CancellationTokenSource _leaving = new();

    public ValueTask OnEnteringAsync(DetailIntent intent) { _id = intent.Id; return ValueTask.CompletedTask; }

    public async ValueTask OnAppearingAsync()
    {
        _liveUpdates = items.Subscribe(_id, OnChanged);      // visibility lifetime
        IsLoading = true;
        try { Item = await items.GetAsync(_id, _leaving.Token); }
        catch (OperationCanceledException) { }
        finally { IsLoading = false; }
    }

    public ValueTask OnDisappearingAsync() { _liveUpdates?.Dispose(); _liveUpdates = null; return ValueTask.CompletedTask; }
    public ValueTask OnLeavingAsync() { _leaving.Cancel(); return ValueTask.CompletedTask; }   // stack lifetime
    public void Dispose() => _leaving.Dispose();                                                // page lifetime
}
```

Guard with dirty tracking (skip when saving):

```csharp
public partial class EditPageModel(INavigationService navigation, INavigationServiceProvider navProvider) : ObservableObject, ILeavingGuard
{
    private bool _saving;
    public bool IsDirty { get; private set; }

    public async ValueTask<bool> CanLeaveAsync()
        => _saving || !IsDirty
           || await navProvider.ContextPage.DisplayAlertAsync("Discard changes?", "", "Discard", "Stay"); // ContextPage = this model's page

    [RelayCommand]
    private async Task SaveAsync()
    {
        _saving = true;
        try { await PersistAsync(); await navigation.GoToAsync(Nav.Pop()); }
        finally { _saving = false; }
    }
}
```

Redirect from a callback (startup/auth) — dispatch, never inline:
`_ = dispatcher.DispatchAsync(() => navigation.GoToAsync(Nav.Absolute(NavigationBehavior.Immediate).Root<LoginPageModel>()));`

Restore opt-in (`MauiProgram.cs`) and per-page control:

```csharp
.UseNaluNavigation<App>(nav => nav.AddPages())
.UseNaluNavigationRestore(restore =>
{
#if !DEBUG
    restore.Enabled = false;                    // e.g. dev-only convenience
#endif
    restore.MaxAge = TimeSpan.FromHours(12);
    restore.AddIntents();                       // source-generated: all IEnteringAware<T>/IAppearingAware<T> intents
})
// page model: never resurrect a wizard step
public ValueTask OnEnteringAsync() => new(restore.ForgetAsync());   // INavigationRestore restore injected
```

Unit test (xunit + NSubstitute; page models are plain classes — call the callbacks directly):

```csharp
var nav = Substitute.For<INavigationService>();
nav.GoToAsync(Arg.Any<INavigationInfo>()).Returns(true);
var vm = new HomePageModel(nav);
await vm.OnEnteringAsync(new DetailIntent(42));                    // lifecycle = ordinary method
await vm.OpenDetailCommand.ExecuteAsync(null);
await nav.Received(1).GoToAsync(Arg.Is<INavigationInfo>(n => n.Matches(Nav.Push<DetailPageModel>(new DetailIntent(42)))));
```

## Rules & gotchas

- Callbacks block navigation: `OnEnteringAsync` runs before the animation — keep it < 30 ms. Slow work goes
  in `OnAppearingAsync` with an `IsLoading` flag, or fire-and-forget from `OnEnteringAsync` with a CTS
  cancelled in `OnLeavingAsync` (not `OnDisappearingAsync`: a child push must not cancel it). Never
  `.Wait()`/`.Result` inside callbacks.
- The initial root's `OnEnteringAsync` MUST complete synchronously (`NotSupportedException` otherwise);
  do startup work in its `OnAppearingAsync`.
- Never call `GoToAsync` inside a callback or guard (throws `InvalidNavigationException`; the flag flows
  through `Task.Run` too). Dispatch it (`IDispatcher.DispatchAsync`) from `OnAppearingAsync`, which runs after
  the navigation is committed; a navigation dispatched from `OnEnteringAsync` may be ignored (`false`).
- Exceptions thrown by a callback propagate out of `GoToAsync` (event `NavigationFailed`); scopes already
  marked for disposal are still disposed. Catch inside the callback for recoverable failures.
- `ILeavingGuard` runs only when the page is about to be REMOVED (pop, tap-active-tab pop-to-root, area
  change): a push over it, or a tab switch that keeps the stack, does not ask. In multi-segment navigations
  earlier segments already applied are not rolled back when a later guard says `false`; `GoToAsync` returns
  `false` (`NavigationCanceled`). `NavigationBehavior.IgnoreGuards` skips guards.
- `OnAppearingAsync` also fires when returning from a child and on tab reselection — make it idempotent.
  Intermediate pages of one navigation never appear.
- Lifecycle target = explicit `BindingContext` only (assigned in the page ctor). Interfaces on the page are
  ignored while a model is assigned; an inherited binding context never counts.
- DI: page, model and `AddScoped` services live per navigated page; a child page's scope is separate.
  Implement `IDisposable` (not only `IAsyncDisposable`: the scope is disposed synchronously), after the pop
  animation. Unsubscribe events in `OnLeavingAsync`, no statics holding pages; the leak detector
  (`EnabledWithDebugger` default) alerts when a popped page is not collected.
- Only `IEnteringAware<T>` / `IAppearingAware<T>` see intents; a pop result reaches the revealed page's
  `IAppearingAware<TResult>`. Declare a typed interface per intent type you send.
- Restore: capture is automatic once `UseNaluNavigationRestore` is called; a page reached with an
  UNREGISTERED intent ends the restorable stack. Register via generated `restore.AddIntents()` (this assembly)
  or `restore.AddIntent<T>("id")`; `[AutoNavigationIntent("id")]` fixes the wire id,
  `[AutoNavigationIntent(Enabled = false)]` opts out; `AwaitableIntent`s are never restorable.
  Non-serializable members: `[JsonIgnore]` + `IIntentHydrator<TIntent>` on an already-restored page.
  While a boot restore is pending, non-replay navigations return `false`; call
  `INavigationRestore.TryStopRestoreAsync()` first when auth/deep-link must win. View state is never restored.
- Unit tests: no test double ships. Mock `INavigationService` (single `GoToAsync`), assert with
  `INavigationInfo.Matches(...)`, `IsAbsolute`, `Count`, `n[0].Type`, `n[0] is NavigationPop`. `Nav` builders
  are `BindableObject`s: the test project must reference MAUI (see reference.md). Awaitable intents complete
  only when the engine disposes the target page — `await intent` / `ResolveIntentAsync` never finish against
  a mock; wrap them behind an app-owned interface.

Per-navigation tables, restore internals, `NavigationEvent` monitoring, testing recipes: read `reference.md`.

## See also

- `nalu-navigation` — Nav builder, intents, `AddPages()`, behaviors, tab roots.
- `nalu-scaffold-transitions` — animations/gestures that trigger the pops described here.
