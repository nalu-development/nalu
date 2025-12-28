## Popups

Nalu.Maui provides a flexible and customizable way to implement popups in your MAUI applications.
Popups are non other than `ContentPage`s with transparent background and a few key properties.

Nalu offers a built-in `PopupPageBase` class, which creates a modal page with a scrim (background overlay) and a styled container for your popup content.

A `CloseOnScrimTapped` property is available to control whether the popup should close when the scrim is tapped: you can bind it to a boolean property in your view model or set it directly in XAML.

### Styling the Popups

The base style for popups must be defined in your `Styles.xaml` file, allowing you to customize the appearance of all popups in your application.

```xml
<Style TargetType="nalu:PopupScrim">
    <Setter Property="BackgroundColor" Value="{AppThemeBinding Light='#20000000', Dark='#20FFFFFF'}" />
</Style>

<Style TargetType="nalu:PopupContainer">
    <Setter Property="Background" Value="{AppThemeBinding Light={StaticResource White}, Dark={StaticResource OffBlack}}" />
    <Setter Property="Margin" Value="16" />
    <Setter Property="StrokeShape" Value="RoundRectangle 24" />
    <Setter Property="StrokeThickness" Value="0" />
    <Setter Property="VerticalOptions" Value="Center" />
    <Setter Property="HorizontalOptions" Value="Center" />
</Style>
```

### Creating a Popup

To create a popup, inherit from `PopupPageBase` and set your content via the `PopupContent` property (in XAML or code). You can style the popup using the provided `PopupContainer` and `PopupScrim` types.

```xml
<?xml version="1.0" encoding="utf-8"?>

<nlayouts:PopupPageBase xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                        xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                        xmlns:popupModels="clr-namespace:MyNamespace.PopupModels"
                        xmlns:nlayouts="https://nalu-development.github.com/nalu/layouts"
                        x:Class="MyNamespace.YesNoPopupPage"
                        x:DataType="popupModels:YesNoPopupPageModel"
                        CloseOnScrimTapped="True">
    <VerticalStackLayout Padding="16">
        <Label Text="Do you want to proceed?"
               FontSize="24"
               HorizontalOptions="Center" />
        <HorizontalStackLayout HorizontalOptions="End">
            <Button Text="Cancel"
                    Command="{Binding Path=NoCommand}"
                    Margin="0,0,16,0" />
            <Button Text="OK"
                    Command="{Binding Path=YesCommand}" />
        </HorizontalStackLayout>
    </VerticalStackLayout>
</nlayouts:PopupPageBase>
```

### Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `CloseOnScrimTapped` | `bool` | `false` | Whether tapping the scrim closes the popup |

### Using Popups with Nalu.Maui.Navigation

First of all the popup page and page model must follow the same naming convention as other pages and page models in your application.
If you want to use a different one, you have to register them manually in the Nalu navigation builder.

#### Define a Base Class for Popup Models

In the following example we use the community toolkit MVVM `ObservableObject` as base class, but you can use any other base class that fits your needs.

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Nalu.Maui.Weather.PopupModels;

public abstract class PopupModelBase<TIntent, TResult>(INavigationService navigationService) : ObservableObject, IEnteringAware<TIntent>
    where TIntent : AwaitableIntent<TResult>
{
    protected TIntent PopupIntent { get; private set; } = null!;

    public virtual ValueTask OnEnteringAsync(TIntent intent)
    {
        PopupIntent = intent;
        return ValueTask.CompletedTask;
    }

    protected async Task CloseAsync()
    {
        await navigationService.GoToAsync(Navigation.Relative().Pop());
    }

    protected Task CloseAsync(TResult result)
    {
        PopupIntent.SetResult(result);
        return CloseAsync();
    }

    protected Task CloseFaultyAsync(Exception exception)
    {
        PopupIntent.SetException(exception);
        return CloseAsync();
    }
}
```

#### Define an Awaitable Intent

Now define our awaitable intent for the popup. This intent will be used to pass parameters to the popup and to await the result.

In the following example we don't have any specific parameters to pass, but you can extend the intent class to include any necessary properties.

```csharp
public class YesNoIntent : AwaitableIntent<bool?>;
```

#### Implement the Popup Model

Use the intent in the corresponding popup model:

```csharp
public partial class YesNoPopupModel(INavigationService navigationService) : PopupModelBase<YesNoIntent, bool?>(navigationService)
{
    [RelayCommand]
    public async Task YesAsync() => await CloseAsync(true);

    [RelayCommand]
    public async Task NoAsync() => await CloseAsync(false);
}
```

#### Show the Popup and Await Result

Now we can leverage the Nalu's navigation service to show the popup and await the result.

```csharp
var response = await navigationService.ResolveIntentAsync<YesNoPopupModel, bool?>(new YesNoIntent());
```

If you just want to await for the popup to close without caring about the result, you can use the non-generic `AwaitableIntent` instead.

### Use Cases

- **Confirmation dialogs** - Ask users to confirm destructive actions
- **Input forms** - Collect simple user input
- **Alerts** - Display important messages
- **Selection pickers** - Let users choose from options
- **Loading indicators** - Show progress during operations

