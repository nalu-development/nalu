## ViewBox, TemplateBox & ToggleTemplate

These components provide lightweight alternatives to `ContentView` and powerful template-based content management.

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

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Content` | `IView` | The content view |
| `ContentBindingContext` | `object` | The binding context for the content |
| `IsClippedToBounds` | `bool` | Whether to clip the content to bounds |

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

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `ContentTemplate` | `DataTemplate` | The template to use for content |
| `ContentBindingContext` | `object` | The binding context for the content |

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

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Value` | `bool` | The boolean value that controls which template is shown |
| `WhenTrue` | `DataTemplate` | The template to display when `Value` is `true` |
| `WhenFalse` | `DataTemplate` | The template to display when `Value` is `false` |

### DataTemplatesSource (Virtualized ScrollView)

You can use a `CollectionView` with our special `ItemsSource` and `ItemTemplate` to achieve a "virtualized `ScrollView`".

```xml
<CollectionView ItemTemplate="{nalu:TemplateSourceSelector}">
    <CollectionView.ItemsSource>
        <nalu:DataTemplatesSource x:Key="VirtualizedViews">
            <!-- first virtualized view -->
            <DataTemplate x:DataType="pageModels:MyPageModel">
                <Label Text="{Binding MyPageModelPropertyHere}"/>
            </DataTemplate>

            <!-- second virtualized view -->
            <DataTemplate x:DataType="pageModels:MyPageModel">
                <Label Text="{Binding MyOtherPropertyHere}"/>
            </DataTemplate>

            <!-- ... -->
        </nalu:DataTemplatesSource>
    </CollectionView.ItemsSource>
</CollectionView>
```

