<h2 id="nalumaui">Nalu.Maui<span></span></h2>

`Nalu.Maui` provides a set of classes to help you with everyday challenges encountered while working with .NET MAUI.

If `Nalu.Maui` is valuable to your work, consider supporting its continued development and maintenance ❤️

[![Sponsor](https://img.shields.io/badge/Sponsor-%E2%9D%A4-pink?logo=github&style=for-the-badge)](https://github.com/sponsors/albyrock87)

---

### Layouts [![Nalu.Maui.Layouts NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Layouts.svg)](https://www.nuget.org/packages/Nalu.Maui.Layouts/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Layouts)](https://www.nuget.org/packages/Nalu.Maui.Layouts/)

Cross-platform layouts and utilities for MAUI applications simplify dealing with templates and `BindinginContext` in XAML.

- Have you ever dreamed of having an `if` statement in XAML?
  ```csharp
    <nalu:ToggleTemplate Value="{Binding HasPermission}"
                         WhenTrue="{StaticResource AdminFormTemplate}"
                         WhenFalse="{StaticResource PermissionRequestTemplate}" />
  ```
- Do you want to scope the binding context of a content?
  ```csharp
    <nalu:ViewBox ContentBindingContext="{Binding SelectedAnimal}"
                  IsVisible="{Binding IsSelected}">
        <views:AnimalView x:DataType="models:Animal" />
    </nalu:ViewBox>
  ```
- And what about rendering a `TemplateSelector` directly like we do on a `CollectionView`?
  ```csharp
    <nalu:TemplateBox ContentTemplateSelector="{StaticResource AnimalTemplateSelector}"
                      ContentBindingContext="{Binding CurrentAnimal}" />
  ```

**Find out more on the [Layouts Wiki](layouts.md)**.

---

### Navigation [![Nalu.Maui.Navigation NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Navigation.svg)](https://www.nuget.org/packages/Nalu.Maui.Navigation/) [![Nalu.Maui NuGet Package Downloads](https://www.nuget.org/packages/Nalu.Maui.Navigation)](https://www.nuget.org/packages/Nalu.Maui.Navigation/)

The MVVM navigation service offers a straightforward and robust method for navigating between pages and passing parameters.

The navigation system utilizes `Shell` under the hood, allowing you to easily define the flyout menu, tabs, and root pages.

We use a **fluent API** instead of strings to define navigations, supporting both `Relative` and `Absolute` navigation, including navigation guards to prompt the user before leaving a page.

```csharp
// Push the page registered with the DetailPageModel
await _navigationService.GoToAsync(Navigation.Relative().Push<DetailPageModel>());
// Navigate to the `SettingsPageModel` root page
await _navigationService.GoToAsync(Navigation.Absolute().Root<SettingsPageModel>());
```

Passing parameters is simple and type-safe.

```csharp
// Pop the page and pass a parameter to the previous page model
await _navigationService.GoToAsync(Navigation.Relative().Pop().WithIntent(new MyPopIntent()));
// which should implement `IAppearingAware<MyPopIntent>`
Task OnAppearingAsync(MyPopIntent intent) { ... }
```

You can also define navigation guards to prevent navigation from occurring.

```csharp
ValueTask<bool> CanLeaveAsync() => { ... ask the user };
```

There is an embedded **leak-detector** to help you identify memory leaks in your application.

**See more on the [Navigation Wiki](navigation.md)**.

---

### VirtualScroll [![Nalu.Maui.VirtualScroll NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.VirtualScroll.svg)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.VirtualScroll)](https://www.nuget.org/packages/Nalu.Maui.VirtualScroll/)

A **high-performance** virtualized scrolling view designed to replace the traditional `CollectionView` in .NET MAUI applications.

- Optimized for Android (`RecyclerView`) and Apple's (`UICollectionView`)
- Based on an adapter pattern and also full support for `ObservableCollection<T>` with change notifications (add, remove, move, replace)
- **Dynamic item sizing** with automatic layout updates
- Pull-to-refresh support
- Header, footer, and section templates
- Carousel mode

```xml
<nalu:VirtualScroll ItemsSource="{Binding Items}">
    <nalu:VirtualScroll.ItemTemplate>
        <DataTemplate x:DataType="models:MyItem">
            <nalu:ViewBox>
                <Label Text="{Binding Name}" Padding="16" />
            </nalu:ViewBox>
        </DataTemplate>
    </nalu:VirtualScroll.ItemTemplate>
</nalu:VirtualScroll>
```

> **Note:** This package uses a **Non-Commercial License**.

**Find out more on the [VirtualScroll Wiki](virtualscroll.md)**.

---

### Core [![Nalu.Maui.Core NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Core.svg)](https://www.nuget.org/packages/Nalu.Maui.Core/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Core)](https://www.nuget.org/packages/Nalu.Maui.Core/)

The core library is intended to provide a set of common use utilities.

#### Do you have issues with the soft keyboard?

Nalu offers an alternative soft-keyboard manager that allows consistent behavior across Android and iOS, enabling the Resize/Pan mode on iOS too in a very convenient way.

```xml
<ContentPage>
    <Grid>
        <!-- Every input field inside this layout will use the Pan screen adjust mode -->
        <VerticalStackLayout naluCore:SoftKeyboardManager.SoftKeyboardAdjustMode="Pan">
            <Entry />
```

You can also easily bind the visibility of an element to the visibility of the keyboard using the `SoftKeyboardManager.State` observable object.

```xml
<VerticalStackLayout.IsVisible>
    <!-- example to show an area only when the keyboard is hidden -->
    <Binding Path="IsHidden" Source="{x:Static nalu:SoftKeyboardManager.State}" x:DataType="nalu:SoftKeyboardState" />
</VerticalStackLayout.IsVisible>
```

#### Have you noticed failed network requests when the app is backgrounded on iOS?

Have you ever noticed that when the user backgrounds the app on iOS, the app is suspended, and the network requests will fail due to `The network connection was lost`?

This is really annoying: it forces us to implement complex retry logic, especially considering that the request may have already hit the server.

To solve this issue, we provide a `NSUrlBackgroundSessionHttpMessageHandler` to be used in your `HttpClient` to allow http request to continue even when the app is in the background.

```csharp
#if IOS
    var client = new HttpClient(new NSUrlBackgroundSessionHttpMessageHandler());
#else
    var client = new HttpClient();
#endif
```

**Check out the [Core Wiki](core.md) for more information**.

---

### Controls [![Nalu.Maui.Controls NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Controls.svg)](https://www.nuget.org/packages/Nalu.Maui.Controls/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Controls)](https://www.nuget.org/packages/Nalu.Maui.Controls/)

The controls library provides a set of cross-platform controls to simplify your development.

- A `InteractableCanvasView` which is a `SkiaSharp` `SKCanvasView` with touch-events support where you can choose to stop touch event propagation to avoid interaction with ancestors (like `ScrollView`)
- A `TimeSpan?` edit control named `DurationWheel` which allows the user to enter a duration by spinning a wheel!!

**Find out more on the [Controls Wiki](controls.md)**.

---

### SharpState [![Nalu.SharpState NuGet Package](https://img.shields.io/nuget/v/Nalu.SharpState.svg)](https://www.nuget.org/packages/Nalu.SharpState/) [![Nalu.SharpState NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.SharpState)](https://www.nuget.org/packages/Nalu.SharpState/)

A compile-time, AOT-friendly state machine powered by a Roslyn source generator. Declare states and triggers with attributes, describe transitions with a strongly-typed fluent API, and let the generator emit a ready-to-use `Actor` with typed trigger methods.

```csharp
[StateMachineDefinition(typeof(DoorContext))]
public partial class DoorMachine
{
    [StateTriggerDefinition] static partial void Open(string reason);
    [StateTriggerDefinition] static partial void Close();

    [StateDefinition]
    private static IStateConfiguration Closed => ConfigureState()
        .OnOpen(t => t
            .Target(State.Opened)
            .Invoke((ctx, reason) => ctx.LastReason = reason));

    [StateDefinition]
    private static IStateConfiguration Opened => ConfigureState()
        .OnClose(t => t.Target(State.Closed));
}
```

```csharp
var door = DoorMachine.CreateActor(new DoorContext());
door.Open("delivery");
Console.WriteLine(door.CurrentState); // Opened
```

- **Strongly typed** trigger methods, guards, and actions — no `object[]` bags, no reflection.
- **Hierarchical state machines** via nested `[SubStateMachine]` partial classes with strict scoping rules.
- **Sync or async** machines with one flag on the definition attribute.
- **Compile-time diagnostics** (`NSS001`-`NSS010`) catch mistakes before you ship.

**Find out more on the [SharpState Wiki](sharpstate.md)**.