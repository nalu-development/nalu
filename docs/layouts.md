## Layouts [![Nalu.Maui.Layouts NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Layouts.svg)](https://www.nuget.org/packages/Nalu.Maui.Layouts/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Layouts)](https://www.nuget.org/packages/Nalu.Maui.Layouts/)

Cross-platform layouts for MAUI applications to simplify dealing with templates and `BindinginContext` in XAML.

Can be added to your project using `.UseNaluLayouts()` on your app builder.

### Component

`Component` is a lightweight replacement for `ContentView` class which is still based on the legacy Xamarin `Compatibility.Layout`.
You can simply use `Component` as base class of your custom views instead of using `ContentView`.

This class also provides a `ContentBindingContext` property that allows you to bind the content to a property of the `Component`'s binding context.
This helps to fulfill interface segregation principle.

```xml
<layouts:Component ContentBindingContext="{Binding SelectedAnimal}"
                   IsVisible="{Binding IsSelected}">
    <views:AnimalView x:DataType="models:Animal" />
</layouts:Component>
```

### TemplatedComponent

A `Component` that generates a content view based on `DataTemplate` or `DataTemplateSelector`.

```xml
<layouts:TemplatedComponent ContentTemplate="{StaticResource AnimalTemplate}" ContentBindingContext="{Binding CurrentAnimal}" />
<!-- or -->
<layouts:TemplatedComponent ContentTemplate="{StaticResource AnimalTemplateSelector}" ContentBindingContext="{Binding CurrentAnimal}" />
```

You can also project the content into the template. The following example will display `Projected => I'm here!`.

```xml
<layouts:TemplatedComponent>

    <layouts:TemplatedComponent.ContentTemplate>
        <DataTemplate>
            <HorizontalStackLayout>
                <Label Text="Projected => " />
                <layouts:ProjectContainer />
            </HorizontalStackLayout>
        </DataTemplate>
    </layouts:TemplatedComponent.ContentTemplate>

    <Label Text="I'm here!" />

</layouts:TemplatedComponent>
```

### ConditionedTemplate

Usually to switch between views we use `IsVisible` property, but this is not always the best solution.
Using `IsVisible` still creates the view in the visual tree applying all the bindings.
`ConditionedTemplate` is a `TemplatedComponent` that generates a content view based on a boolean value and corresponding template(s).

```xml
<layouts:ConditionedTemplate Value="{Binding HasPermission}"
                             TrueTemplate="{StaticResource AdminFormTemplate}"
                             FalseTemplate="{StaticResource PermissionRequestTemplate}" />
```

This can also be used with one single expensive template:
```xml
<layouts:ConditionedTemplate Value="{Binding ShowExpensiveTemplate}"
                             TrueTemplate="{StaticResource ExpensiveTemplate}" />
```



