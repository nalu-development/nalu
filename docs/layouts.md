## Layouts [![Nalu.Maui.Layouts NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Layouts.svg)](https://www.nuget.org/packages/Nalu.Maui.Layouts/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Layouts)](https://www.nuget.org/packages/Nalu.Maui.Layouts/)

Cross-platform layouts for MAUI applications to simplify dealing with templates and `BindinginContext` in XAML.

Can be added to your project using `.UseNaluLayouts()` on your app builder.

### ViewBox

`ViewBox` is a lightweight replacement for `ContentView` class which is still based on the legacy Xamarin `Compatibility.Layout`.
You can simply use `ViewBox` as base class of your custom views instead of using `ContentView`.

This class also provides a `ContentBindingContext` property that allows you to bind the content to a property of the `ViewBox`'s binding context.
This helps to fulfill interface segregation principle.

On top of that, `ViewBox` offers the possibility to clip the content to the bounds of the view through `IsClippedToBounds` property.

```xml
<nalu:ViewBox ContentBindingContext="{Binding SelectedAnimal}"
              IsVisible="{Binding IsSelected}">
    <views:AnimalView x:DataType="models:Animal" />
</nalu:ViewBox>
```

### TemplateBox

Similarly to `ViewBox` this component holds a view based on `DataTemplate` or `DataTemplateSelector`.

```xml
<nalu:TemplateBox ContentTemplate="{StaticResource AnimalTemplate}" ContentBindingContext="{Binding CurrentAnimal}" />
```
```xml
<nalu:TemplateBox ContentTemplate="{StaticResource AnimalTemplateSelector}" ContentBindingContext="{Binding CurrentAnimal}" />
```

You can also project the content into the template (like you usually do with [ContentPresenter](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/controltemplate?view=net-maui-8.0#substitute-content-into-a-contentpresenter)).

The following example will display `Projected => I'm here!`.

```xml
<nalu:TemplateBox>

    <nalu:TemplateBox.ContentTemplate>
        <DataTemplate>
            <HorizontalStackLayout>
                <Label Text="Projected => " />
                <nalu:TemplateContentPresenter />
            </HorizontalStackLayout>
        </DataTemplate>
    </nalu:TemplateBox.ContentTemplate>

    <Label Text="I'm here!" />

</nalu:TemplateBox>
```

### ToggleTemplate

Usually to switch between views we use `IsVisible` property, but this is not always the best solution.
Using `IsVisible` still creates the view in the visual tree applying all the bindings (performance impact).
`ToggleTemplate` is a `TemplateBox` that generates a content view based on a boolean value and a corresponding `DataTemplate` (or even `DataTemplateSelector`).

```xml
<nalu:ToggleTemplate Value="{Binding HasPermission}"
                     WhenTrue="{StaticResource AdminFormTemplate}"
                     WhenFalse="{StaticResource PermissionRequestTemplate}" />
```

This can also be used with one single expensive template:
```xml
<nalu:ToggleTemplate Value="{Binding ShowExpensiveTemplate}"
                     WhenTrue="{StaticResource ExpensiveTemplate}" />
```

### ExpanderViewBox

`ExpanderViewBox` is a custom view that fully displays or **collapses** its content by **animating** the size transition.

It is useful for scenarios where you want to show or hide additional information dynamically or if you want to build an **accordion** control.

Here's an example of how we can use it to build a section that can be expanded or collapsed through a button only when the content exceeds the `CollapsedHeight`.

```csharp
private void ToggleExpended(object? sender, EventArgs e)
{
    TheExpander.IsExpanded = !TheExpander.IsExpanded;
}
```

```xml
<VerticalStackLayout>

    <!-- This button is only visible when the expander's content is bigger than the collapsed size. -->
    <Button Text="Toggle expanded"
            Clicked="ToggleExpended"
            IsVisible="{Binding Path=CanCollapse,
                                Source={x:Reference TheExpander},
                                x:DataType=nalu:ExpanderViewBox}"/>

    <nalu:ExpanderViewBox x:Name="TheExpander"
                          BackgroundColor="Coral"
                          CollapsedHeight="200">

        <VerticalStackLayout VerticalOptions="Start">
            <Label Text="List of my friends" />
            <!--
                The height of this stack layout depends on the number of friends,
                so the height will change at runtime and may or may not exceed the collapsed height. 
            -->
            <VerticalStackLayout BindableLayout.ItemsSource="{Binding Friends}"
                                 BindableLayout.ItemTemplate="{StaticResource FriendTemplate}" />
        </VerticalStackLayout>

    </nalu:ExpanderViewBox>

</VerticalStackLayout>
```


