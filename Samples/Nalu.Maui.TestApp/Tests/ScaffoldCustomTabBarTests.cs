using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public class CustomTabBarHomePageModel : ObservableObject;

[UsedImplicitly]
public class CustomTabBarHomePage : ContentPage
{
    public CustomTabBarHomePage(CustomTabBarHomePageModel model)
    {
        BindingContext = model;
        Title = "Custom TabBar Home";

        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };
        stack.Add(new Label { Text = "Custom TabBar Home", AutomationId = "CustomTabBarHomePage", FontSize = 22, FontAttributes = FontAttributes.Bold });

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitCustomTabBar", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }
}

/// <summary>
/// Scaffold harness for a fully CUSTOM <see cref="ScaffoldTabBar.TabBarViewProperty"/>: a
/// container consuming the bottom safe area (painting it) with an 80dp content band above it —
/// the bar spans content + inset and reaches the very bottom of the screen. Mirrors the shape
/// a user would write in XAML:
/// <code>
/// &lt;nalu:ScaffoldTabBar.TabBarView&gt;
///     &lt;Grid SafeAreaEdges="None,None,None,Container" BackgroundColor="Blue"&gt;
///         &lt;Grid HeightRequest="80" BackgroundColor="Red" VerticalOptions="Start" /&gt;
///     &lt;/Grid&gt;
/// &lt;/nalu:ScaffoldTabBar.TabBarView&gt;
/// </code>
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Custom TabBar Label Tests")]
public class CustomTabBarLabelScaffold : Scaffold
{
    public CustomTabBarLabelScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        // Natural-size content, no explicit heights: the bar's measured height is the label's,
        // the strip extends it by the system inset, and the label must NOT inflate.
        var container = new Grid
        {
            SafeAreaEdges = SafeAreaEdges.None,
            AutomationId = "LabelTabBarOuter"
        };

        var inner = new Grid
        {
            BackgroundColor = Colors.Red,
            AutomationId = "LabelTabBarInner"
        };

        inner.Add(new Label { Text = "Hello", BackgroundColor = Colors.Blue, AutomationId = "LabelTabBarLabel" });
        container.Add(inner);

        Areas.Add(
            new ScaffoldTabBar
            {
                TabBarView = container,
                Roots =
                {
                    new ScaffoldRoot
                    {
                        Title = "Home",
                        PageType = typeof(CustomTabBarHomePage)
                    }
                }
            }
        );
    }
}

[UsedImplicitly]
[TestPage("Scaffold Custom TabBar Tests")]
public class CustomTabBarScaffold : Scaffold
{
    public CustomTabBarScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        // Bottom Container: the bar owns the inset — the blue container pads itself by the
        // system inset (painting it) and the 80dp red band sits fully above it.
        var container = new Grid
        {
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.Container),
            BackgroundColor = Colors.Blue,
            AutomationId = "CustomTabBarContainer"
        };

        container.Add(
            new Grid
            {
                HeightRequest = 80,
                BackgroundColor = Colors.Red,
                VerticalOptions = LayoutOptions.Start,
                AutomationId = "CustomTabBarContent"
            }
        );

        Areas.Add(
            new ScaffoldTabBar
            {
                TabBarView = container,
                Roots =
                {
                    new ScaffoldRoot
                    {
                        Title = "Home",
                        PageType = typeof(CustomTabBarHomePage)
                    }
                }
            }
        );
    }
}
