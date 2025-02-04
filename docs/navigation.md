## Nalu.Maui.Navigation [![Nalu.Maui.Navigation NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Navigation.svg)](https://www.nuget.org/packages/Nalu.Maui.Navigation/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Navigation)](https://www.nuget.org/packages/Nalu.Maui.Navigation/)

### Intuitive Asynchronous Navigation Lifecycle Events

We provide a set of interfaces to react to navigation lifecycle events, and **it takes care of disposing all the resources when the page is popped**.

The navigation system awaits every navigation lifecycle event, so you can safely perform asynchronous operations with real `async ValueTask` methods instead of relying on `async void` ones.

```csharp
Navigation.Relative()
    .Push<ContactsPageModel>()
    .Push<ContactDetailPageModel>()
```

![Push twice in a row](assets/images/push-push.png)

There's also a "navigation guard" feature that allows you to prevent the navigation to happen if a condition is not met.
You can leverage that to ask the user to confirm leaving the page.

```csharp
// Starting from: RootPageModel > ContactsPageModel > ContactDetailPageModel
Navigation.Absolute()
    .ShellContent<RootPageModel>()

// This is gonna trigger the navigation guard on ContactDetailPageModel
ValueTask<bool> CanLeaveAsync() => { ... ask the user };
```

![Absolute navigation to root page](assets/images/pop-pop-with-guard.png)

### Leak detection

`Nalu.Maui` automatically detects and reports memory leaks when the debugger is attached.
An alert dialog will be shown when your `Page` was not collected after navigating away.

In the above example the leak detection on `ContactsPageModel`, `ContactDetailPageModel` and their respective `Page`s triggers after the navigation completed (root page appears).

Leak detection can be enabled in `MauiProgram.cs`:

```csharp
.UseNaluNavigation<App>(nav => nav
    .AddPages()
    .WithLeakDetectorState(NavigationLeakDetectorState.EnabledWithDebugger)
)
```

### Implementation in detail

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
            .UseNaluNavigation<App>()
```

This method will scan the `<App>` assembly for pages and view models by naming convention `MainPage` => `MainPageModel`.
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

#### Initial setup - without MVVM pattern

Nalu can be used even without MVVM pattern, just add your pages as `Scoped` services:

```csharp
builder
    .UseNaluNavigation<App>()
    .Services
    .AddScoped<MyPage>();
```

#### Customizing the appearance of the navigation bar

You can eventually customize in one place all the back and menu icons.

```csharp
builder
    .UseMauiApp<App>()
    .UseNaluNavigation<App>(nav => nav.AddPages()
        .WithMenuIcon(ImageSource.FromFile("menu.png"))
        .WithBackIcon(ImageSource.FromFile("back.png")))
```

#### Shell definition

Nalu navigation is based on `Shell` navigation, so you need to define your `Shell` in `AppShell.xaml` by inheriting from `NaluShell`.
Use `nalu:Navigation.PageType` to specify the page type for each `ShellContent`.

```xml
<?xml version="1.0" encoding="utf-8"?>

<nalu:NaluShell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                xmlns:pages="clr-namespace:Nalu.Maui.Sample.PageModels"
                xmlns:nalu="https://nalu-development.github.com/nalu/navigation"
                x:Class="Nalu.Maui.Sample.AppShell">
    <FlyoutItem Route="main"
                FlyoutDisplayOptions="AsMultipleItems">
        <Tab Title="Pages"
             Route="pages">
            <ShellContent nalu:Navigation.PageType="pages:OnePage"
                          Title="Page One"/>
            <ShellContent nalu:Navigation.PageType="pages:TwoPage"
                          Title="Page Two"/>
        </Tab>
    </FlyoutItem>
    <ShellContent nalu:Navigation.PageType="pages:FivePage"
                  Title="Page Five"/>
</nalu:NaluShell>
```

In the code behind you need to set the initial shell page passing the navigation service and the initial page type to the base constructor:

```csharp
public partial class App : Application
{
    private readonly INavigationService _navigationService;

    public App(INavigationService navigationService)
    {
        _navigationService = navigationService;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState) => new(new AppShell(_navigationService));
}

public partial class AppShell : NaluShell
{
    public AppShell(INavigationService navigationService) : base(navigationService, typeof(OnePage))
    {
        InitializeComponent();
    }
}
```

#### Navigation concepts

`Shell` structure is based on `Item` > `Section` > `Content` hierarchy.
Even when you don't specify an `Item` or a `Section` in the `Shell` definition, it is automatically created for you.

For example, the following `Shell` definition

```xml
<nalu:NaluShell>
    <ShellContent nalu:Navigation.PageType="pages:OnePage"/>
    <ShellContent nalu:Navigation.PageType="pages:TwoPage"/>
</nalu:NaluShell>
```

is equivalent to

```xml
<nalu:NaluShell>
    <ShellItem>
        <ShellSection>
            <ShellContent nalu:Navigation.PageType="pages:OnePage"/>
        </ShellSection>
    </ShellItem>
    <ShellItem>
        <ShellSection>
            <ShellContent nalu:Navigation.PageType="pages:TwoPage"/>
        </ShellSection>
    </ShellItem>
</nalu:NaluShell>
```

That said, `Nalu` navigation provides the following navigation behavior when switching between `ShellContent`s:
- if the content is in the same `ShellSection`, navigation stack will be popped
- if the content is in a different `ShellSection` but in the same `ShellItem`, the current navigation stack will be persisted
- if the content is in a different `ShellItem`, all of the current item's navigation stacks will be popped and the `ShellContent` pages will be destroyed

You can customize this behavior by providing a custom `NavigationBehavior` to the `Navigation` object.

For example you can also use the `IgnoreGuards` behavior to ignore the `ILeavingGuard` when popping a page:

```csharp
await _navigationService.GoToAsync(Navigation.Relative(NavigationBehavior.IgnoreGuards).Pop());
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

Note: when an intent is passed to the view model, the `OnEnteringAsync` and `OnAppearingAsync` parameterless methods will be invoked only if the intent-specific method is not implemented.
You can change this behavior by configuring Nalu with `Strict` behavior.

```csharp
.UseNaluNavigation<App>(nav => nav
    .AddPages()
    .WithNavigationIntentBehavior(NavigationIntentBehavior.Strict)
)
```

Obviously nothing stops you to call the intent-less ones manually from the intent-aware one if you need to.

#### Leaving guards

Sometimes you want to protect a page from being popped from the navigation stack, for example when the user is editing a form.
You can do that by implementing the `ILeavingGuard` interface which defines a `ValueTask<bool> CanLeaveAsync()` method from which you can eventually display a prompt to ask the user if they want to leave the page.

```csharp
public class ViewModel :  ILeavingGuard
{
    public async ValueTask<bool> CanLeaveAsync()
    {
        return await ConfirmUserLeaveAsync("Are you sure you want to leave without saving?") // a method to verify the leave action
    }
}
```

Note: a page "appears" only when it is the target of the navigation, intermediate pages models will trigger `OnAppearingAsync` unless the `ILeavingGuard` needs to be evaluated.

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
await _navigationService.GoToAsync(Navigation.Relative().Push<TwoPageModel>().WithIntent(myIntent));
// Remove the current page from the navigation stack
await _navigationService.GoToAsync(Navigation.Relative().Pop());
// Remove the current page from the navigation stack providing an intent to the previous page
var myIntent = new MyResult(/* ... */);
await _navigationService.GoToAsync(Navigation.Relative().Pop().WithIntent(myIntent))
// Pop two pages than push a new one
await _navigationService.GoToAsync(Navigation.Relative().Pop().Pop().Push<ThreePageModel>());
// Pop to the root page using absolute navigation
await _navigationService.GoToAsync(Navigation.Absolute().ShellContent<MainPageModel>());
// Switch to a different shell content and push a page there
await _navigationService.GoToAsync(Navigation.Absolute().ShellContent<OtherPageModel>().Push<OtherChildPageModel>());
```

Note:
- if you don't want to use MVVM pattern just use page types instead of page model types (i.e. `Navigation.Relative().Push<TwoPage>()`).
- non-sense navigations will throw an exception, for example: pop -> push -> pop.

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
            <nalu:NavigationSegment Type="pages:SixPage" />
        </nalu:RelativeNavigation>
    </Button.CommandParameter>
</Button>
```

```xml
<!-- Navigate to main page -->
<Button Command="{nalu:NavigateCommand}" Text="Go to main page">
    <Button.CommandParameter>
        <nalu:AbsoluteNavigation>
            <nalu:NavigationSegment x:TypeArguments="pages:MainPage" />
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

#### How to unit test navigation

Here's an example of how to unit test navigation using `NSubstitute`:

Using `record` for intents is recommended to avoid having to implement an equality comparer.
Suppose to have defined an intent class `public record AnIntent(int Value = 0);`.

```csharp
// Arrange
var navigationService = Substitute.For<INavigationService>();
navigationService.GoToAsync(Arg.Any<Navigation>()).Returns(Task.FromResult(true));
var viewModel = new MyViewModel(navigationService);

// Act
await viewModel.DoSomethingAsync(5);

// Assert
var expectedNavigation = Navigation.Relative().Push<TargetPageModel>().WithIntent(new AnIntent(5));
await navigationService.Received().GoToAsync(Arg.Is<Navigation>(n => n.Matches(expectedNavigation)));
```

#### Why Nalu navigation?

Unfortunately MAUI navigation (NavigationPage, or Shell) do not provide automatic page/view model disposal as [widely explained in this issue](https://github.com/dotnet/maui/issues/7354).
This is a problem because it can lead to memory/event leaks.

There are other big issues with Shell navigation:
- `Shell.Current.GoToAsync` API is really hard to understand: can you easily tell what's the difference between `GoToAsync("Page1")` / `GoToAsync("/Page1")` / `GoToAsync("//Page1")` / `GoToAsync("///Page1")`?
- Root pages (defined as `ShellContent` will never be disposed, even if you navigate to a different shell item.
- Have you ever wonder what's the difference between `Transient` and `Scoped` service lifetime in MAUI?
- The way to pass parameters is a bit inconvenient
- There's no way to define something and provide that value to all nested pages (like a context)
- We have to rely on `async void` methods to perform asynchronous operations on page lifecycle events

On the other hand, `Shell` offers a convenient way to define the app structure including tab bar and flyout menu.
`Shell` also supports having multiple navigation stacks alive at the same time when using a global `TabBar`.

Nalu navigation is based on `Shell` navigation, but it solves all the issues above.
