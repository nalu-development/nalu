# Nalu.Maui.Navigation

[![Nalu.Maui.Navigation NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Navigation.svg)](https://www.nuget.org/packages/Nalu.Maui.Navigation/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Navigation)](https://www.nuget.org/packages/Nalu.Maui.Navigation/)

A powerful, type-safe navigation system for .NET MAUI that fixes Shell navigation's pain points while preserving its strengths.

## Why Nalu Navigation?

Standard MAUI Shell navigation has several critical issues:

- **Memory leaks**: Pages and ViewModels aren't properly disposed ([MAUI Issue #7354](https://github.com/dotnet/maui/issues/7354))
- **Confusing API**: Hard to understand the difference between `GoToAsync("Page")`, `GoToAsync("/Page")`, `GoToAsync("//Page")`, etc.
- **No scoped services**: Difficult to distinguish between `Transient` and `Scoped` service lifetimes
- **Async void lifecycle**: Page lifecycle events use `async void` methods instead of proper async patterns
- **No navigation context**: Can't share data between nested pages easily

Nalu Navigation **solves all these problems** while keeping Shell's best features: tab bars, flyout menus, and multiple navigation stacks.

## Quick Start

### 1. Installation

```bash
dotnet add package Nalu.Maui.Navigation
```

### 2. Setup in MauiProgram.cs

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseNaluNavigation<App>(nav => nav
                .AddPages() // source-generated: registers every page in this assembly, AOT/trim-safe
                .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
            );
        
        return builder.Build();
    }
}
```

**`AddPages()` is source-generated** (the generator ships inside the NuGet package): at build
time it discovers every non-abstract `ContentPage` in your assembly and emits plain
`AddPage<...>` calls — fully AOT/trim-compatible, no reflection. For each page the page model
is inferred, in order:

1. the constructor parameter assigned to `BindingContext` in the page constructor (an
   interface-typed parameter is registered together with its single implementation);
2. otherwise, a single `INotifyPropertyChanged` constructor parameter;
3. otherwise, the `MyPage` → `MyPageModel` naming convention (preferring `IMyPageModel` when
   the model implements it);
4. otherwise the page is registered **view-only** (see [View-Only Navigation](navigation-view-only.md)).

Exclude a page from the generated registration with `[AutoNavigationPage(Enabled = false)]`
(abstract pages are always skipped; a concrete base page other pages derive from is
registered like any other — harmless if never navigated to). The generator reports
diagnostics (`NALU0001`–`NALU0006`) for view-only fallbacks, ambiguous models and
intent id collisions.

**Manual configuration options** (usable alongside `AddPages()` — e.g. for pages living in
**other assemblies**, which the generator does not scan):
- `.AddPage<MainPageModel, MainPage>()` - Manual registration ✅ **AOT-compatible**
- `.AddPage<IMainPageModel, MainPageModel, MainPage>()` - With interface (better for testing) ✅ **AOT-compatible**
- `.AddPage<MainPage>()` - **View-only** registration: no page model, lifecycle interfaces go directly on the page ✅ **AOT-compatible** — see [View-Only Navigation](navigation-view-only.md)

> **Without MVVM?** You can use Nalu without ViewModels — register pages with `AddPage<TPage>()` and use page types in navigation; lifecycle interfaces, guards and intents go directly on the page. See [View-Only Navigation](navigation-view-only.md).

### 3. Create your Page and ViewModel

**Pages must** require the ViewModel as a constructor parameter:

```csharp
public partial class MainPage : ContentPage
{
    public MainPage(MainPageModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
```

**ViewModels must** implement `INotifyPropertyChanged`:

```csharp
public class MainPageModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    public MainPageModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }
}
```

### 4. Define your Shell

Create `AppShell.xaml` inheriting from `NaluShell`:

```xml
<nalu:NaluShell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                xmlns:nalu="https://nalu-development.github.com/nalu/navigation"
                xmlns:pages="clr-namespace:MyApp.Pages"
                x:Class="MyApp.AppShell">
    <ShellContent nalu:Navigation.PageType="pages:MainPage"
                  Title="Home"/>
    <ShellContent nalu:Navigation.PageType="pages:SettingsPage"
                  Title="Settings"/>
</nalu:NaluShell>
```

Code-behind:

```csharp
public partial class AppShell : NaluShell
{
    public AppShell(INavigationService navigationService) 
        : base(navigationService, typeof(MainPage))
    {
        InitializeComponent();
    }
}
```

### 5. Initialize Shell in App.cs

```csharp
public partial class App : Application
{
    public App(INavigationService navigationService)
    {
        InitializeComponent();
        MainPage = new AppShell(navigationService);
    }
}
```

**Android-specific**: Cache the Window instance:

```csharp
#if ANDROID
    private Window? _window;
#endif

    protected override Window CreateWindow(IActivationState? activationState)
    {
#if ANDROID
        return _window ??= new Window(new AppShell(_navigationService));
#else
        return new Window(new AppShell(_navigationService));
#endif
    }
```

## Core Concepts

### Automatic Disposal and Scoped Services

Nalu creates a **ServiceScope for each navigated page**. Pages and ViewModels are registered as `Scoped` and **automatically disposed** when removed from the navigation stack.

```csharp
// This service lives only while the page is in the navigation stack
builder.Services.AddScoped<IPageSpecificService, PageSpecificService>();
```

When you implement `IDisposable`, it's automatically called after the page is removed and the navigation animation completes.

### Type-Safe Navigation

Navigate using types, not strings:

```csharp
// Push onto current stack
await _navigationService.GoToAsync(
    Navigation.Relative().Push<ContactDetailPageModel>()
);

// Switch to a different shell content
await _navigationService.GoToAsync(
    Navigation.Absolute().Root<SettingsPageModel>()
);

// Pop current page
await _navigationService.GoToAsync(
    Navigation.Relative().Pop()
);
```

### Shell Structure and Navigation Behavior

Shell organizes as: `ShellItem` > `ShellSection` > `ShellContent` > NavigationStack

Navigation behavior varies by hierarchy:
- **Same ShellSection**: Navigation stack pops to target
- **Different ShellSection, same ShellItem**: Current stack is preserved
- **Different ShellItem**: All stacks cleared, pages disposed

## Basic Navigation

> 💡 **Shorthands**: `Navigation.Push<T>()`, `Navigation.Pop()` and `Navigation.Root<T>()` are
> shorthand for `Relative().Push<T>()`, `Relative().Pop()` and `Absolute().Root<T>()` — still
> chainable, plus intent overloads that end the chain: `Navigation.Push<T>(intent)`,
> `Navigation.Pop(resultIntent)`, `Navigation.Root<T>(intent)`.
> Use the long `Relative(behavior)` / `Absolute(behavior)` form when you need a custom
> [`NavigationBehavior`](#shell-structure-and-navigation-behavior) — the shorthands always use
> the default one.
>
> 💡 **Inside a `Page` subclass** the inherited `Page.Navigation` property hides the
> `Navigation` class — alias it once per app: `global using Nav = Nalu.Navigation;` and write
> `Nav.Push<T>()`.
>
> ⚠️ **`GoToAsync` must be called on the UI thread** — it drives the shell directly and does not
> marshal for you. Concurrent calls are serialized and the queued one is dropped (returns `false`)
> if the shell moved meanwhile; navigating from *within* a navigation throws. See
> [Threading and Concurrency](navigation-advanced.md#threading-and-concurrency).

### Relative Navigation

```csharp
// Push
Navigation.Push<DetailPageModel>()

// Push with intent (ends the chain, like WithIntent)
Navigation.Push<DetailPageModel>(new DetailIntent(42))

// Pop
Navigation.Pop()

// Pop delivering a result intent to the revealed page
Navigation.Pop(new EditResult(saved: true))

// Replace (pop and push)
Navigation.Pop().Push<NewPageModel>()

// Pop multiple
Navigation.Pop().Pop().Push<PageModel>()

// Custom behavior: long form
Navigation.Relative(NavigationBehavior.PopAllPagesOnItemChange).Push<DetailPageModel>()
```

### Absolute Navigation

```csharp
// Navigate to shell content
Navigation.Root<MainPageModel>()

// Navigate and push
Navigation.Root<SettingsPageModel>().Add<DetailPageModel>()

// Custom route (long form only: a string argument to Root<T> means a route, not an intent)
Navigation.Absolute().Root<MainPageModel>("custom-route")
```

### XAML Navigation

```xml
<!-- Pop -->
<Button Command="{nalu:NavigateCommand}" Text="Back">
    <Button.CommandParameter>
        <nalu:RelativeNavigation>
            <nalu:NavigationPop />
        </nalu:RelativeNavigation>
    </Button.CommandParameter>
</Button>

<!-- Push -->
<Button Command="{nalu:NavigateCommand}" Text="Details">
    <Button.CommandParameter>
        <nalu:RelativeNavigation>
            <nalu:NavigationSegment Type="pages:DetailPage" />
        </nalu:RelativeNavigation>
    </Button.CommandParameter>
</Button>
```

## Lifecycle Events Overview

Nalu provides **async/await lifecycle events**. **Implement only the interfaces you need** - most pages use just 1-2:

```csharp
// Simple page - just load data when appearing
public class ContactListPageModel : ObservableObject, IAppearingAware
{
    public async ValueTask OnAppearingAsync()
    {
        await LoadContactsAsync();
    }
}
```

**Available lifecycle interfaces:**

```csharp
public class MyPageModel : ObservableObject, 
    IEnteringAware,      // Before animation starts (keep fast!)
    IAppearingAware,     // After animation completes
    IDisappearingAware,  // Before leaving
    ILeavingAware,       // Being removed from stack
    IDisposable          // After disposal
{
    public async ValueTask OnEnteringAsync()
    {
        // Fast initialization - delays animation
        await QuickSetupAsync();
    }

    public async ValueTask OnAppearingAsync()
    {
        // Slow operations - show loading indicator
        await LoadDataAsync();
    }

    public ValueTask OnDisappearingAsync()
    {
        StopTimers();
        return ValueTask.CompletedTask;
    }

    public ValueTask OnLeavingAsync()
    {
        UnsubscribeEvents();
        return ValueTask.CompletedTask;
    }
    
    public void Dispose()
    {
        // Dispose resources
    }
}
```

**Event order**: `Entering` → Animation → `Appearing` → ... → `Disappearing` → `Leaving` → Animation → `Dispose`

**Important notes:**
- `OnAppearingAsync` and `OnDisappearingAsync` fire **multiple times** (when returning from child pages)
- `OnEnteringAsync` and `OnLeavingAsync` fire **once** per stack entry
- For slow operations (>500ms) in `OnEnteringAsync`, use the [Background Loading Pattern](navigation-lifecycle.md#background-loading-pattern) to avoid blocking navigation

> 📘 **Deep dive**: See [Navigation Lifecycle](navigation-lifecycle.md) for timing details, choosing the right interface, and advanced patterns.

## Passing Data with Intents

**Intents** are strongly-typed data passed during navigation:

```csharp
// Define intent
public record ContactIntent(int ContactId);

// Navigate with intent
await _navigationService.GoToAsync(
    Navigation.Relative()
        .Push<ContactDetailPageModel>()
        .WithIntent(new ContactIntent(42))
);

// Receive intent
public class ContactDetailPageModel : IEnteringAware<ContactIntent>
{
    public async ValueTask OnEnteringAsync(ContactIntent intent)
    {
        await LoadContactAsync(intent.ContactId);
    }
}
```

**Awaitable intents** for getting results back:

```csharp
// Define awaitable intent
public class SelectContactIntent : AwaitableIntent<Contact?> { }

// Navigate and await result in one call
var intent = new SelectContactIntent();
var selectedContact = await _navigationService.ResolveIntentAsync<ContactSelectionPageModel, Contact?>(intent);
```

```csharp
// Pushed page sets the result and navigates back
intent.SetResult(new Contact("Jane Doe"));
await navigationService.GoToAsync(Navigation.Relative().Pop());
```

> 📘 **Deep dive**: See [Navigation Intents](navigation-intents.md) for returning results, awaitable intents, intent behaviors, and patterns.

## Navigation Guards

Prevent navigation to confirm unsaved changes:

```csharp
public class EditPageModel : ObservableObject, ILeavingGuard
{
    public async ValueTask<bool> CanLeaveAsync()
    {
        if (!HasUnsavedChanges) return true;
        
        return await DisplayAlert(
            "Unsaved Changes",
            "Leave without saving?",
            "Leave", "Stay"
        );
    }
}
```

Bypass guards when needed:

```csharp
Navigation.Relative(NavigationBehavior.IgnoreGuards).Pop()
```

> 📘 **Deep dive**: See [Advanced Navigation](navigation-advanced.md) for behaviors, scoped services, and leak detection.

## Testing Navigation

```csharp
// Arrange
var navigationService = Substitute.For<INavigationService>();
var viewModel = new MyViewModel(navigationService);

// Act
await viewModel.NavigateToDetailsAsync(5);

// Assert
var expectedNav = Navigation.Relative()
    .Push<DetailPageModel>()
    .WithIntent(new DetailIntent(5));

await navigationService.Received().GoToAsync(
    Arg.Is<Navigation>(n => n.Matches(expectedNav))
);
```

> 📘 **Deep dive**: See [Testing and Troubleshooting](navigation-testing.md) for complete testing patterns and common issues.

## Custom Tab Bar

Nalu provides a customizable tab bar feature that works with both standard MAUI Shell and `NaluShell`. This feature is **independent** of Nalu's MVVM navigation system and allows you to replace the native tab bar with a fully customizable cross-platform view.

This feature also solves the issues `Shell` has with pages under the iOS `More` tab.

> 📘 **See**: [Custom Tab Bar](navigation-tabbar.md) for complete documentation on using custom tab bars, including setup, styling options, and platform-specific considerations.

## Common Patterns

### Initialization Flow

```csharp
// Start with a splash page
public AppShell(INavigationService navigationService) 
    : base(navigationService, typeof(InitPage), new StartupIntent())
{ }

// In the InitPage ViewModel - use IAppearingAware
public class InitPageModel : IAppearingAware<StartupIntent>
{
    private readonly IDispatcher _dispatcher;
    private readonly INavigationService _navigationService;

    public async ValueTask OnAppearingAsync(StartupIntent intent)
    {
        await LoadDataAsync();
        
        // Must dispatch - can't navigate directly from lifecycle event
        _ = _dispatcher.DispatchAsync(() =>
            _navigationService.GoToAsync(
                Navigation.Absolute(NavigationBehavior.Immediate).Root<HomePageModel>()
            )
        );
    }
}
```

### Tab Bar with Multiple Stacks

```xml
<TabBar>
    <Tab Title="Home">
        <ShellContent nalu:Navigation.PageType="pages:HomePage"/>
    </Tab>
    <Tab Title="Search">
        <ShellContent nalu:Navigation.PageType="pages:SearchPage"/>
    </Tab>
</TabBar>
```

Each tab maintains its own navigation stack independently.

## Best Practices

1. ✅ Use interfaces for ViewModels (better testing)
2. ✅ Use `record` types for intents (convenient value equality in unit tests)
3. ✅ Keep `OnEnteringAsync` fast (<30ms) - or use [Background Loading Pattern](navigation-lifecycle.md#background-loading-pattern) for slow operations
4. ✅ Use `IAppearingAware` for operations that should run when returning from child pages
5. ✅ Implement `IDisposable` for cleanup (i.e. when using `Timer`)
6. ✅ Enable leak detection in development
7. ✅ **Match cleanup to creation**: Constructor → Dispose, Entering → Leaving, Appearing → Disappearing
8. ✅ **Dispatch navigation from lifecycle events** - use `IDispatcher.DispatchAsync()` to avoid blocking
9. ✅ **Navigate from the UI thread** - hop back with `MainThread.InvokeOnMainThreadAsync()` after background work
10. ✅ **Check the `bool` returned by `GoToAsync`** - it is `false` when a guard blocked the navigation or a concurrent one superseded it

## Learn More

- 📘 [Navigation Lifecycle](navigation-lifecycle.md) - Deep dive into lifecycle events and timing
- 📘 [Navigation Intents](navigation-intents.md) - Passing data and returning results
- 📘 [Advanced Navigation](navigation-advanced.md) - Guards, behaviors, scoped services, and leak detection
- 📘 [State Restoration](navigation-restore.md) - Land exactly where you were after an app restart
- 📘 [Custom Tab Bar](navigation-tabbar.md) - Customizable tab bar for iOS/Android/MacCatalyst (works with standard Shell too)
- 📘 [Testing & Troubleshooting](navigation-testing.md) - Unit testing and common issues

## Migration from Shell

| Shell Navigation | Nalu Navigation |
|-----------------|-----------------|
| `await Shell.Current.GoToAsync("page")` | `await _navigationService.GoToAsync(Navigation.Relative().Push<PageModel>())` |
| `await Shell.Current.GoToAsync("..")` | `await _navigationService.GoToAsync(Navigation.Relative().Pop())` |
| `await Shell.Current.GoToAsync("//route")` | `await _navigationService.GoToAsync(Navigation.Absolute().Root<PageModel>())` |
| Query parameters | Strongly-typed intents |
| `OnNavigatedTo` / `OnNavigatedFrom` | `IEnteringAware` / `ILeavingAware` / `IAppearingAware` / `IDisappearingAware` |

## API Reference

For complete API documentation, see the [API reference](../api/Nalu.yml).
