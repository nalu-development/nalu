# Custom Tab Bar

> **Note**: The custom tab bar feature is **independent** of Nalu's MVVM navigation system. It can be used with **standard MAUI Shell** as well as `NaluShell`.

Nalu provides a custom tab bar implementation that allows you to replace the native tab bar with a fully customizable cross-platform view.
This is especially useful when you need more than 5 tabs (avoiding the native "More" tab on iOS) or want complete control over tab bar styling.

This feature also solves the issues `Shell` has with pages under the iOS `More` tab.

![Custom Tab Bar](assets/images/nalu-tab-bar.png)

**Platform support**: Android, iOS, and MacCatalyst only.
**Edge-to-edge**: The custom tab bar allows edge-to-edge behavior when supported (iOS: always, Android: .NET10 + API 35 device).

## Quick Start

### Setup

1. Enable the custom tab bar handler in `MauiProgram.cs`:

```csharp
builder
    .UseMauiApp<App>()
    // Optional: Only if using Nalu navigation
    .UseNaluNavigation<App>(nav => nav.AddPages())
#if IOS || ANDROID || MACCATALYST
    .UseNaluTabBar()  // Works with both standard Shell and NaluShell
#endif
```

2. Attach the custom tab bar view to your `TabBar` or `FlyoutItem`:

**With standard Shell:**
```xml
<Shell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
       xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
       xmlns:nalu="https://nalu-development.github.com/nalu/navigation"
       x:Class="MyApp.AppShell">
    <TabBar nalu:NaluShell.TabBarView="{nalu:NaluTabBar}">
        <ShellContent Title="Home"
                      Icon="{FontImageSource FontFamily='MaterialOutlined', Glyph='&#xe88a;', Size=24}" />
        <ShellContent Title="Search"
                      Icon="{FontImageSource FontFamily='MaterialOutlined', Glyph='&#xe8b6;', Size=24}" />
        <!-- Add as many tabs as needed - no "More" tab limitation! -->
    </TabBar>
</Shell>
```

**With NaluShell:**
```xml
<nalu:NaluShell xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                 xmlns:nalu="https://nalu-development.github.com/nalu/navigation"
                 xmlns:pages="clr-namespace:MyApp.Pages"
                 x:Class="MyApp.AppShell">
    <FlyoutItem nalu:NaluShell.TabBarView="{nalu:NaluTabBar}">
        <ShellContent nalu:Navigation.PageType="pages:HomePage"
                      Title="Home"
                      Icon="{FontImageSource FontFamily='MaterialOutlined', Glyph='&#xe88a;', Size=24}" />
        <!-- More ShellContent items -->
    </FlyoutItem>
</nalu:NaluShell>
```

## Built-in `NaluTabBar` Control

Nalu provides a customizable `NaluTabBar` control with extensive styling options. To customize it, define it as a resource:

```xml
<Application.Resources>
    <ResourceDictionary>
        <nalu:NaluTabBar x:Key="CustomTabBar"
                         BarBackground="White"
                         BarPadding="8,0"
                         TabForegroundColor="Gray"
                         TabBackground="Transparent"
                         TabStrokeShape="{RoundRectangle CornerRadius=8}"
                         ActiveTabBackground="Blue"
                         ActiveTabForegroundColor="White"
                         ActiveTabStrokeShape="{RoundRectangle CornerRadius=8}" />
    </ResourceDictionary>
</Application.Resources>
```

Then reference it in your Shell:

```xml
<TabBar nalu:NaluShell.TabBarView="{StaticResource CustomTabBar}">
    <!-- Your ShellContent items -->
</TabBar>
```

### Styling Properties

The `NaluTabBar` control provides extensive styling options:

**Bar Properties** (container):
- `BarBackground` - Background brush for the tab bar container
- `BarPadding` - Padding around the tab bar container
- `BarMargin` - Margin around the tab bar container
- `BarStroke` - Stroke brush for the container border
- `BarStrokeThickness` - Thickness of the container border
- `BarStrokeShape` - Shape of the container border

**Tab Properties** (individual tabs):
- `TabBackground` - Background brush for inactive tabs
- `TabForegroundColor` - Text color for inactive tabs
- `TabStroke` - Stroke brush for inactive tab borders
- `TabStrokeThickness` - Thickness of inactive tab borders
- `TabStrokeShape` - Shape of inactive tab borders

**Active Tab Properties**:
- `ActiveTabBackground` - Background brush for the active tab
- `ActiveTabForegroundColor` - Text color for the active tab
- `ActiveTabStroke` - Stroke brush for the active tab border
- `ActiveTabStrokeThickness` - Thickness of the active tab border
- `ActiveTabStrokeShape` - Shape of the active tab border

## Custom Tab Bar View

You can also create your own custom tab bar view. The view will be bound to the `ShellItem` (TabBar or FlyoutItem), allowing you to iterate through `Items` and handle tab selection:

```xml
<TabBar nalu:NaluShell.TabBarView="{StaticResource MyCustomTabBar}">
    <!-- Your ShellContent items -->
</TabBar>
```

When creating a custom tab bar view:

1. **Binding Context**: The view will be bound to the `ShellItem` (TabBar or FlyoutItem)
2. **Accessing Items**: Use `Items` property to iterate through `ShellSection` items
3. **Tab Selection**: Handle tap gestures and navigate using `Shell.Current.GoToAsync($"//{shellSection.CurrentItem.Route}")`
4. **Active State**: Check `IsChecked` property on `ShellSection` to determine active tab

Example custom tab bar implementation:

```xml
<Grid xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
      x:DataType="ShellItem">
    <HorizontalStackLayout BindableLayout.ItemsSource="{Binding Items}">
        <BindableLayout.ItemTemplate>
            <DataTemplate x:DataType="ShellSection">
                <Border Background="{Binding IsChecked, Converter={StaticResource BoolToColorConverter}}"
                        Padding="12,8">
                    <Border.GestureRecognizers>
                        <TapGestureRecognizer Tapped="OnTabTapped" />
                    </Border.GestureRecognizers>
                    <VerticalStackLayout>
                        <Image Source="{Binding Icon}" />
                        <Label Text="{Binding Title}" />
                    </VerticalStackLayout>
                </Border>
            </DataTemplate>
        </BindableLayout.ItemTemplate>
    </HorizontalStackLayout>
</Grid>
```

## Benefits

- ✅ **No "More" tab limitation** - Supports unlimited tabs with horizontal scrolling
- ✅ **Fully customizable styling** - Colors, shapes, spacing, and more
- ✅ **Cross-platform consistent appearance** - Same look on all platforms
- ✅ **Works with both standard Shell and NaluShell** - Flexible integration
- ✅ **Independent feature** - Can be used without Nalu navigation

## Platform Considerations

### iOS

On iOS, the custom tab bar prevents the native "More" navigation controller from appearing when you have more than 5 tabs. All tabs are accessible through the custom tab bar UI, while the underlying `UITabBarController` manages up to 5 view controllers at a time, with automatic swapping when tabs beyond the visible set are selected.

### Android

On Android, the custom tab bar replaces the native `BottomNavigationView` menu items with your custom view, providing full control over appearance and behavior.

### MacCatalyst

MacCatalyst uses the same iOS implementation, providing consistent behavior across Apple platforms.

## See Also

- [Navigation Overview](navigation.md) - Learn about Nalu's MVVM navigation system
- [Navigation Lifecycle](navigation-lifecycle.md) - Understanding page lifecycle events
- [Navigation Intents](navigation-intents.md) - Passing data between pages

