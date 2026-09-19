using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class NavBarHomePageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushDetail() => navigationService.GoToAsync(Navigation.Relative().Push<NavBarDetailPageModel>());

    public Task PushDrawerDetail() => navigationService.GoToAsync(Navigation.Relative().Push<NavBarDrawerDetailPageModel>());

    public Task PushCustomBar() => navigationService.GoToAsync(Navigation.Relative().Push<NavBarCustomPageModel>());

    public Task PushEdgeToEdgeBar() => navigationService.GoToAsync(Navigation.Relative().Push<NavBarEdgeToEdgePageModel>());

    public Task PushToolbar() => navigationService.GoToAsync(Navigation.Relative().Push<NavBarToolbarPageModel>());

    public Task PushFold() => navigationService.GoToAsync(Navigation.Relative().Push<NavBarFoldPageModel>());
}

[UsedImplicitly]
public partial class NavBarFoldPageModel(INavigationService navigationService) : ObservableObject
{
    /// <summary>The last activated toolbar item, as the UI tests read it.</summary>
    [ObservableProperty]
    public partial string LastActivated { get; set; } = "-";

    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class NavBarToolbarPageModel(INavigationService navigationService) : ObservableObject
{
    private Command? _saveCommand;

    /// <summary>The last activated toolbar item, as the UI tests read it.</summary>
    [ObservableProperty]
    public partial string LastActivated { get; set; } = "-";

    /// <summary>Bound from a toolbar item: proves the page model reaches the items (binding-context propagation).</summary>
    public ICommand SaveCommand => _saveCommand ??= new Command(() => LastActivated = "save");

    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class NavBarDetailPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class NavBarDrawerDetailPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class NavBarCustomPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class NavBarEdgeToEdgePageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}



file static class NavBarPageFactory
{
    public static View BuildContent(string marker, VisualElement? probeAnchor, params View[] extraViews)
    {
        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };

        stack.Add(new Label { Text = marker, AutomationId = marker, FontSize = 22, FontAttributes = FontAttributes.Bold });

        // Platform ground truth for the inset assertions (see SafeAreaProbe). ONLY the harness
        // root page carries it: its AutomationIds are reserved, and scaffold-hosted pages stay in
        // the element tree once visited, so a probe per page would duplicate them.
        if (probeAnchor is not null)
        {
            stack.Add(SafeAreaProbe.CreateProbe(probeAnchor));
        }

        foreach (var view in extraViews)
        {
            stack.Add(view);
        }

        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{marker}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        var scroll = new ScrollView { Content = stack };

        if (probeAnchor is null)
        {
            return scroll;
        }

        // Bottom-anchored marker: where the page's content actually ends is the platform-agnostic
        // witness that the page received the system bottom inset (no reading of anyone's
        // SafeAreaEdges interpretation involved).
        var grid = new Grid();
        grid.Add(scroll);

        grid.Add(
            new BoxView
            {
                Color = Colors.MediumPurple,
                HeightRequest = 4,
                VerticalOptions = LayoutOptions.End,
                AutomationId = "NavBarBottomProbe"
            }
        );

        return grid;
    }
}

/// <summary>Root page: title, pushes, nav bar visibility toggle.</summary>
[UsedImplicitly]
public class NavBarHomePage : ContentPage
{
    public NavBarHomePage(NavBarHomePageModel model)
    {
        BindingContext = model;
        Title = "Home Title";

        var toggleButton = new Button { Text = "Toggle nav bar", AutomationId = "ToggleNavBar", FontSize = 11 };
        toggleButton.Clicked += (_, _) => Scaffold.SetIsNavBarVisible(this, !Scaffold.GetIsNavBarVisible(this));

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageHome",
            probeAnchor: this,
            NavPageFactory.MakeButton("Push detail", "PushNavBarDetail", model.PushDetail),
            NavPageFactory.MakeButton("Push drawer detail", "PushNavBarDrawerDetail", model.PushDrawerDetail),
            NavPageFactory.MakeButton("Push custom bar", "PushNavBarCustom", model.PushCustomBar),
            NavPageFactory.MakeButton("Push edge-to-edge bar", "PushNavBarEdgeToEdge", model.PushEdgeToEdgeBar),
            NavPageFactory.MakeButton("Push toolbar page", "PushNavBarToolbar", model.PushToolbar),
            NavPageFactory.MakeButton("Push fold page", "PushNavBarFold", model.PushFold),
            toggleButton
        );
    }
}

/// <summary>
/// Pushed page carrying standard MAUI <see cref="Page.ToolbarItems"/>: three primary items
/// (text, font icon, disabled text) declared OUT of priority order, two secondary ones, plus
/// runtime mutations (add, move to the overflow, toggle enabled). Activations land in the
/// model's <see cref="NavBarToolbarPageModel.LastActivated"/>.
/// </summary>
[UsedImplicitly]
public class NavBarToolbarPage : ContentPage
{
    public NavBarToolbarPage(NavBarToolbarPageModel model)
    {
        BindingContext = model;
        Title = "Toolbar Title";

        // A page-level foreground: the text item and the UNCOLORED font icon must follow it.
        Scaffold.SetNavBarForeground(this, Color.FromArgb("#2C479D"));

        // "Items win" here, on purpose: this page is about the ITEMS (rendering, activation,
        // live changes) and wants them all in the bar, so the title's reservation is dropped —
        // the title truncates, as native bars do. The fold contract has its own page.
        Scaffold.SetNavBarTemplate(this, new DataTemplate(static () => new ScaffoldNavBarView { KeepTitleWhole = false, MinimumTitleWidth = 0 }));

        var save = new ToolbarItem { Text = "Save", Priority = 0, AutomationId = "ToolbarSave" };
        save.SetBinding(MenuItem.CommandProperty, static (NavBarToolbarPageModel m) => m.SaveCommand);

        var share = new ToolbarItem
        {
            Text = "Share",
            Priority = 1,
            AutomationId = "ToolbarShare",
            IconImageSource = new FontImageSource { FontFamily = "Material", Glyph = "", Size = 24 }
        };

        share.Clicked += (_, _) => model.LastActivated = "share";

        var locked = new ToolbarItem { Text = "Locked", Priority = 2, AutomationId = "ToolbarLocked", IsEnabled = false };
        locked.Clicked += (_, _) => model.LastActivated = "locked";

        var about = new ToolbarItem
        {
            Text = "About",
            Order = ToolbarItemOrder.Secondary,
            Priority = 0,
            AutomationId = "ToolbarAbout",
            IconImageSource = new FontImageSource { FontFamily = "Material", Glyph = "", Color = Colors.DarkOrange, Size = 24 }
        };

        about.Clicked += (_, _) => model.LastActivated = "about";

        var settings = new ToolbarItem { Text = "Settings", Order = ToolbarItemOrder.Secondary, Priority = 1, AutomationId = "ToolbarSettings" };
        settings.Clicked += (_, _) => model.LastActivated = "settings";

        // Declared out of priority order on purpose: the bar sorts.
        ToolbarItems.Add(share);
        ToolbarItems.Add(settings);
        ToolbarItems.Add(save);
        ToolbarItems.Add(about);
        ToolbarItems.Add(locked);

        var log = new Label { AutomationId = "ToolbarActivationLog", FontSize = 14 };
        // A string path: the MAUI binding source generator cannot see a source-generated partial property.
        log.SetBinding(Label.TextProperty, nameof(NavBarToolbarPageModel.LastActivated));

        var addButton = new Button { Text = "Add item", AutomationId = "AddToolbarItem", FontSize = 11 };
        var extras = 0;

        addButton.Clicked += (_, _) =>
        {
            // Numbered: every extra is addressable, and a pile of them exercises the fold.
            var n = ++extras;
            var extra = new ToolbarItem { Text = $"Extra {n}", Priority = 3, AutomationId = $"ToolbarExtra{n}" };
            extra.Clicked += (_, _) => model.LastActivated = $"extra{n}";
            ToolbarItems.Add(extra);
        };

        var moveButton = new Button { Text = "Move Share to overflow", AutomationId = "MoveShareToOverflow", FontSize = 11 };
        moveButton.Clicked += (_, _) => share.Order = ToolbarItemOrder.Secondary;

        var toggleButton = new Button { Text = "Toggle Locked", AutomationId = "ToggleLockedItem", FontSize = 11 };
        toggleButton.Clicked += (_, _) => locked.IsEnabled = !locked.IsEnabled;

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageToolbar",
            probeAnchor: null,
            log,
            addButton,
            moveButton,
            toggleButton,
            NavPageFactory.MakeButton("Pop", "PopNavBarToolbar", model.Pop)
        );
    }
}

/// <summary>Pushed page: back button appears; the start-drawer button yields (Auto policy).</summary>
[UsedImplicitly]
public class NavBarDetailPage : ContentPage
{
    public NavBarDetailPage(NavBarDetailPageModel model)
    {
        BindingContext = model;
        Title = "Detail Title";

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageDetail",
            probeAnchor: null,
            NavPageFactory.MakeButton("Pop", "PopNavBarDetail", model.Pop)
        );
    }
}

/// <summary>
/// Pushed page opting the start-drawer button into <see cref="ScaffoldFlyoutButtonVisibility.Visible"/>:
/// drawer and back buttons render side by side.
/// </summary>
[UsedImplicitly]
public class NavBarDrawerDetailPage : ContentPage
{
    public NavBarDrawerDetailPage(NavBarDrawerDetailPageModel model)
    {
        BindingContext = model;
        Title = "Drawer Detail";

        Scaffold.SetFlyoutStartButtonVisibility(this, ScaffoldFlyoutButtonVisibility.Visible);

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageDrawerDetail",
            probeAnchor: null,
            NavPageFactory.MakeButton("Pop", "PopNavBarDrawerDetail", model.Pop)
        );
    }
}

/// <summary>Pushed page installing a page-level CUSTOM nav bar built from the public primitives.</summary>
[UsedImplicitly]
public class NavBarCustomPage : ContentPage
{
    public NavBarCustomPage(NavBarCustomPageModel model)
    {
        BindingContext = model;
        Title = "Custom Title";

        var customBar = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions = { new RowDefinition(GridLength.Auto) },
            BackgroundColor = Colors.MediumPurple,
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container)
        };

        customBar.Add(new ScaffoldBackButton { AutomationId = "CustomNavBarBack" }, 0);

        customBar.Add(
            new Label
            {
                Text = "Custom bar",
                AutomationId = "CustomNavBarMarker",
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 44,
                VerticalTextAlignment = TextAlignment.Center
            },
            1
        );

        Scaffold.SetNavBarTemplate(this, new DataTemplate(() => customBar));

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageCustom",
            probeAnchor: null,
            NavPageFactory.MakeButton("Pop", "PopNavBarCustom", model.Pop)
        );
    }
}


/// <summary>
/// Pushed page installing an EDGE-TO-EDGE custom nav bar: a short bar declaring
/// <c>SafeAreaEdges.None</c>, i.e. an author who wants their own chrome to start at the very top
/// of the screen and paint under the status bar themselves.
/// </summary>
[UsedImplicitly]
public class NavBarEdgeToEdgePage : ContentPage
{
    public NavBarEdgeToEdgePage(NavBarEdgeToEdgePageModel model)
    {
        BindingContext = model;
        Title = "Edge To Edge";

        var customBar = new Grid
        {
            SafeAreaEdges = SafeAreaEdges.None,
            BackgroundColor = Colors.DarkOrange,
            HeightRequest = 20,
            AutomationId = "EdgeToEdgeNavBarMarker"
        };

        Scaffold.SetNavBarTemplate(this, new DataTemplate(() => customBar));

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageEdgeToEdge",
            probeAnchor: null,
            NavPageFactory.MakeButton("Pop", "PopNavBarEdgeToEdge", model.Pop)
        );
    }
}

/// <summary>
/// Pushed page with the DEFAULT bar (title kept whole above its floor) and more primary items than
/// any phone-width bar can show — eight icon items, plus one secondary — so the "if room" fold is
/// exercised on default settings: the first items by priority stay in the bar, the rest fold
/// into the overflow menu ahead of the secondary item, and the title keeps its floor.
/// </summary>
[UsedImplicitly]
public class NavBarFoldPage : ContentPage
{
    public NavBarFoldPage(NavBarFoldPageModel model)
    {
        BindingContext = model;
        Title = "Fold Title";

        for (var i = 1; i <= 8; i++)
        {
            var n = i;

            var item = new ToolbarItem
            {
                Text = $"Fold {n}",
                Priority = n,
                AutomationId = $"ToolbarFold{n}",
                IconImageSource = new FontImageSource { FontFamily = "Material", Glyph = "\ue838", Size = 24 }
            };

            item.Clicked += (_, _) => model.LastActivated = $"fold{n}";
            ToolbarItems.Add(item);
        }

        var secondary = new ToolbarItem { Text = "Fold Secondary", Order = ToolbarItemOrder.Secondary, AutomationId = "ToolbarFoldSecondary" };
        secondary.Clicked += (_, _) => model.LastActivated = "secondary";
        ToolbarItems.Add(secondary);

        var log = new Label { AutomationId = "FoldActivationLog", FontSize = 14 };
        log.SetBinding(Label.TextProperty, nameof(NavBarFoldPageModel.LastActivated));

        Content = NavBarPageFactory.BuildContent(
            "NavBarPageFold",
            probeAnchor: null,
            log,
            NavPageFactory.MakeButton("Pop", "PopNavBarFold", model.Pop)
        );
    }
}

/// <summary>
/// Scaffold harness exercising the default nav bar (§5.2): a single plain area (no tab bar),
/// a global start flyout feeding the drawer button, titles per page, back/pop through the
/// nav bar, per-page drawer-button policy, per-page custom bar, visibility toggling.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold NavBar Tests")]
public class NavBarScaffold : Scaffold
{
    public NavBarScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Home", PageType = typeof(NavBarHomePage) });

        // Closed flyouts keep stale element-tree bounds (only the platform view unmounts):
        // the close handler records deterministic completion for the UI tests instead.
        var stateLabel = new Label { Text = "-", AutomationId = "NavFlyoutState", FontSize = 11 };

        var closeButton = NavPageFactory.MakeButton(
            "Close",
            "CloseNavFlyout",
            async () =>
            {
                await CloseFlyoutAsync();
                stateLabel.Text = "closed";
            }
        );

        // Mode Flyout: available on every page (the drawer requires content + an enabling mode).
        Scaffold.SetFlyoutStartMode(this, ScaffoldFlyoutMode.Flyout);

        FlyoutStart = new VerticalStackLayout
        {
            AutomationId = "GlobalNavFlyout",
            BackgroundColor = Colors.White,
            Padding = 16,
            Spacing = 8,
            Children =
            {
                new Label { Text = "Nav flyout", AutomationId = "GlobalNavFlyoutLabel", FontSize = 18, FontAttributes = FontAttributes.Bold },
                closeButton,
                stateLabel
            }
        };
    }
}
