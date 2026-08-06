# View-Only Navigation (No Page Models)

Nalu navigation is MVVM-first, but the MVVM layer is **optional**. You can register plain
pages — no page models, no `BindingContext` — and navigate between them with the exact same
engine: typed navigations, guards, lifecycle events, intents, tab-stack preservation and
(under the [Scaffold](scaffold.md)) gesture-driven back all work identically.

This is the mode to reach for when:

- you're building a small app where page models would be ceremony;
- you're migrating an existing code-behind app and want Scaffold chrome *now*, MVVM later;
- you have (or want to build) **your own MVVM abstraction** and only need Nalu as the
  navigation/host engine underneath.

## Quick Start

### 1. Register pages instead of page/model pairs

```csharp
builder
    .UseMauiApp<App>()
    .UseNaluNavigation<App>(nav => nav
        .AddPage<TodayPage>()      // view-only: no page model
        .AddPage<SettingsPage>()
        .AddPage<DetailPage>())
    .UseNaluScaffold();
```

`AddPage<TPage>()` registers the page as a scoped service: every navigation creates a **fresh
instance** in its own service scope, so constructor injection works exactly as it does for
page models:

```csharp
public partial class DetailPage : ContentPage
{
    public DetailPage(IWeatherService weather)
    {
        InitializeComponent();
        _weather = weather;
    }
}
```

### 2. Navigate by page type

One line of setup first: inside a `Page` subclass the inherited `Page.Navigation` property
hides Nalu's `Navigation` class, so alias it once for the whole app (e.g. in `GlobalUsings.cs`):

```csharp
global using Nav = Nalu.Navigation;
```

The standard navigation API accepts page types directly — using the shorthand entry points
(`Nav.Push<T>()` ≡ `Nav.Relative().Push<T>()`, still chainable):

```csharp
// Push / pop
await _navigationService.GoToAsync(Nav.Push<DetailPage>());
await _navigationService.GoToAsync(Nav.Pop());

// Multi-segment navigations chain as usual (one transition)
await _navigationService.GoToAsync(Nav.Pop().Pop());

// Absolute navigation to a root
await _navigationService.GoToAsync(Nav.Root<SettingsPage>());
```

And the Scaffold's roots are already page types — nothing changes:

```xml
<nalu:ScaffoldTabBar>
    <nalu:ScaffoldRoot Title="Today" PageType="{x:Type pages:TodayPage}" />
    <nalu:ScaffoldRoot Title="Settings" PageType="{x:Type pages:SettingsPage}" />
</nalu:ScaffoldTabBar>
```

### 3. Lifecycle, guards and intents live on the page

Without a page model, **the page itself is the lifecycle target**: implement the same
interfaces you would implement on a page model — directly on the page.

```csharp
public partial class DetailPage : ContentPage, IEnteringAware, ILeavingGuard
{
    public ValueTask OnEnteringAsync()
    {
        // Load data before the push animation starts.
        return LoadAsync();
    }

    public async ValueTask<bool> CanLeaveAsync()
        => !_hasUnsavedChanges
           || await DisplayAlert("Discard changes?", "You have unsaved edits.", "Discard", "Stay");
}
```

`ILeavingGuard` is honored on **every** leave path — programmatic pops, the back button,
Android system/predictive back and the iOS edge swipe (guarded pages get no interactive
preview; the committed back routes through the engine, which runs the guard).

Intents work the same way — implement the typed interface on the page:

```csharp
public partial class DetailPage : ContentPage, IEnteringAware<int>
{
    public ValueTask OnEnteringAsync(int itemId) => LoadAsync(itemId);
}

await _navigationService.GoToAsync(Nav.Push<DetailPage>(42)); // ≡ ...Push<DetailPage>().WithIntent(42)
```

See [Lifecycle Events](navigation-lifecycle.md) and [Intents](navigation-intents.md) for the
full contracts — everything there applies unchanged, with "page model" read as "lifecycle
target".

## Who receives lifecycle events?

There is exactly **one lifecycle target per page** — events are never dispatched to two
objects. The rule:

1. An **explicitly assigned `BindingContext`** (the MVVM page model) is the target, and wins
   *entirely*: if both the page and its binding context implement lifecycle interfaces, only
   the binding context is called.
2. Otherwise, **the page itself** is the target.

Two details worth knowing:

- An **inherited** binding context does not count. Pages hosted in a Scaffold (or Shell)
  inherit the host's `BindingContext` through MAUI's normal propagation — that's application
  state, not the page's model, and it never becomes a lifecycle target. Only an explicit
  assignment (`BindingContext = ...`, in code or XAML) switches the target.
- `BindingContext = this` is equivalent to not setting it: the page is the target either way.

## Mixing modes

Modes are **per page type** and mix freely in one app: register some pages with models
(`AddPage<TPageModel, TPage>()`) and others without (`AddPage<TPage>()`); push either kind
onto the same stack. A page type registered with a model can also be pushed by its page type
(`Push<DetailPage>()` and `Push<DetailPageModel>()` resolve to the same destination).

## Building your own MVVM abstraction on top

If you want your own navigation abstraction (your own conventions for view models, parameters
and routing), you don't need to replace anything: write a thin facade over
`INavigationService` and keep the engine — and all the Scaffold chrome it drives — underneath.

```csharp
public interface IAppNavigator
{
    Task ShowDetailAsync(int itemId);
    Task BackAsync();
}

internal sealed class AppNavigator(INavigationService navigation) : IAppNavigator
{
    public Task ShowDetailAsync(int itemId)
        => navigation.GoToAsync(Nav.Push<DetailPage>(itemId));

    public Task BackAsync()
        => navigation.GoToAsync(Nav.Pop());
}
```

Your abstraction decides how pages get their state (intents, DI, your own view-model wiring);
Nalu provides typed stacks, guards, lifecycle, transitions and chrome.

## Notes

- **Instances are single-use.** The engine creates a page per navigation and disposes its
  scope (and disconnects handlers) when it leaves the stack — the same lifecycle page models
  get. Don't cache and re-push page instances.
- **AOT/trimming**: `AddPage<TPage>()` is fully AOT/trim-compatible and also preserves the
  page's methods for intent dispatch — register view-only pages through it rather than with a
  raw `services.AddScoped<TPage>()`.
- Works with both hosts: the [Scaffold](scaffold.md) and the classic `NaluShell`.
