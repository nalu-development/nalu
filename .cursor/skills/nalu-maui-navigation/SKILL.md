---
name: nalu-maui-navigation
description: Nalu.Maui.Navigation: type-safe MVVM navigation with Shell, lifecycle events, intents, and guards. Use when setting up navigation, passing parameters, preventing navigation (unsaved changes), custom tab bar, or testing navigation in MAUI.
---

# Nalu.Maui.Navigation

Type-safe navigation on top of Shell: fluent API, scoped disposal, async lifecycle, intents, and guards.

## When to use

- Replace Shell string routes with type-based navigation and proper disposal.
- Pass typed data (intents) and get results (awaitable intents).
- Guard navigation (e.g. unsaved changes) and customize tab bar.

## Setup

```csharp
builder.UseNaluNavigation<App>(nav => nav
    .AddPage<MainPageModel, MainPage>()  // AOT: use AddPage for each page
    .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
);
```

**AOT:** Use `.AddPage<TPageModel, TPage>()` (or with interface) per page; `.AddPages()` is not AOT/trim-compatible.

## Page and ViewModel

- **Page**: Constructor must take the ViewModel (and optionally other deps); set `BindingContext = viewModel`.
- **ViewModel**: Implement `INotifyPropertyChanged` (e.g. `ObservableObject`). Register as Scoped; disposed when removed from stack.

## Shell

- Use **NaluShell**; constructor: `base(navigationService, typeof(DefaultPage))`.
- In XAML: `ShellContent` with `nalu:Navigation.PageType="pages:MainPage"`.
- In `App`: `MainPage = new AppShell(navigationService)`. On Android, cache `Window` in `CreateWindow` to avoid duplicate Shell.

## Navigation API

- **Relative**: `Navigation.Relative().Push<DetailPageModel>()`, `.Pop()`, `.Pop().Push<NewPageModel>()`.
- **Absolute**: `Navigation.Absolute().Root<SettingsPageModel>()`, `.Root<X>().Add<Y>()`.
- **Execute**: `await _navigationService.GoToAsync(navigation)`.

XAML: `NavigateCommand` with `RelativeNavigation` and `NavigationPop` or `NavigationSegment Type="pages:DetailPage"`.

## Lifecycle

Implement only what you need. Order: **Entering** → animation → **Appearing** → ... → **Disappearing** → **Leaving** → animation → **Dispose**.

| Interface | When | Fires multiple times? |
|-----------|------|------------------------|
| IEnteringAware | Before animation | No |
| IAppearingAware | After animation | Yes (returning from child) |
| IDisappearingAware | Before leaving | Yes (pushing a child) |
| ILeavingAware | Before removal | No |
| IDisposable | After removal | No |

Keep `OnEnteringAsync` fast (<30ms); use `IAppearingAware` for slow work or the [Background Loading Pattern](https://nalu-development.github.io/nalu/navigation-lifecycle.html#background-loading-pattern).

## Intents

- **Pass data**: `Navigation.Relative().Push<DetailPageModel>().WithIntent(new ContactIntent(42))`. Receive in `IEnteringAware<ContactIntent>` or `IAppearingAware<ContactIntent>`.
- **Get result**: Define `class MyIntent : AwaitableIntent<TResult>`. Navigate with `ResolveIntentAsync<PageModel, TResult>(intent)`. On pushed page: `intent.SetResult(value)` then `GoToAsync(Navigation.Relative().Pop())`.

Use `record` for intents when possible (value equality for tests).

## Guards

Implement **ILeavingGuard** and **CanLeaveAsync()**; return `false` to cancel navigation (e.g. after user confirms "Leave without saving?"). Bypass: `Navigation.Relative(NavigationBehavior.IgnoreGuards).Pop()`.

## Custom tab bar

Optional: `builder.UseNaluTabBar()`. Works with standard Shell and NaluShell.

- **NaluTabBar**: Use the built-in control and style it (bar/tab colors, shapes, blur). Inherit in XAML and set `NaluTabBar.UseBlurEffect` in static constructor if needed.
- **Completely custom bar**: Use any view as tab bar; on tab press call `NaluTabBar.GoTo(shellSection)` (each tab’s `BindingContext` = corresponding `ShellSection`). Attach via `nalu:NaluShell.TabBarView="{YourCustomTabBar}"`.

For edge-to-edge and safe area (e.g. content not extending into bottom inset), see [Custom TabBarView: edge-to-edge / safe area](https://github.com/nalu-development/nalu/discussions/124) (root `Grid` with `SafeAreaEdges="None"`, inner content with `SafeAreaEdges="Container"`).

Full reference: [Custom Tab Bar](https://nalu-development.github.io/nalu/navigation-tabbar.html).

## Testing

Mock `INavigationService` (e.g. NSubstitute). Assert with `nav.Matches(expectedNav)` on the argument passed to `GoToAsync`. Use record intents for easy equality.

## Caveats

- Dispatch navigation from lifecycle events: use `IDispatcher.DispatchAsync(() => _navigationService.GoToAsync(...))` to avoid blocking.
- Match cleanup to creation: Constructor ↔ Dispose, Entering ↔ Leaving, Appearing ↔ Disappearing.

## Additional context

- [Navigation](https://nalu-development.github.io/nalu/navigation.html) · [Lifecycle](https://nalu-development.github.io/nalu/navigation-lifecycle.html) · [Intents](https://nalu-development.github.io/nalu/navigation-intents.html) · [Advanced](https://nalu-development.github.io/nalu/navigation-advanced.html) · [Tab Bar](https://nalu-development.github.io/nalu/navigation-tabbar.html) · [Testing](https://nalu-development.github.io/nalu/navigation-testing.html)
- Code: [Source/Nalu.Maui.Navigation/](Source/Nalu.Maui.Navigation/)
