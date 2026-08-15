# Nalu navigation — reference

Companion to `SKILL.md`. Namespace `Nalu`; `Nav` = `Nalu.Navigation` (global alias in `GlobalUsings.cs`).

## 1. Navigation grammar

A navigation is an `INavigationInfo`: `IsAbsolute`, `Behavior` (`NavigationBehavior?`), `Intent`
(`object?`), `Path` (string form, e.g. `DetailPage/..` or `//SettingsPage/DetailPage`) and an ordered
list of `INavigationSegment`s. `Navigation.ToString()` prints `Path?(IntentTypeName)`.

Entry points on `Nav`:

| Call | Returns | Then |
|------|---------|------|
| `Nav.Relative(NavigationBehavior? b = null)` | `IRelativeNavigationInitialBuilder` | `.Push<T>()` or `.Pop()` |
| `Nav.Absolute(NavigationBehavior? b = null)` | `IAbsoluteNavigationInitialBuilder` | `.Root<T>()` or `.Root<T>("custom-route")` |
| `Nav.Push<T>()` | `IRelativeNavigationPushOnlyBuilder` | `.Push<T2>()…`, `.WithIntent(o)` |
| `Nav.Push<T>(object intent)` | `INavigationInfo` (chain ended) | — |
| `Nav.Pop()` | `IRelativeNavigationBuilder` | `.Pop()…`, `.Push<T>()…`, `.WithIntent(o)` |
| `Nav.Pop(object intent)` | `INavigationInfo` | — |
| `Nav.Root<T>()` | `IAbsoluteNavigationBuilder` | `.Add<T2>()…`, `.WithIntent(o)` |
| `Nav.Root<T>(object intent)` | `INavigationInfo` | — |

Rules enforced by the builders (throw `InvalidOperationException`):

- Absolute navigations never contain pops (`Nav.Root<A>()` has no `.Pop()`).
- In a relative navigation all pops come first: `Pop().Pop().Push<A>().Push<B>()` is valid;
  `Push<A>().Pop()` is not (the push-only builder simply has no `Pop`).
- `WithIntent(...)` is terminal — the intent is delivered to the lifecycle target of the LAST
  segment: the last pushed page for pushes/adds, the revealed page for a pure pop chain, the root
  page for a bare `Root<T>()`.
- `T` in `Push/Root/Add` is either a registered page model type (mapped to its page) or a page type
  (`Page` subclass, registered view-only or with a model). Unregistered types throw
  `InvalidOperationException("Cannot find page type for segment type …")` at navigation time.
- Segment names come from the PAGE type: `[NavigationSegment("name")]` if present, otherwise the class
  name (generic pages: `Name-Arg1-Arg2`). Two page classes with the same simple name collide.
- `Root<T>("custom-route")` matches a root registered under a custom route; not used by the Scaffold
  (roots are keyed by page type) — ignore in Scaffold apps.
- Obsolete `ShellContent<T>()` = `Root<T>()`; do not use.

Absolute semantics in the Scaffold (engine terms in parentheses):

- `ScaffoldArea`/`ScaffoldTabBar` = *item*; `ScaffoldRoot` = *section* with exactly one *content*.
- Target root == current root → the absolute navigation is converted to a relative one against the
  current stack (pops down to the root, then pushes the `Add<>` segments). Guards of popped pages run.
- Target root in the same area (section change) → default keeps the outgoing stack (tab-switch feel);
  `PopAllPagesOnSectionChange` destroys it (pages leave + dispose).
- Target root in another area (item change) → default `PopAllPagesOnItemChange` destroys every stack
  of the area you leave; `NavigationBehavior.None` (or any value without that flag) preserves them.
- Modal-presented pages on the current stack are popped first (guards apply) before switching root.
- Absolute default when `Behavior` is null: `PopAllPagesOnItemChange`. Relative default: `None`.

`NavigationBehavior` flags: `None`, `PopAllPagesOnSectionChange`, `PopAllPagesOnItemChange`,
`IgnoreGuards`, `Immediate` (skip the 60 ms delay that lets touch feedback render — use for
programmatic/startup navigations); combos `DefaultIgnoreGuards`, `DefaultImmediate`,
`DefaultImmediateIgnoreGuards` (each includes `PopAllPagesOnItemChange`).

`GoToAsync` result/exception matrix:

| Situation | Result |
|-----------|--------|
| Completed | `true` |
| A guard returned `false` | `false`, no lifecycle events fired, stack untouched |
| Queued behind another navigation and the stack moved meanwhile | `false` (`NavigationIgnored` event) |
| Called inside a lifecycle callback / guard (same async flow, incl. `Task.Run` started there) | throws `InvalidNavigationException` |
| Unregistered type / unreachable path | throws `InvalidOperationException` |
| Called off the UI thread | undefined (crash on iOS/Android) |

## 2. `AddPages()` generator

Emitted as `internal static class NaluNavigationRegistrations` (global namespace) with
`AddPages(this NavigationConfigurator)` and `AddIntents(this NavigationRestoreOptions)` (restore →
skill `nalu-navigation-lifecycle`). Discovery: every non-abstract, non-generic class deriving from
`Microsoft.Maui.Controls.ContentPage` in the compiling assembly, found through C# syntax AND through
`.xaml` files (so a XAML page whose code-behind omits the base type is still found); a `Scaffold`
subclass is never a page. Pages in other assemblies: call their own `AddPages()` if that assembly
references Nalu, or register manually.

Model inference per page:

1. Any ctor body assignment `BindingContext = param` / `this.BindingContext = param` where `param` is
   a ctor parameter → that parameter's type. Interface type → the single non-abstract INPC class in the
   assembly implementing it (`AddPage<IModel, Impl, Page>()`).
2. Else exactly one distinct INPC-typed parameter across public ctors → that type.
3. Else class named `<PageName>Model` (exactly one) → registered as `AddPage<Model, Page>()`, or as
   `AddPage<I<PageName>Model, Model, Page>()` when such an interface exists and the class implements it.
4. Else `AddPage<Page>()` (view-only).

Opt-out: `[AutoNavigationPage(Enabled = false)]` on the page class (skipped entirely: not registered,
not added to DI). Registration is idempotent per model type: a manual `AddPage<Model, Page>()` next to
`AddPages()` is harmless (first wins).

| Id | Severity | Meaning | Fix |
|----|----------|---------|-----|
| NALU0001 | Info | Page registered view-only (no model found) | Intended for view-only pages; else fix naming / ctor |
| NALU0002 | Warning | `BindingContext` assigned from more than one ctor parameter — page SKIPPED | Assign one; register manually otherwise |
| NALU0003 | Warning | Interface model has 0 or >1 INPC implementations — page SKIPPED | Use `AddPage<IModel, Impl, Page>()` |
| NALU0004 | Warning | Type assigned to `BindingContext` is not INPC — page SKIPPED | Derive the model from `ObservableObject` |
| NALU0005 | Error | Two restorable intents share a restore type id | `[AutoNavigationIntent("unique-id")]` |
| NALU0006 | Warning | Several classes named `<PageName>Model` — registered view-only | Register manually to pick one |

"Skipped" pages are neither navigable nor in DI: `Nav.Push<SkippedPageModel>()` throws at runtime.

## 3. Registration API (`NavigationConfigurator`)

| Member | Effect |
|--------|--------|
| `AddPage<TPage>()` | `services.AddScoped<TPage>()`; page is the lifecycle target |
| `AddPage<TPageModel, TPage>()` | mapping model→page; both scoped |
| `AddPage<TPageModel, TPageModelImpl, TPage>()` | mapping interface→page; `AddScoped<TPageModel, TPageModelImpl>()` + page |
| `AddPage(Type model, Type page)` / `AddPage(Type model, Type impl, Type page)` | non-generic forms (not trim-safe for reflection-free intent dispatch unless annotated) |
| `WithLeakDetectorState(state)` | default `EnabledWithDebugger` |
| `WithNavigationIntentBehavior(b)` | default `Fallthrough` |
| `WithBackImage(ImageSource)` / `WithMenuImage(ImageSource)` | Shell-host icons only; no effect on the Scaffold nav bar |
| `Mapping` (`IReadOnlyDictionary<Type,Type>`) | model→page map (read via `INavigationConfiguration`) |

`UseNaluNavigation<App>(configure)` also registers: `INavigationService` (singleton),
`INavigationServiceProvider` (scoped, per navigated page) and the restore services.

## 4. Intents in depth

- Handler lookup: a `ValueTask OnEnteringAsync(X)` / `OnAppearingAsync(X)` method on the lifecycle
  target whose single parameter type is assignable from the intent's runtime type. Explicit interface
  implementations count. First match wins; declare handlers for a base intent type to fan-in.
- Both events check the intent independently: `Push<T>(intent)` delivers it to `IEnteringAware<T>` AND
  `IAppearingAware<T>` of the pushed page (each once, if implemented). Pop results only hit
  `IAppearingAware<T>` of the revealed page.
- `Fallthrough` (default): typed handler if any, otherwise the parameterless one. `Strict`: typed
  handler or nothing (the parameterless one is only called for intent-less navigations).
- `AwaitableIntent<T>`: `SetResult(T)`, `SetException(Exception)`, awaitable (`GetAwaiter`). It
  completes when the page that received it LEAVES the stack (pop, tab destroy, absolute navigation),
  with `default(T)` if `SetResult` was never called. `AwaitableIntent` (non-generic) completes with no
  value. `ResolveIntentAsync<TPage, TResult>(intent)` = `GoToAsync(Nav.Push<TPage>().WithIntent(intent))`
  then `await intent`; if the push returns `false` (guard) the await still waits — check guards first
  when that matters. Awaitable intents are never restorable.
- Intents flow to `NavigationLifecycleEventArgs.Data` of the shell-level events (`NaluShell` only).

## 5. Navigation-scoped services

`INavigationServiceProvider` is `IServiceProvider` + `IDisposable`, one per navigated page:

- `AddNavigationScoped<T>(instance)` registers by exact `T` (adding the same `T` twice throws).
- `GetService(Type)` looks in the page's own registrations, then the page BELOW it in the stack, and so
  on to the root; it does NOT fall back to the app container (`GetRequiredService<T>` throws if absent).
- `ContextPage` = the page owning this provider.
- On dispose (page leaves) every registered `IDisposable` instance is disposed.
- Roots have no parent: state added on a root page is visible to every page pushed on that tab; a
  different tab cannot see it. Prefer intents for one-shot parameters, nav-scoped services for mutable
  context shared by a flow (wizard, order, edit session).

## 6. View-only pages

- Registered by `AddPage<TPage>()`; the generator emits it for pages without a model. Ctor injection
  works normally (`public AboutPage(IAppInfo info)`).
- Lifecycle target = the page (unless `BindingContext` is explicitly set to a non-null object other
  than the page itself — an inherited context does not count; `BindingContext = this` behaves as unset).
- Same stack can mix view-only and model pages; a model page can be pushed by its page type.
- Instances are single-use: never cache or re-push a page instance.

## 7. XAML navigation

Namespace `xmlns:nav="https://nalu-development.github.com/nalu/navigation"`.

```xml
<Button Text="Settings" Command="{nav:NavigateCommand}">
    <Button.CommandParameter>
        <nav:AbsoluteNavigation>
            <nav:NavigationSegment Type="pages:SettingsPage" />
        </nav:AbsoluteNavigation>
    </Button.CommandParameter>
</Button>
<Button Text="Back" Command="{nav:NavigateCommand}">
    <Button.CommandParameter>
        <nav:RelativeNavigation Intent="{Binding SaveResult}"><nav:NavigationPop /></nav:RelativeNavigation>
    </Button.CommandParameter>
</Button>
```

- `NavigateCommand` resolves `INavigationService` from the element's handler (`MauiContext`); the
  element must be in the visual tree. It disables itself while a navigation runs (double-tap safe).
- `RelativeNavigation` / `AbsoluteNavigation` are `Navigation` subclasses: children are
  `NavigationSegment` (`Type` = page or model type; or `SegmentName` string) and `NavigationPop`.
  `Intent` is a bindable property on the navigation object. `Behavior` is ctor-only (not settable in
  XAML) — use C# for custom behaviors.

## 8. Rarer scenarios

- **Replace current page**: `Nav.Pop().Push<NewPageModel>()` — one transition, one guard pass.
- **Pop to root of the current tab**: absolute to the same root, `Nav.Root<HomePageModel>()`; or chain
  `.Pop()` N times.
- **Deep link at startup / from a notification**: `Nav.Absolute(NavigationBehavior.Immediate)
  .Root<HomePageModel>().Add<DetailPageModel>().WithIntent(new DetailIntent(id))` dispatched on the UI
  thread; if the app may still be navigating, honor the `bool`.
- **Forced logout / reset**: `Nav.Absolute(NavigationBehavior.DefaultImmediateIgnoreGuards).Root<LoginPageModel>()`
  — destroys the other area's stacks only if login is in a different `ScaffoldArea`; roots in the same
  tab bar keep their stacks unless you add `PopAllPagesOnSectionChange`.
- **Redirect after a guard-less confirmation inside a lifecycle event**: dispatch
  (`IDispatcher.DispatchAsync(() => _nav.GoToAsync(...))`) from `OnAppearingAsync`, never inline.
- **Custom tab bar / menu item selecting a root**: bind to `ScaffoldRoot.SelectCommand` (engine-routed,
  guards run) → skill `nalu-scaffold-structure`. Programmatic equivalent: `Nav.Root<TRootPage>()`.
- **Monitoring every navigation event** (analytics/logging): subscribe to `Scaffold.NavigationEvent`
  (`EventHandler<NavigationLifecycleEventArgs>`, e.g. in `AppScaffold`'s ctor). `e.EventType`
  (`NavigationLifecycleEventType`: `NavigationRequested`, `NavigationCompleted`, `NavigationCanceled`,
  `NavigationFailed`, `NavigationIgnored`, `Entering`, `Appearing`, `Disappearing`, `Leaving`,
  `LeavingGuard`), `e.Target` (page model/page, or `NavigationLifecycleInfo` for the request events:
  `RequestedNavigation`, `CurrentState`, `TargetState`), `e.Data` (intent).
- **Testing** (`Matches`, mocking `INavigationService`) → skill `nalu-navigation-lifecycle`.
