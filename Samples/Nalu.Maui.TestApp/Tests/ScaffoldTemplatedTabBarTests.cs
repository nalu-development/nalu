using CommunityToolkit.Maui.Layouts;
using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class TplTabHomePageModel(INavigationService navigationService) : ObservableObject
{
    public Task GoOther() => navigationService.GoToAsync(Navigation.Absolute().Root<TplTabOtherPageModel>());
}

[UsedImplicitly]
public partial class TplTabOtherPageModel(INavigationService navigationService) : ObservableObject
{
    public Task GoHome() => navigationService.GoToAsync(Navigation.Absolute().Root<TplTabHomePageModel>());
}

/// <summary>
/// Root WITHOUT a nav bar. Switching between this root and <see cref="TplTabOtherPage"/> makes the
/// nav bar present and dismiss, which is the animation that lays the tab bar strip out from inside
/// an animation block — see <see cref="TemplatedTabBarScaffold"/>.
/// </summary>
[UsedImplicitly]
public class TplTabHomePage : ContentPage
{
    public TplTabHomePage(TplTabHomePageModel model)
    {
        BindingContext = model;
        Title = "Tpl Home";
        Scaffold.SetIsNavBarVisible(this, false);

        var stack = new VerticalStackLayout { Spacing = 8, Padding = 16 };
        stack.Add(new Label { Text = "TplTabHomePage", AutomationId = "TplTabHomePage", FontSize = 20 });
        stack.Add(NavPageFactory.MakeButton("Go other root", "TplGoOtherRoot", model.GoOther));
        stack.Add(TplTabBarPageParts.MakeExitButton("ExitTplTabHome"));

        Content = new Grid { Children = { stack } };
    }
}

/// <summary>Root WITH a nav bar: the switch toggles nav bar presentation in both directions.</summary>
[UsedImplicitly]
public class TplTabOtherPage : ContentPage
{
    public TplTabOtherPage(TplTabOtherPageModel model)
    {
        BindingContext = model;
        Title = "Tpl Other";
        Scaffold.SetIsNavBarVisible(this, true);

        var stack = new VerticalStackLayout { Spacing = 8, Padding = 16 };
        stack.Add(new Label { Text = "TplTabOtherPage", AutomationId = "TplTabOtherPage", FontSize = 20 });
        stack.Add(NavPageFactory.MakeButton("Go home root", "TplGoHomeRoot", model.GoHome));
        stack.Add(TplTabBarPageParts.MakeExitButton("ExitTplTabOther"));

        Content = new Grid { Children = { stack } };
    }
}

/// <summary>One templated bar entry: the cell's data.</summary>
public sealed record TplTabEntry(string Title);

internal static class TplTabBarPageParts
{
    /// <summary>
    /// A scaffold harness has no decorated ResetButton (the decorator only reaches ContentPage and
    /// NavigationPage roots), so the wrapper resets these pages through a button reading "Exit".
    /// </summary>
    public static Button MakeExitButton(string automationId)
    {
        var button = new Button
        {
            Text = "Exit",
            AutomationId = automationId,
            FontSize = 11,
            BackgroundColor = Colors.IndianRed
        };

        button.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();

        return button;
    }
}

/// <summary>
/// Harness for a TEMPLATED custom tab bar — the shape real apps write, and the one that wedged the
/// main thread: a non-consuming root (<c>SafeAreaEdges.None</c>) carrying a shadow, hosting a
/// <c>UniformItemsLayout</c> whose children are REALIZED FROM A TEMPLATE via
/// <c>BindableLayout.ItemsSource</c> rather than declared inline.
/// </summary>
/// <remarks>
/// The distinction that matters is when the bar can answer a measure. A bar with inline children
/// measures the same from its first pass; a templated one realizes its cells (image + label rows)
/// as the layout runs, so its height ANSWER CHANGES across the passes the strip drives — which is
/// exactly what the strip's settle-then-remeasure path reacts to. Add the nav bar presentation of a
/// root switch and those passes run inside an animation block, where UIKit drains the dirty views
/// before returning: anything that re-dirties the host from within a layout pass never lets the
/// drain finish.
/// </remarks>
[UsedImplicitly]
[TestPage("Scaffold Templated TabBar Tests")]
public class TemplatedTabBarScaffold : Scaffold
{
    public TemplatedTabBarScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        var items = new UniformItemsLayout { AutomationId = "TplTabItems" };

        BindableLayout.SetItemsSource(
            items,
            new[] { new TplTabEntry("One"), new TplTabEntry("Two"), new TplTabEntry("Three") }
        );

        BindableLayout.SetItemTemplate(
            items,
            new DataTemplate(
                () =>
                {
                    var cell = new Grid
                    {
                        Margin = new Thickness(8, 8, 8, 0),
                        RowDefinitions =
                        {
                            new RowDefinition(GridLength.Auto),
                            new RowDefinition(GridLength.Auto)
                        }
                    };

                    var icon = new BoxView
                    {
                        HeightRequest = 24,
                        WidthRequest = 24,
                        Color = Colors.SlateGray,
                        HorizontalOptions = LayoutOptions.Center
                    };

                    var label = new Label
                    {
                        MaxLines = 1,
                        FontSize = 11,
                        HorizontalOptions = LayoutOptions.Center
                    };

                    label.SetBinding(Label.TextProperty, static (TplTabEntry entry) => entry.Title);

                    cell.Add(icon);
                    cell.Add(label);
                    Grid.SetRow(label, 1);

                    return cell;
                }
            )
        );

        var bar = new Grid
        {
            SafeAreaEdges = SafeAreaEdges.None,
            BackgroundColor = Colors.White,
            AutomationId = "TplTabBarRoot",
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.2f,
                Radius = 8,
                Offset = new Point(0, -2)
            },
            Children = { items }
        };

        Areas.Add(
            new ScaffoldTabBar
            {
                TabBarView = bar,
                Roots =
                {
                    new ScaffoldRoot { Title = "Home", PageType = typeof(TplTabHomePage) },
                    new ScaffoldRoot { Title = "Other", PageType = typeof(TplTabOtherPage) }
                }
            }
        );
    }
}
