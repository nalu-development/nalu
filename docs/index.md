<h2 id="nalumaui"><a style="text-decoration:none;padding:8px 16px;color: #2C479D;border-radius:8px;box-shadow: 0 0 4px #2C479D;font-weight: 600;background: #f6fafe;float: right;font-size: 14px;margin-top: -4px;" target="_blank" href="https://buymeacoffee.com/albyrock87">🍕&nbsp;<span class="bmc-btn-text">Buy me a pizza</span></a>Nalu.Maui<span></span></h2>

`Nalu.Maui` provides a set of classes to help you with everyday challenges encountered while working with .NET MAUI.

### Navigation [![Nalu.Maui.Navigation NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Navigation.svg)](https://www.nuget.org/packages/Nalu.Maui.Navigation/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Navigation)](https://www.nuget.org/packages/Nalu.Maui.Navigation/)

The MVVM navigation service offers a straightforward and robust method for navigating between pages and passing parameters.

The navigation system utilizes `Shell` under the hood, allowing you to easily define the flyout menu, tabs, and root pages.

We use a **fluent API** instead of strings to define navigations, supporting both `Relative` and `Absolute` navigation, including navigation guards to prompt the user before leaving a page.

```csharp
// Push the page registered with the DetailPageModel
await _navigationService.GoToAsync(Navigation.Relative().Push<DetailPageModel>());
// Navigate to the `SettingsPageModel` root page
await _navigationService.GoToAsync(Navigation.Absolute().ShellContent<SettingsPageModel>());
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

See more on the [Navigation Wiki](navigation.md).

### Layouts [![Nalu.Maui.Layouts NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Layouts.svg)](https://www.nuget.org/packages/Nalu.Maui.Layouts/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Layouts)](https://www.nuget.org/packages/Nalu.Maui.Layouts/)

Cross-platform layouts and utilities for MAUI applications simplify dealing with templates and `BindinginContext` in XAML.

- Have you ever dreamed of having an `if` statement in XAML?
  ```csharp
    <layouts:ConditionedTemplate Value="{Binding HasPermission}"
                                 TrueTemplate="{StaticResource AdminFormTemplate}"
                                 FalseTemplate="{StaticResource PermissionRequestTemplate}" />
  ```
- Do you want to scope the binding context of a content?
  ```csharp
    <layouts:Component ContentBindingContext="{Binding SelectedAnimal}"
                       IsVisible="{Binding IsSelected}">
        <views:AnimalView x:DataType="models:Animal" />
    </layouts:Component>
  ```
- And what about rendering a `TemplateSelector` directly like we do on a `CollectionView`?
  ```csharp
    <layouts:TemplatedComponent ContentTemplateSelector="{StaticResource AnimalTemplateSelector}"
                                ContentBindingContext="{Binding CurrentAnimal}" />
  ```

Find out more on the [Layouts Wiki](layouts.md).
