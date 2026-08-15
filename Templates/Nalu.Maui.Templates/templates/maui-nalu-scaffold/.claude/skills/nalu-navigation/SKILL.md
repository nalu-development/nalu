---
name: nalu-navigation
description: Nalu.Maui.Navigation in a Scaffold app — AddPages() generator rules, INavigationService/GoToAsync, the Nav builder (Push/Pop/Root/Add, intents, behaviors), guards, view-only pages, tab roots and DI scopes; load when adding pages or navigating.
---
# Nalu navigation (model-first, Scaffold-hosted)

Package `Nalu.Maui.Navigation`, namespace `Nalu` (`GlobalUsings.cs` has `global using Nalu;` and
`global using Nav = Nalu.Navigation;`). XAML: `xmlns:nav="https://nalu-development.github.com/nalu/navigation"`
(the template's `nalu:` prefix is the Scaffold namespace). Mental model: every `ScaffoldRoot` in
`AppScaffold.xaml` owns an independent stack; you navigate with typed requests built by `Nav`
and executed by `INavigationService.GoToAsync`. Each navigated page lives in its own DI scope,
gets its page model as `BindingContext`, receives lifecycle calls, and is disposed when popped.

## Quick reference

| API | Purpose | Notes |
|-----|---------|-------|
| `.UseNaluNavigation<App>(nav => nav.AddPages())` | Registers navigation + all pages | `AddPages()` is source-generated for THIS assembly (trim/AOT-safe). |
| `nav.AddPage<TPageModel, TPage>()` | Manual page/model pair | `TPageModel : INotifyPropertyChanged`, `TPage : ContentPage`. Use for other assemblies / generator opt-outs. |
| `nav.AddPage<IModel, ModelImpl, TPage>()` | Pair via interface | Navigate with `Push<IModel>()`. |
| `nav.AddPage<TPage>()` | View-only page (no model) | Lifecycle/guards/intents implemented on the page itself. |
| `nav.WithLeakDetectorState(NavigationLeakDetectorState)` | `Disabled` / `EnabledWithDebugger` (default) / `Enabled` | Alerts when a popped page is not GC'd. |
| `nav.WithNavigationIntentBehavior(NavigationIntentBehavior)` | `Fallthrough` (default) / `Strict` | See intents rule below. |
| `[AutoNavigationPage(Enabled = false)]` on a page | Exclude from `AddPages()` | Register manually if still needed. |
| `[NavigationSegment("name")]` on a page | Override route segment (default: page class name) | Only needed for duplicate class names / restore ids. |
| `INavigationService.GoToAsync(INavigationInfo)` → `Task<bool>` | Execute a navigation | UI thread only. `false` = blocked by guard or superseded. Singleton, inject anywhere. |
| `Nav.Push<T>()` / `Nav.Push<T>(intent)` | Relative push | `T` = page model OR page type. Chainable `.Push<T2>()`; `.WithIntent(x)` ends the chain. |
| `Nav.Pop()` / `Nav.Pop(resultIntent)` | Relative pop | Chain `.Pop().Pop()`, `.Pop().Push<T>()` (replace). Result intent goes to the REVEALED page. |
| `Nav.Root<T>()` / `Nav.Root<T>(intent)` | Absolute: select the root (tab) whose page is `T` | `.Add<T2>()` pushes on top of it. |
| `Nav.Relative(behavior)` / `Nav.Absolute(behavior)` | Long form with `NavigationBehavior` flags | Then `.Push/.Pop` or `.Root/.Add`. |
| `NavigationBehavior` | `IgnoreGuards`, `Immediate` (skip 60 ms touch delay), `PopAllPagesOnSectionChange`, `PopAllPagesOnItemChange` (default for absolute) | Combine with `\|` or `DefaultIgnoreGuards` / `DefaultImmediate` / `DefaultImmediateIgnoreGuards`. |
| `_nav.ResolveIntentAsync<TPage, TResult>(AwaitableIntent<TResult>)` | Push and await a result | Also `ResolveIntentAsync<TPage>(AwaitableIntent)`. |
| `AwaitableIntent<T>` / `AwaitableIntent` | Base for result-carrying intents | Target calls `SetResult(v)` / `SetException(ex)` then pops. |
| `INavigationServiceProvider` (scoped) | `AddNavigationScoped<T>(instance)`, `GetRequiredService<T>()`, `ContextPage` | Shares state page → all pages pushed above it. |
| `{nav:NavigateCommand}` + `nav:RelativeNavigation` / `nav:AbsoluteNavigation` | Navigate from XAML | Children `nav:NavigationSegment Type="pages:X"` / `nav:NavigationPop`. |
| `INavigationInfo.Matches(other)` | Compare navigations (unit tests) | Path + intent equality. |

Lifecycle interfaces (implement on the page model, or on the page when view-only) — ordering, rules
and restoration → skill `nalu-navigation-lifecycle`:

| Interface | Method | When |
|-----------|--------|------|
| `IEnteringAware` / `IEnteringAware<TIntent>` | `OnEnteringAsync([intent])` | Once, before the push animation (keep fast). |
| `IAppearingAware` / `IAppearingAware<TIntent>` | `OnAppearingAsync([intent])` | After the animation; again each time revealed (pop result intents arrive here). |
| `IDisappearingAware` | `OnDisappearingAsync()` | Page covered or leaving. |
| `ILeavingAware` | `OnLeavingAsync()` | Once, removed from the stack. |
| `ILeavingGuard` | `CanLeaveAsync()` → `bool` | Before anything else; `false` cancels the whole navigation. |
| `IDisposable` | `Dispose()` | After leaving, when the scope is disposed. |

## Patterns

Page ↔ model pair picked up by `AddPages()` (template shape — no registration code needed):

```csharp
public partial class DetailPage : ContentPage
{
    public DetailPage(DetailPageModel model) { BindingContext = model; InitializeComponent(); }
}
public partial class DetailPageModel : ObservableObject, IEnteringAware<DetailIntent>
{
    private readonly INavigationService _navigation;
    public DetailPageModel(INavigationService navigation) => _navigation = navigation;
    public ValueTask OnEnteringAsync(DetailIntent intent) { Id = intent.Id; return ValueTask.CompletedTask; }
    [RelayCommand] private Task GoBackAsync() => _navigation.GoToAsync(Nav.Pop());
}
public record DetailIntent(int Id);
```

Push with intent, pop with result, absolute root switch:

```csharp
await _navigation.GoToAsync(Nav.Push<DetailPageModel>(new DetailIntent(42)));
await _navigation.GoToAsync(Nav.Pop(new DetailSaved(item)));            // caller: IAppearingAware<DetailSaved>
await _navigation.GoToAsync(Nav.Root<SettingsPageModel>());              // switch tab, keeps Home's stack
await _navigation.GoToAsync(Nav.Root<HomePageModel>().Add<DetailPageModel>().WithIntent(new DetailIntent(1)));
await _navigation.GoToAsync(Nav.Relative(NavigationBehavior.IgnoreGuards).Pop());
```

Awaitable intent (push a picker and get its result in one call):

```csharp
public class PickColorIntent : AwaitableIntent<Color?>;
var color = await _navigation.ResolveIntentAsync<ColorPickerPageModel, Color?>(new PickColorIntent());
// in ColorPickerPageModel : IEnteringAware<PickColorIntent> — store the intent, then:
_intent.SetResult(picked); await _navigation.GoToAsync(Nav.Pop());
```

Guard + navigation-scoped context shared with child pages:

```csharp
public partial class EditPageModel(INavigationServiceProvider navProvider) : ObservableObject, ILeavingGuard, IEnteringAware<OrderIntent>
{
    public ValueTask OnEnteringAsync(OrderIntent i) { navProvider.AddNavigationScoped<IOrderContext>(new OrderContext(i.OrderId)); return ValueTask.CompletedTask; }
    public async ValueTask<bool> CanLeaveAsync()
        => !IsDirty || await navProvider.ContextPage.DisplayAlertAsync("Discard changes?", "", "Discard", "Stay");
}
// child pushed above: ctor(INavigationServiceProvider p) => _order = p.GetRequiredService<IOrderContext>();
```

View-only page (no model): `nav.AddPage<AboutPage>()` is emitted automatically when no model is
inferred; navigate with `Nav.Push<AboutPage>()`, implement `IEnteringAware<T>`/`ILeavingGuard` on the page.

XAML navigation:

```xml
<Button Text="Details" Command="{nav:NavigateCommand}" xmlns:nav="https://nalu-development.github.com/nalu/navigation">
    <Button.CommandParameter>
        <nav:RelativeNavigation><nav:NavigationSegment Type="pages:DetailPage" /></nav:RelativeNavigation>
    </Button.CommandParameter>
</Button>
```

## Rules & gotchas

- `AddPages()` scans only the compiling assembly for non-abstract, non-generic `ContentPage` subclasses (the
  Scaffold itself is skipped). Model inference order: (1) ctor parameter assigned to `BindingContext`
  (interface param → its single INPC implementation), (2) exactly one INPC-typed public ctor parameter,
  (3) naming `MyPage` → `MyPageModel` (via `IMyPageModel` if implemented), (4) otherwise view-only.
- The model MUST implement `INotifyPropertyChanged` (`ObservableObject`) even without properties, or the
  page is skipped/view-only (NALU0004). Assigning `BindingContext` from two ctor params skips the page (NALU0002).
- Diagnostics NALU0001–0006 are info/warnings (0005 error): read build output when a page "cannot be found".
- Segment name = page class name: two pages with the same class name in different namespaces collide —
  use `[NavigationSegment("...")]`. `Push<TModel>()` and `Push<TPage>()` resolve to the same page.
- `GoToAsync` on the UI thread only (hop with `MainThread.InvokeOnMainThreadAsync` after `ConfigureAwait(false)`
  or `Task.Run`). Calls are serialized; a queued call is dropped (`false`) if the stack moved. Navigating from
  inside a lifecycle callback/guard throws `InvalidNavigationException` — dispatch it (`IDispatcher.DispatchAsync`),
  preferably from `OnAppearingAsync`.
- Do NOT `new` pages, cache instances, use `Shell.Current.GoToAsync`, `Navigation.PushAsync` or
  `PushModalAsync`: one instance per navigation, created and disposed by the engine (modal presentation is a
  Scaffold page mode → skill `nalu-scaffold-transitions`).
- Intent dispatch: the typed handler is chosen by assignability of the intent's runtime type
  (`IEnteringAware<Base>` receives derived intents). Default `Fallthrough`: no matching typed handler → the
  untyped `OnEnteringAsync()`/`OnAppearingAsync()` runs; `Strict`: nothing runs. Only ONE handler runs per event.
- Only `IEnteringAware<T>`/`IAppearingAware<T>` receive intents; the pop result intent reaches the revealed
  page's `IAppearingAware<TResult>` (not `IEnteringAware<T>`, which already ran). Prefer immutable `record` intents.
- Guards run before any lifecycle event on every leave path (button, Android back, iOS edge swipe, tab tap,
  multi-pop). Any `false` cancels the whole navigation; bypass with `NavigationBehavior.IgnoreGuards`.
- Tabs = `ScaffoldRoot`s (`PageType` must be a registered PAGE type). Tapping another tab or `Nav.Root<T>()`
  keeps the outgoing stack; tapping the active tab pops to root; `PopAllPagesOnSectionChange` clears the
  outgoing stack. Roots in a DIFFERENT `ScaffoldArea` are an "item change": default absolute behavior
  (`PopAllPagesOnItemChange`) destroys all stacks of the area you leave.
- Absolute navigations cannot contain `Pop`; relative chains cannot put a `Push` before a `Pop`
  (`Pop().Push<T>()` is fine, `Push<T>().Pop()` throws).
- Lifecycle target is the explicitly assigned `BindingContext` (wins entirely) else the page; an inherited
  binding context never counts. Register pages only through `AddPages()`/`AddPage<...>()` (they keep the
  intent-dispatch members alive under trimming) — never a raw `services.AddScoped<MyPage>()`.
- `INavigationServiceProvider.ContextPage` (inject the provider into a page model) is that model's own page —
  handy for `DisplayAlertAsync` in guards without any Shell reference.
- DI: page + model are scoped; `AddScoped` services live per navigated page; the scope (and `IDisposable`
  models) is disposed after the pop animation. `INavigationServiceProvider.GetService` resolves only
  navigation-scoped instances (walking up to parent pages), not the container.
- Startup lands on `Scaffold.InitialRootPageType` (default: first root); redirect from `OnAppearingAsync`
  with `Nav.Absolute(NavigationBehavior.Immediate).Root<T>()` dispatched.

Full grammar, generator diagnostics table, behavior matrix, view-only details and rarer scenarios:
read `reference.md` when a case above is not covered.

## See also

- `nalu-navigation-lifecycle` — event ordering, navigation inside lifecycle events, restoration, testing.
- `nalu-scaffold-structure` — `ScaffoldRoot`/`ScaffoldTabBar`, `SelectCommand`, per-page tab bar visibility.
- `nalu-scaffold-transitions` — page transitions, modal `PageMode`, back gestures.
