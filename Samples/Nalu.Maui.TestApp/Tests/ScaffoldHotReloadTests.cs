using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public class HrAlphaPageModel : ObservableObject;

[UsedImplicitly]
public class HrBravoPageModel : ObservableObject;

[UsedImplicitly]
public class HrCharliePageModel : ObservableObject;

/// <summary>
/// Root page of the hot-reload harness carrying the mutation buttons: each one performs a
/// LIVE structure edit on the presented scaffold, mimicking what XAML hot reload (or runtime
/// code) does to a live instance.
/// </summary>
public abstract class HrPageBase : ContentPage
{
    protected HrPageBase(string name)
    {
        Title = name;

        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };
        stack.Add(new Label { Text = $"HR {name}", AutomationId = $"Hr{name}Page", FontSize = 22, FontAttributes = FontAttributes.Bold });

        stack.Add(MakeButton("Swap tab bar view", "HrSwapTabBarView", () =>
        {
            if (FindTabBar() is { } tabBar)
            {
                var custom = new Grid
                {
                    SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.Container),
                    BackgroundColor = Colors.Purple,
                    AutomationId = "HrCustomBar"
                };

                custom.Add(new Grid { HeightRequest = 56, BackgroundColor = Colors.Orange, VerticalOptions = LayoutOptions.Start });
                tabBar.TabBarView = custom;
            }
        }));

        stack.Add(MakeButton("Add Charlie root", "HrAddCharlie", () => FindTabBar()?.Roots.Add(MakeRoot<HrCharliePage>("HrCharlie"))));

        stack.Add(MakeButton("Remove Bravo root", "HrRemoveBravo", () => RemoveRoot(typeof(HrBravoPage))));

        stack.Add(MakeButton("Remove Alpha root", "HrRemoveAlpha", () => RemoveRoot(typeof(HrAlphaPage))));

        stack.Add(MakeButton("Simulate hot reload", "HrSimulateReload", () =>
        {
            // XAML hot reload re-runs the whole initialization on the LIVE scaffold: the full
            // structure is re-ADDED as fresh instances. Here the "edited" structure gains a
            // third root.
            if (FindScaffold() is { } scaffold)
            {
                scaffold.Areas.Add(
                    new ScaffoldTabBar
                    {
                        Roots =
                        {
                            MakeRoot<HrAlphaPage>("HrAlpha"),
                            MakeRoot<HrBravoPage>("HrBravo"),
                            MakeRoot<HrCharliePage>("HrCharlie")
                        }
                    }
                );
            }
        }));

        var exitButton = new Button { Text = "Exit", AutomationId = "HrExit", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = new ScrollView { Content = stack };
    }

    internal static ScaffoldRoot MakeRoot<TPage>(string title)
        where TPage : Page
        => new()
        {
            Title = title,
            PageType = typeof(TPage)
        };

    private static Button MakeButton(string text, string automationId, Action action)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
        button.Clicked += (_, _) => action();

        return button;
    }

    private Scaffold? FindScaffold()
    {
        Element? element = this;

        while (element is not null and not Scaffold)
        {
            element = element.Parent;
        }

        return element as Scaffold;
    }

    private ScaffoldTabBar? FindTabBar() => FindScaffold()?.CurrentArea as ScaffoldTabBar;

    private void RemoveRoot(Type pageType)
    {
        if (FindTabBar() is { } tabBar && tabBar.Roots.FirstOrDefault(r => r.PageType == pageType) is { } root)
        {
            tabBar.Roots.Remove(root);
        }
    }
}

[UsedImplicitly]
public class HrAlphaPage(HrAlphaPageModel model) : HrPageBase("Alpha")
{
    private readonly HrAlphaPageModel _model = model;
}

[UsedImplicitly]
public class HrBravoPage(HrBravoPageModel model) : HrPageBase("Bravo")
{
    private readonly HrBravoPageModel _model = model;
}

[UsedImplicitly]
public class HrCharliePage(HrCharliePageModel model) : HrPageBase("Charlie")
{
    private readonly HrCharliePageModel _model = model;
}

/// <summary>
/// Scaffold harness for live structure edits (§ hot reload): two tab roots at start; the
/// page buttons swap the tab bar view, add/remove roots, and simulate a full hot-reload
/// re-inflation (the whole structure re-added as fresh instances).
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Hot Reload Tests")]
public class HotReloadScaffold : Scaffold
{
    public HotReloadScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    HrPageBase.MakeRoot<HrAlphaPage>("HrAlpha"),
                    HrPageBase.MakeRoot<HrBravoPage>("HrBravo")
                }
            }
        );
    }
}
