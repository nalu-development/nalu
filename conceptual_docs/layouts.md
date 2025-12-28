## Layouts [![Nalu.Maui.Layouts NuGet Package](https://img.shields.io/nuget/v/Nalu.Maui.Layouts.svg)](https://www.nuget.org/packages/Nalu.Maui.Layouts/) [![Nalu.Maui NuGet Package Downloads](https://img.shields.io/nuget/dt/Nalu.Maui.Layouts)](https://www.nuget.org/packages/Nalu.Maui.Layouts/)

Cross-platform layouts for MAUI applications to simplify dealing with templates, `BindingContext` in XAML, and advanced layout scenarios.

### Getting Started

Add layouts support to your application in `MauiProgram.cs`:

```csharp
var builder = MauiApp.CreateBuilder();
builder
    .UseMauiApp<App>()
    .UseNaluLayouts(); // Add this line
```

### Available Components

Nalu.Maui.Layouts provides several powerful components:

| Component | Description |
|-----------|-------------|
| [ViewBox](layouts-viewbox.md#viewbox) | Lightweight replacement for `ContentView` with content binding context support |
| [TemplateBox](layouts-viewbox.md#templatebox) | View holder based on `DataTemplate` or `DataTemplateSelector` |
| [ToggleTemplate](layouts-viewbox.md#toggletemplate) | Conditional template switcher based on boolean value |
| [ExpanderViewBox](layouts-expander.md) | Animated collapsible content container |
| [HorizontalWrapLayout](layouts-wrap.md#horizontalwraplayout) | Flow layout that wraps children left-to-right, top-to-bottom |
| [VerticalWrapLayout](layouts-wrap.md#verticalwraplayout) | Flow layout that wraps children top-to-bottom, left-to-right |
| [Popups](layouts-popup.md) | Flexible modal popup system |
| [Magnet](layouts-magnet.md) | Constraint-based layout engine |

### Quick Examples

#### ViewBox with Content Binding

```xml
<nalu:ViewBox ContentBindingContext="{Binding SelectedAnimal}">
    <views:AnimalView x:DataType="models:Animal" />
</nalu:ViewBox>
```

#### Wrap Layout for Tags

```xml
<nalu:HorizontalWrapLayout HorizontalSpacing="8" VerticalSpacing="8">
    <Label Text="Tag 1" BackgroundColor="LightBlue" Padding="8,4" />
    <Label Text="Tag 2" BackgroundColor="LightGreen" Padding="8,4" />
    <Label Text="Tag 3" BackgroundColor="LightCoral" Padding="8,4" />
</nalu:HorizontalWrapLayout>
```

#### Animated Expander

```xml
<nalu:ExpanderViewBox CollapsedHeight="100" IsExpanded="{Binding IsExpanded}">
    <Label Text="{Binding LongDescription}" />
    </nalu:ExpanderViewBox>
```

## Learn More

- 📘 [ViewBox, TemplateBox & ToggleTemplate](layouts-viewbox.md) - Template containers and content binding
- 📘 [ExpanderViewBox](layouts-expander.md) - Animated collapsible content
- 📘 [Wrap Layouts](layouts-wrap.md) - Horizontal and vertical flow layouts with expand modes
- 📘 [Popups](layouts-popup.md) - Modal popup system
- 📘 [Magnet](layouts-magnet.md) - Constraint-based layout engine
