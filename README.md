![Banner](https://raw.githubusercontent.com/nalu-development/nalu/main/Images/Banner.png)

# Nalu [![GitHub Actions Status](https://github.com/nalu-development/nalu/actions/workflows/build.yml/badge.svg?branch=main)](https://github.com/nalu-development/nalu/actions/workflows/build.yml)

## Nalu.Maui [![Nalu.Maui NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.svg)](https://www.nuget.org/packages/Nalu.Maui/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui)](https://www.nuget.org/packages/Nalu.Maui/)

Nalu.Maui is a library that solves some problems like navigation between pages in a MAUI application.

### Navigation

Unfortunately MAUI navigation (NavigationPage, or Shell) do not provide automatic page/view model disposal as [widely explained in this issue](https://github.com/dotnet/maui/issues/7354).
This is a problem because it can lead to memory/event leaks.

There are other big issues with Shell navigation:
- Routes needs to be verified ahead of time, so you can't use dynamic routes.
  Well, there's a way to do that via `Routing.RegisterRoute` but it's not very convenient.
- `Shell.Current.GoToAsync` API is really hard to understand: can you easily tell what's the difference between `GoToAsync("Page1")` / `GoToAsync("/Page1")` / `GoToAsync("//Page1")` / `GoToAsync("///Page1")`?
- Root pages (defined as `ShellContent` will never be removed from the navigation stack, even if you navigate to a different root page.
- Have you ever wonder what's the difference between `Transient` and `Scoped` service lifetime in MAUI?
- The way to pass parameters is a bit inconvenient
- There's no way to define something and provide that value to all nested pages (like a context)

On the other hand, `Shell` offers a convenient way to define root pages and have a ready-to use flyout menu.

Nalu navigation is based on `Shell` navigation, but it solves all the issues above.

#### Dependency injection the right way

With Nalu navigation, a `ServiceScope` is created for each page, so you can use `Scoped` services in your pages and view models.
Pages and view models are in fact registered as `Scoped` services and automatically disposed by the `ServiceScope` when the page is removed from the navigation stack.

#### Initial setup

First of all, you need to add the Nalu.Maui package to your project, then just call `UseNaluNavigation` in your `MauiProgram`:

```csharp
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseNaluNavigation<App>(nav => nav.AddPages())
```

The `AddPages` method will scan the `<App>` assembly for pages and view models by naming convention `MainPage` => `MainPageModel`.
You can specify a custom naming convention by passing a function that returns the view model type given the page type:

```csharp
builder
    .UseMauiApp<App>()
    .UseNaluNavigation<App>(nav => nav.AddPages((pageType) => pageType.Name.Replace("Page", "ViewModel")))
```

**Important notes**:
- page models needs to implement `INotifyPropertyChanged` interface
- pages need to require the view model as constructor parameter and assign it to the `BindingContext` property

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

Eventually you can specify the pages and view models manually:

```csharp
builder
    .UseMauiApp<App>()
    .UseNaluNavigation<App>(nav => nav.AddPage<MainPageModel, MainPage>())
```

To help with testability you can also register the page model as an interface:

```csharp
builder
    .UseMauiApp<App>()
    .UseNaluNavigation<App>(nav => nav.AddPage<IMainPageModel, MainPageModel, MainPage>())
```

Note: the automatic registration by naming convention automatically considers the page model as an interface.

Due to some issues in MAUI, we need to define `ImageSource` for the menu button and back button displayed in the navigation bar.
Nalu navigation already provides them, but you can override them if you want:

```csharp
builder
    .UseMauiApp<App>()
    .UseNaluNavigation<App>(nav => nav.AddPages()
        .WithMenuIcon(ImageSource.FromFile("menu.png"))
        .WithBackIcon(ImageSource.FromFile("back.png")))
```

#### Shell definition

Nalu navigation is based on `Shell` navigation, so you need to define your `Shell` in `AppShell.xaml` by inheriting from `NaluShell`.
Use `nalu:Navigation.PageModel` to specify the page model for each `ShellContent`.

Important:
- all the root pages must be defined as `ShellContent`
- to make the flyout work correctly either
  - define at least two root pages
  - or set the `FlyoutBehavior` to `FlyoutBehavior.Flyout`

```xml
<?xml version="1.0" encoding="utf-8"?>

<nalu:NaluShell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                xmlns:pageModels="clr-namespace:Nalu.Maui.Sample.PageModels"
                xmlns:nalu="https://nalu-development.github.com/nalu"
                x:Class="Nalu.Maui.Sample.AppShell">
    <FlyoutItem Route="main"
                FlyoutDisplayOptions="AsMultipleItems">
        <Tab Title="Pages"
             Route="pages">
            <ShellContent nalu:Navigation.PageModel="pageModels:OnePageModel"
                          Title="Page One"/>
            <ShellContent nalu:Navigation.PageModel="pageModels:TwoPageModel"
                          Title="Page Two"/>
        </Tab>
    </FlyoutItem>
    <ShellContent nalu:Navigation.PageModel="pageModels:FivePageModel"
                  Title="Page Five"/>
</nalu:NaluShell>
```

In the code behind you need to set the initial shell page using the `ConfigureNavigation` method:

```csharp
public partial class AppShell : NaluShell
{
    public AppShell()
    {
        InitializeComponent();
        ConfigureNavigation<OnePageModel>();
    }
}
```

#### Navigation events

The page view model can selectively react to navigation events by implementing the following interfaces:

- `IEnteringAware`: defines a `ValueTask OnEnteringAsync()` called when the page is entering the navigation stack
- `IAppearingAware`: defines a `ValueTask OnAppearingAsync()` called when the page is appearing
- `IDisappearingAware`: defines a `ValueTask OnDisappearingAsync()` called when the page is disappearing
- `ILeavingAware`: defines a `ValueTask OnLeavingAsync()` called when the page is leaving the navigation stack

With Nalu navigation you can also pass parameters to the target page using the `IntentAware` interfaces:
- `IEnteringAware<TIntent>`: defines a `ValueTask OnEnteringAsync(TIntent intent)` called when the page is entering the navigation stack
- `IAppearingAware<TIntent>`: defines a `ValueTask OnAppearingAsync(TIntent intent)` called when the page is appearing

Note: when an intent is passed to the view model, the `OnEnteringAsync` and `OnAppearingAsync` parameterless methods will not be called.
Obviously you can call them manually from the intent-aware one if you need to.

Sometimes you want to protect a page from being popped from the navigation stack, for example when the user is editing a form.
You can do that by implementing the `ILeavingGuard` interface which defines a `ValueTask<bool> CanLeaveAsync()` method from which you can eventually display a prompt to ask the user if they want to leave the page.

Note: a page "appears" only when it is the target of the navigation, intermediate pages models will trigger `OnAppearingAsync` unless the `ILeavingGuard` stops the navigation on that page.

#### Navigation using C#

First of all, you need to inject the `INavigationService` in your page model:

```csharp
public class OnePageModel : IOnePageModel
{
    private readonly INavigationService _navigationService;

    public OnePageModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }
}
```

Then you can use the `GoToAsync` method to navigate to a page using relative or absolute navigation:

```csharp
// Add a page to the navigation stack
await _navigationService.GoToAsync(Navigation.Relative().Push<TwoPageModel>());
// Add a page to the navigation stack providing an intent
var myIntent = new MyIntent(/* ... */);
await _navigationService.GoToAsync(Navigation.Relative(myIntent).Push<TwoPageModel>());
// Remove the current page from the navigation stack
await _navigationService.GoToAsync(Navigation.Relative().Pop());
// Remove the current page from the navigation stack providing an intent to the previous page
var myIntent = new MyResult(/* ... */);
await _navigationService.GoToAsync(Navigation.Relative(myIntent).Pop())
// Pop two pages than push a new one
await _navigationService.GoToAsync(Navigation.Relative().Pop().Pop().Push<ThreePageModel>());
// Pop to the root page using absolute navigation
await _navigationService.GoToAsync(Navigation.Absolute().Add<MainPageModel>());
```

Note, non-sense navigations will throw an exception, for example: pop -> push -> pop.

#### Navigation using XAML

Nalu provides a `Navigation` markup extension that can be used to navigate to a page using relative or absolute navigation:

```xml
<!-- Pops the current page -->
<Button Command="{nalu:NavigateCommand}" Text="Back">
    <Button.CommandParameter>
        <nalu:RelativeNavigation>
            <nalu:NavigationPop />
        </nalu:RelativeNavigation>
    </Button.CommandParameter>
</Button>
```

```xml
<!-- Push a new page onto the navigation stack with intent -->
<Button Command="{nalu:NavigateCommand}" Text="Push some page">
    <Button.CommandParameter>
        <nalu:RelativeNavigation Intent="{Binding MyIntentValue}">
             <nalu:NavigationSegment x:TypeArguments="pageModels:SomePageModel" />
        </nalu:RelativeNavigation>
    </Button.CommandParameter>
</Button>
```

```xml
<!-- Navigate to main page -->
<Button Command="{nalu:NavigateCommand}" Text="Go to main page">
    <Button.CommandParameter>
        <nalu:AbsoluteNavigation>
            <nalu:NavigationSegment x:TypeArguments="pageModels:MainPageModel" />
        </nalu:AbsoluteNavigation>
    </Button.CommandParameter>
</Button>
```

#### Advanced scenario: navigation-scoped services

Sometimes you need to share a service between pages, starting from a specific page down to all the nested pages.

Nalu navigation provides an `INavigationServiceProvider` service that can be used to provide services to nested page.

In the page model where you want to start providing the service, you need to inject the `INavigationServiceProvider` and call the `Provide` method:

```csharp
public class PersonPageModel(INavigationServiceProvider navigationServiceProvider) : IPersonPageModel // which inherits from IEnteringAware<int>
{
    public ValueTask OnEnteringAsync(int personId)
    {
        var personContext = new PersonContext(personId);
        navigationServiceProvider.AddNavigationScoped<IPersonContext>(personContext);
    }
}
```

Then you can inject the service in the nested page models through the `INavigationServiceProvider`:

```csharp
public class PersonDetailsPageModel(INavigationServiceProvider navigationServiceProvider) : IPersonDetailsPageModel
{
    private readonly IPersonContext _personContext = navigationServiceProvider.GetRequiredService<IPersonContext>();
}
```

#### Sample application

This repository contains a `Nalu.Maui.Sample` project that shows how to use Nalu navigation.
Play with it to better see how it works.

#### Do you just care about disposing page view model?

If you're here just because you want page/vm disposal on the standard `NavigationPage` or `Shell` and you don't want these awsome features, you can just use the following methods to enable calling `Dispose` on page models after page has been removed from navigation stack.

```csharp
var navigationPage = new NavigationPage(new MainPage()).ConfigureForPageDisposal();
```

```csharp
public AppShell() {
    InitializeComponent();
    this.ConfigureForPageDisposal();
}
```
