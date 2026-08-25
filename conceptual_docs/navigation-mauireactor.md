# MauiReactor Component Pages (MVU)

Nalu navigation can drive **[MauiReactor](https://adospace.gitbook.io/mauireactor/) stateful
components** as first-class pages: no `Page` subclass, no page model, no `BindingContext` —
the component **is** the navigation destination *and* the lifecycle target. Typed navigations,
guards, lifecycle events, intents, tab-stack preservation and (under the
[Scaffold](scaffold.md)) gesture-driven back all work identically to the MVVM and
[view-only](navigation-view-only.md) modes.

Nalu deliberately ships **no MauiReactor package**: the bridge is a small class you paste into
your app, built on MauiReactor's public `TemplateHost` integration primitive. The seam it
plugs into (`IComponentPageFactory`) is framework-agnostic — the same two-member contract
bridges other component frameworks too.

## Quick Start

### 1. Paste the bridge and enable it

With `Reactor.Maui` installed, add this class to your app (this exact code is exercised by
Nalu's test suite):

```csharp
using MauiReactor;
using MauiPage = Microsoft.Maui.Controls.Page;

internal sealed class MauiReactorComponentPageFactory : IComponentPageFactory
{
    public IComponentPageHandle CreatePage(object component)
    {
        if (component is not VisualNode visualNode)
        {
            throw new InvalidOperationException($"{component.GetType().FullName} must derive from MauiReactor.Component to be used as a component-based page.");
        }

        var host = new TemplateHost(visualNode);

        if (host.NativeElement is not MauiPage page)
        {
            host.Stop();

            throw new InvalidOperationException($"{component.GetType().FullName} must render a Page-derived root (e.g. ContentPage) to be used as a navigation page.");
        }

        return new Handle(host, page, component);
    }

    private sealed class Handle(TemplateHost host, MauiPage page, object component) : IComponentPageHandle
    {
        public MauiPage Page => page;
        public object LifecycleTarget => component;
        public void Dispose() => host.Stop();
    }
}
```

Then register it — one line on each builder:

```csharp
builder
    .UseMauiApp<App>()
    .UseNaluNavigation<App>(nav => nav
        .AddPages() // source-generated, includes opted-in components (below)
        .UseComponentPageFactory<MauiReactorComponentPageFactory>())
    .UseMauiReactor() // MauiReactor's own init (Component.Services etc.)
    .UseNaluScaffold();
```

### 2. Write components, mark the page-rendering ones

A navigable component is a regular MauiReactor component whose `Render()` returns a
**Page-rooted** tree (e.g. `ContentPage(...)`). Decorate it with `[AutoNavigationPage]` and
the source-generated `AddPages()` registers it:

```csharp
class CounterState
{
    public int Count { get; set; }
}

[AutoNavigationPage]
partial class CounterPage(INavigationService navigation) : Component<CounterState>
{
    public override VisualNode Render()
        => ContentPage(
            VStack(
                Label($"Count: {State.Count}"),
                Button("Increment").OnClicked(() => SetState(s => s.Count++)),
                Button("Details").OnClicked(() => navigation.GoToAsync(Nav.Push<DetailPage>(State.Count)))
            ).Center()
        ).Title("Counter");
}
```

On non-`Page` classes the attribute is an **opt-in** (nothing else about a component reveals
that it renders a page — most components are view fragments, and those must never be
registered), while on `ContentPage`s it keeps its usual opt-out role
(`Enabled = false`). Components you prefer to register by hand use the same model-less
overload as view-only pages:

```csharp
nav.AddPage<CounterPage>()
   .AddPage<DetailPage>();
```

### 3. Navigate by component type

The standard API accepts component types wherever it accepts page or page-model types
(`Nav` is the usual `global using Nav = Nalu.Navigation;` alias):

```csharp
await _navigationService.GoToAsync(Nav.Push<DetailPage>());
await _navigationService.GoToAsync(Nav.Pop());
await _navigationService.GoToAsync(Nav.Root<CounterPage>());
```

And a component is a valid Scaffold root:

```csharp
new ScaffoldRoot { Title = "Counter", PageType = typeof(CounterPage) }
```

Inside a component, reach the engine through `INavigationService` — constructor injection
works because Nalu creates the component **inside the page's own DI scope** (scoped services
like `INavigationServiceProvider` included). Prefer it over MauiReactor's `Navigation`
property: with Nalu hosting the app, raw `INavigation` pushes are rejected by design (pops
are fine), because untyped page instances bypass the engine.

### 4. Lifecycle, guards and intents live on the component

Implement the same interfaces a page model would — directly on the component. Typed intents
included:

```csharp
class DetailState
{
    public int ItemId { get; set; }
}

[AutoNavigationPage]
partial class DetailPage : Component<DetailState>, IEnteringAware<int>, ILeavingGuard
{
    public ValueTask OnEnteringAsync(int itemId)
    {
        SetState(s => s.ItemId = itemId);  // re-renders into the SAME native page

        return default;
    }

    public async ValueTask<bool> CanLeaveAsync()
        => !_hasUnsavedChanges
           || await ConfirmDiscardAsync();

    public override VisualNode Render() => ContentPage( /* ... */ );
}
```

Everything in [Lifecycle Events](navigation-lifecycle.md) and
[Intents](navigation-intents.md) applies unchanged, with "page model" read as "component".
`ILeavingGuard` is honored on every leave path (programmatic pops, back button, Android
system/predictive back, iOS edge swipe), and intents implemented by **registered** components
feed the generated `AddIntents()`, so [State Restoration](navigation-restore.md) replays them
after an app restart.

## Who receives lifecycle events?

For component pages the rule is simpler than the
[view-only precedence](navigation-view-only.md#who-receives-lifecycle-events): **the
component is the lifecycle target, unconditionally.** Nalu never assigns the native page's
`BindingContext` (propagating a context through a component-rendered tree would be pure
overhead for an MVU framework that doesn't use bindings), and even an explicitly assigned one
would not steal the lifecycle.

One consequence worth knowing: the Scaffold nav bar's `PageBindingContext` stays `null` for
component pages. That's coherent with MVU — drive the nav bar by setting the Scaffold
attached properties with concrete values from `Render()`; re-renders update them.

## How it works

- **Creation**: on navigation, Nalu resolves the component from the page's fresh DI scope and
  hands it to your registered factory, which mounts it through MauiReactor's `TemplateHost`. The `Page`
  the component renders becomes the navigation page — pushed, transitioned and tracked like
  any other.
- **Re-renders**: `SetState` updates flow into that **same page instance**; the navigation
  stack never sees a page swap.
- **Teardown**: when the page leaves the stack, Nalu unmounts the component tree, then
  disposes the scope and disconnects handlers — the exact lifecycle pages and page models
  get. The leak detector watches component pages too.

## Notes

- **The root must be a Page.** A component rendering a plain view is rejected at navigation
  time with a descriptive error — wrap the content in `ContentPage(...)`.
- **MauiReactor hot reload** (the `dotnet-maui-reactor` console) does not currently refresh
  Nalu-hosted component pages: MauiReactor's assembly-swap pipeline is internal to its own
  hosts. State-driven re-renders and regular .NET hot reload of method bodies are unaffected.
- **Mixing modes** works per destination type: MVVM pages, view-only pages and component
  pages coexist on the same stack.
- **Other component frameworks** (Comet, BlazorBindings.Maui, …): implement
  `IComponentPageFactory` the same way — turn a component instance into an
  `IComponentPageHandle` exposing the rendered `Page` and the lifecycle target — and register
  it via `nav.UseComponentPageFactory<TFactory>()`; the engine takes care of everything else.
  Note the contract is synchronous (the page is pushed immediately): an async-rendering
  framework should materialize into a shell page it fills.
