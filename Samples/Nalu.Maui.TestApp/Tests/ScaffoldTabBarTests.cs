using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Page models of the tab bar harness: minimal — a push to a shared detail page and a pop.
/// </summary>
public abstract class TabPageModelBase(INavigationService navigationService) : ObservableObject
{
    public Task PushDetail() => navigationService.GoToAsync(Navigation.Relative().Push<TabDetailPageModel>());

    public Task PushAutoDetail() => navigationService.GoToAsync(Navigation.Relative().Push<TabAutoDetailPageModel>());
}

[UsedImplicitly]
public class TabAlphaPageModel(INavigationService navigationService) : TabPageModelBase(navigationService);

[UsedImplicitly]
public class TabBravoPageModel(INavigationService navigationService) : TabPageModelBase(navigationService);

[UsedImplicitly]
public class TabCharliePageModel(INavigationService navigationService) : TabPageModelBase(navigationService);

[UsedImplicitly]
public class TabDeltaPageModel(INavigationService navigationService) : TabPageModelBase(navigationService);

[UsedImplicitly]
public class TabEchoPageModel(INavigationService navigationService) : TabPageModelBase(navigationService);

[UsedImplicitly]
public class TabFoxtrotPageModel(INavigationService navigationService) : TabPageModelBase(navigationService);

[UsedImplicitly]
public partial class TabDetailPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class TabAutoDetailPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

/// <summary>
/// Tab root page: name marker, a state-preservation entry, push/toggle buttons and a
/// bottom probe label (must end up above the tab bar thanks to the §5.4 inset contribution —
/// scrolling to the end shows it fully, uncovered).
/// </summary>
public abstract class TabPageBase : ContentPage
{
    protected TabPageBase(string name, TabPageModelBase model)
    {
        BindingContext = model;
        Title = name;

        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };

        stack.Add(new Label { Text = $"Tab {name}", AutomationId = $"TabPage{name}", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(new Entry { AutomationId = $"{name}StateEntry", Placeholder = "state", FontSize = 11 });
        stack.Add(NavPageFactory.MakeButton("Push detail", $"PushTabDetail{name}", model.PushDetail));
        stack.Add(NavPageFactory.MakeButton("Push auto detail", $"PushAutoDetail{name}", model.PushAutoDetail));

        var toggleThemeButton = new Button { Text = "Toggle app theme", AutomationId = $"ToggleTheme{name}", FontSize = 11 };
        toggleThemeButton.Clicked += (_, _) =>
        {
            // The app-scope path (NOT a system theme change): flips UserAppTheme like an
            // in-app theme switcher would — AppThemeBindings re-resolve live, no activity
            // recreation.
            var application = Application.Current!;
            application.UserAppTheme = (application.UserAppTheme == AppTheme.Unspecified ? application.RequestedTheme : application.UserAppTheme) == AppTheme.Dark
                ? AppTheme.Light
                : AppTheme.Dark;
        };
        stack.Add(toggleThemeButton);

        var resetThemeButton = new Button { Text = "Reset app theme", AutomationId = $"ResetTheme{name}", FontSize = 11 };
        resetThemeButton.Clicked += (_, _) => Application.Current!.UserAppTheme = AppTheme.Unspecified;
        stack.Add(resetThemeButton);


        var toggleTabBarButton = new Button { Text = "Toggle tab bar", AutomationId = $"ToggleTabBar{name}", FontSize = 11 };
        toggleTabBarButton.Clicked += (_, _) => Scaffold.SetTabBarVisibility(
            this,
            Scaffold.GetTabBarVisibility(this) == ScaffoldTabBarVisibility.Hidden
                ? ScaffoldTabBarVisibility.Visible
                : ScaffoldTabBarVisibility.Hidden
        );
        stack.Add(toggleTabBarButton);

        var badgeButton = new Button { Text = "Set Alpha badge", AutomationId = $"SetBadge{name}", FontSize = 11 };
        badgeButton.Clicked += (_, _) =>
        {
            // Hosted pages are logical children of the Scaffold itself — resolve the tab bar
            // through it.
            Element? element = this;

            while (element is not null and not Scaffold)
            {
                element = element.Parent;
            }

            if (element is Scaffold scaffold && scaffold.Areas.OfType<ScaffoldTabBar>().FirstOrDefault() is { Roots.Count: > 0 } tabBar)
            {
                ScaffoldTabBarView.SetBadgeText(tabBar.Roots[0], "9");
            }
        };
        stack.Add(badgeButton);

        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{name}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        for (var i = 0; i < 30; i++)
        {
            stack.Add(new Label { Text = $"{name} filler {i}", FontSize = 11 });
        }

        stack.Add(new Label { Text = $"{name} bottom probe", AutomationId = $"BottomProbe{name}", FontSize = 14, FontAttributes = FontAttributes.Bold });

        // .NET 10 edge-to-edge model: default SafeAreaEdges — iOS applies the (chrome-augmented)
        // safe area natively via UIScrollViewContentInsetAdjustmentBehavior.Automatic.
        Content = new ScrollView
        {
            AutomationId = $"{name}Scroll",
            Content = stack
        };
    }
}

[UsedImplicitly]
public class TabAlphaPage(TabAlphaPageModel model) : TabPageBase("Alpha", model);

[UsedImplicitly]
public class TabBravoPage(TabBravoPageModel model) : TabPageBase("Bravo", model);

[UsedImplicitly]
public class TabCharliePage(TabCharliePageModel model) : TabPageBase("Charlie", model);

[UsedImplicitly]
public class TabDeltaPage(TabDeltaPageModel model) : TabPageBase("Delta", model);

[UsedImplicitly]
public class TabEchoPage(TabEchoPageModel model) : TabPageBase("Echo", model);

[UsedImplicitly]
public class TabFoxtrotPage(TabFoxtrotPageModel model) : TabPageBase("Foxtrot", model);

[UsedImplicitly]
public class TabDetailPage : ContentPage
{
    public TabDetailPage(TabDetailPageModel model)
    {
        BindingContext = model;
        Title = "TabDetail";

        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };
        stack.Add(new Label { Text = "Tab Detail", AutomationId = "TabDetailPage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Pop", "PopTabDetail", model.Pop));

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitTabDetail", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = new ScrollView { Content = stack };
    }
}

/// <summary>
/// Pushed page with <see cref="ScaffoldTabBarVisibility.Auto"/>: the tab bar hides (animated,
/// in sync with the push) while this page is on top and shows again when it pops.
/// </summary>
[UsedImplicitly]
public class TabAutoDetailPage : ContentPage
{
    public TabAutoDetailPage(TabAutoDetailPageModel model)
    {
        BindingContext = model;
        Title = "TabAutoDetail";

        Scaffold.SetTabBarVisibility(this, ScaffoldTabBarVisibility.Auto);

        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };
        stack.Add(new Label { Text = "Tab Auto Detail", AutomationId = "TabAutoDetailPage", FontSize = 22, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Pop", "PopTabAutoDetail", model.Pop));

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitTabAutoDetail", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = new ScrollView { Content = stack };
    }
}

/// <summary>
/// Scaffold harness exercising the default tab bar template (§5.3): six roots — more than fit a
/// phone width at the default 76dp ItemWidth, so the trailing "More" item and the overflow panel
/// engage — icons from the metadata quintet (untinted FontImageSource, including a Selected
/// variant), badges on an in-bar and an overflow root, and per-page bar visibility toggling.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold TabBar Tests")]
public class TabBarScaffold : Scaffold
{
    private static readonly Color _iconColor = Color.FromArgb("#8A8A8E");
    private static readonly Color _selectedIconColor = Color.FromArgb("#4A7DD1");

    /// <summary>Nav bar surface per theme — distinctive enough for a pixel assertion.</summary>
    internal static readonly Color _lightBarColor = Color.FromArgb("#E8F0FE");
    internal static readonly Color _darkBarColor = Color.FromArgb("#101827");

    public TabBarScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        // Replicates the DailyHelper/template styling that exposed the UserAppTheme bug:
        // ONE shared brush instance from an APP-LEVEL implicit style (where real apps put
        // theming), its Color carrying an AppThemeBinding. Added lazily on first use; the
        // implicit key guards re-entry when the page is opened again.
        var appResources = Application.Current!.Resources;
        if (!appResources.ContainsKey(typeof(ScaffoldTabBarItemView).FullName!))
        {
            var pillBrush = new SolidColorBrush();
            pillBrush.SetAppThemeColor(SolidColorBrush.ColorProperty, Color.FromArgb("#E4EBFD"), Color.FromArgb("#223050"));
            appResources.Add(new Style(typeof(ScaffoldTabBarItemView))
            {
                Setters = { new Setter { Property = ScaffoldTabBarItemView.SelectionPillBackgroundProperty, Value = pillBrush } }
            });
        }

        // The NAV bar's theming, in the shape every real app uses: an implicit style, on a
        // scaffold that DERIVES from Scaffold, with the opt-in that makes MAUI apply it at all.
        // Scaffold-scoped rather than app-level on purpose — several suites sample chrome pixels
        // and an app-wide repaint would move their ground truth.
        var barBrush = new SolidColorBrush();
        barBrush.SetAppThemeColor(SolidColorBrush.ColorProperty, _lightBarColor, _darkBarColor);

        Resources = new ResourceDictionary
                    {
                        new Style(typeof(Scaffold))
                        {
                            ApplyToDerivedTypes = true,
                            Setters = { new Setter { Property = NavBarBackgroundProperty, Value = barBrush } }
                        }
                    };

        var alpha = MakeRoot<TabAlphaPage>("Alpha", "\ue88a"); // home
        var bravo = MakeRoot<TabBravoPage>("Bravo", "\ue8b6"); // search
        var charlie = MakeRoot<TabCharliePage>("Charlie", "\ue7fd"); // person
        var delta = MakeRoot<TabDeltaPage>("Delta", "\ue87d"); // favorite
        var echo = MakeRoot<TabEchoPage>("Echo", "\ue88e"); // info
        var foxtrot = MakeRoot<TabFoxtrotPage>("Foxtrot", "\ue8b8"); // settings

        ScaffoldTabBarView.SetBadgeText(alpha, "11");
        ScaffoldTabBarView.SetBadgeText(foxtrot, "2");

        Areas.Add(
            new ScaffoldTabBar
            {
                Roots = { alpha, bravo, charlie, delta, echo, foxtrot }
            }
        );
    }

    private static ScaffoldRoot MakeRoot<TPage>(string title, string glyph)
        where TPage : Page
        => new()
        {
            Title = title,
            PageType = typeof(TPage),
            Icon = new FontImageSource
            {
                FontFamily = "Material",
                Glyph = glyph,
                Color = _iconColor,
                Size = 24
            },
            SelectedIcon = new FontImageSource
            {
                FontFamily = "Material",
                Glyph = glyph,
                Color = _selectedIconColor,
                Size = 24
            }
        };
}
