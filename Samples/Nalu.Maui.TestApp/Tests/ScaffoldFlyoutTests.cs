using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class FlyoutHomePageModel(INavigationService navigationService, IScaffoldFlyoutController flyoutController) : ObservableObject
{
    // Bound by the page-level END drawer content: proves the drawer inherits the page's
    // BindingContext through logical parenting.
    public string Marker => "HomeModel";

    public Task PushDetail() => navigationService.GoToAsync(Navigation.Relative().Push<FlyoutDetailPageModel>());

    public Task OpenStart() => flyoutController.OpenAsync(ScaffoldFlyoutSide.Start);

    public Task OpenEnd() => flyoutController.OpenAsync(ScaffoldFlyoutSide.End);

    public Task Close() => flyoutController.CloseAsync();
}

[UsedImplicitly]
public partial class FlyoutDetailPageModel(INavigationService navigationService, IScaffoldFlyoutController flyoutController) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());

    public Task OpenStart() => flyoutController.OpenAsync(ScaffoldFlyoutSide.Start);

    public Task OpenEnd() => flyoutController.OpenAsync(ScaffoldFlyoutSide.End);
}

[UsedImplicitly]
public partial class FlyoutAlphaPageModel : ObservableObject;

[UsedImplicitly]
public partial class FlyoutBetaPageModel : ObservableObject;

[UsedImplicitly]
public partial class FlyoutTabPageModel : ObservableObject;

/// <summary>
/// Home page (single-root area): exercises the drawer controller (page-scope DI), the Auto
/// mode (drawer at stack roots only), the page-level END mode enablement and the per-side
/// open-state events (mirrored into <c>FlyoutStateLabel</c>).
/// </summary>
[UsedImplicitly]
public class FlyoutHomePage : ContentPage
{
    private readonly Label _stateLabel;
    private Scaffold? _observedScaffold;

    public FlyoutHomePage(FlyoutHomePageModel model)
    {
        BindingContext = model;
        Title = "FlyoutHome";

        // END drawer fully page-level: content AND mode set here (the scaffold default stays
        // Disabled with no content). The "stack of flyouts" resolution keeps BOTH alive on
        // pages pushed on top; the drawer content inherits THIS page's BindingContext.
        Scaffold.SetFlyoutEndMode(this, ScaffoldFlyoutMode.Flyout);

        var boundLabel = new Label { AutomationId = "EndFlyoutBoundLabel", FontSize = 14 };
        boundLabel.SetBinding(Label.TextProperty, nameof(FlyoutHomePageModel.Marker));

        var closeEndLabel = new Label { Text = "Close", AutomationId = "CloseEndFlyoutButton", FontSize = 16, TextColor = Colors.Blue, Padding = new Thickness(12, 8) };
        var closeEndTap = new TapGestureRecognizer();
        closeEndTap.Tapped += (_, _) => _ = model.Close();
        closeEndLabel.GestureRecognizers.Add(closeEndTap);

        Scaffold.SetFlyoutEnd(
            this,
            new VerticalStackLayout
            {
                AutomationId = "EndFlyoutPanel",
                BackgroundColor = Colors.White,
                Padding = 16,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "End panel", AutomationId = "EndFlyoutLabel", FontSize = 18 },
                    boundLabel,
                    closeEndLabel
                }
            }
        );

        _stateLabel = new Label { AutomationId = "FlyoutStateLabel", Text = "idle" };

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "FlyoutHomePage", AutomationId = "FlyoutHomePage", FontSize = 20, FontAttributes = FontAttributes.Bold });
        stack.Add(_stateLabel);
        stack.Add(NavPageFactory.MakeButton("Open start", "OpenStartFlyoutButton", model.OpenStart));
        stack.Add(NavPageFactory.MakeButton("Open end", "OpenEndFlyoutButton", model.OpenEnd));
        stack.Add(NavPageFactory.MakeButton("Push detail", "PushFlyoutDetail", model.PushDetail));

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitFlyoutHome", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();

        // Hosted pages are logical descendants of the scaffold: walk up and mirror the
        // per-side open events into the asserted label.
        Element? element = this;

        while (element is not null and not Scaffold)
        {
            element = element.Parent;
        }

        if (element is Scaffold scaffold && !ReferenceEquals(_observedScaffold, scaffold))
        {
            _observedScaffold = scaffold;
            scaffold.FlyoutStartOpened += (_, _) => _stateLabel.Text = $"start-open:{scaffold.IsFlyoutStartOpen}";
            scaffold.FlyoutStartClosed += (_, _) => _stateLabel.Text = $"start-closed:{scaffold.IsFlyoutStartOpen}";
            scaffold.FlyoutEndOpened += (_, _) => _stateLabel.Text = $"end-open:{scaffold.IsFlyoutEndOpen}";
            scaffold.FlyoutEndClosed += (_, _) => _stateLabel.Text = $"end-closed:{scaffold.IsFlyoutEndOpen}";
        }
    }
}

/// <summary>Pushed page: with the start mode Auto the drawer must NOT be available here.</summary>
[UsedImplicitly]
public class FlyoutDetailPage : ContentPage
{
    public FlyoutDetailPage(FlyoutDetailPageModel model)
    {
        BindingContext = model;
        Title = "FlyoutDetail";

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = "FlyoutDetailPage", AutomationId = "FlyoutDetailPage", FontSize = 20, FontAttributes = FontAttributes.Bold });
        stack.Add(NavPageFactory.MakeButton("Open start (no-op)", "OpenStartFromDetailButton", model.OpenStart));
        stack.Add(NavPageFactory.MakeButton("Open end (inherited)", "OpenEndFromDetailButton", model.OpenEnd));
        stack.Add(NavPageFactory.MakeButton("Pop", "PopFlyoutDetail", model.Pop));

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitFlyoutDetail", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }
}

[UsedImplicitly]
public class FlyoutAlphaPage : ContentPage
{
    public FlyoutAlphaPage(FlyoutAlphaPageModel model)
    {
        BindingContext = model;
        Title = "Alpha";
        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12,
            Children = { new Label { Text = "FlyoutAlphaPage", AutomationId = "FlyoutAlphaPage", FontSize = 20 } }
        };

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitFlyoutAlpha", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }
}

[UsedImplicitly]
public class FlyoutBetaPage : ContentPage
{
    public FlyoutBetaPage(FlyoutBetaPageModel model)
    {
        BindingContext = model;
        Title = "Beta";
        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12,
            Children = { new Label { Text = "FlyoutBetaPage", AutomationId = "FlyoutBetaPage", FontSize = 20 } }
        };

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitFlyoutBeta", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }
}

[UsedImplicitly]
public class FlyoutTabPage : ContentPage
{
    public FlyoutTabPage(FlyoutTabPageModel model)
    {
        BindingContext = model;
        Title = "FlyoutTab";
        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12,
            Children = { new Label { Text = "FlyoutTabPage", AutomationId = "FlyoutTabPage", FontSize = 20 } }
        };

        var exitButton = new Button { Text = "Exit", AutomationId = "ExitFlyoutTab", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App)Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }
}

/// <summary>
/// Scaffold harness exercising the §5.5 flyout completion: the default
/// <see cref="ScaffoldFlyoutMenuView"/> (flat entry for the single-root area, group header for
/// the multi-root area, hidden root and tab-bar area excluded), Auto/page-level modes, width
/// options, open-state events and the page-scope controller.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Flyout Tests")]
public class FlyoutScaffold : Scaffold
{
    public FlyoutScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        InitialRootPageType = typeof(FlyoutHomePage);

        // Single-root area → flat menu entry.
        Areas.Add(new ScaffoldRoot { Title = "FlyoutHome", PageType = typeof(FlyoutHomePage) });

        // Multi-root area (one root hidden) → "Zone" group header + Alpha/Beta entries.
        Areas.Add(
            new ScaffoldArea
            {
                Title = "Zone",
                Roots =
                {
                    new ScaffoldRoot { Title = "Alpha", PageType = typeof(FlyoutAlphaPage) },
                    new ScaffoldRoot { Title = "Beta", PageType = typeof(FlyoutBetaPage) },
                    new ScaffoldRoot { Title = "Ghost", PageType = typeof(FlyoutTabPage), IsVisible = false }
                }
            }
        );

        // Tab-bar area: excluded from the menu (IsTabBarDisplayed defaults to false).
        Areas.Add(
            new ScaffoldTabBar
            {
                Title = "Tabs",
                Roots = { new ScaffoldRoot { Title = "TabOne", PageType = typeof(FlyoutTabPage) } }
            }
        );

        // START drawer: default menu, Auto mode (stack roots only), explicit width.
        SetFlyoutStartMode(this, ScaffoldFlyoutMode.Auto);
        FlyoutStartOptions = new ScaffoldFlyoutOptions { Width = 300 };

        FlyoutStart = new ScaffoldFlyoutMenuView
        {
            AutomationId = "StartFlyoutMenu",
            HeaderView = new Label { Text = "Menu", AutomationId = "StartFlyoutHeader", FontSize = 18, FontAttributes = FontAttributes.Bold },

            // Overlay-hosted controls need TapGestureRecognizer-based taps (DevFlow limit).
            FooterView = MakeTapLabel("Close", "CloseStartFlyoutButton", () => CloseFlyoutAsync())
        };

        // The END drawer is fully page-level (see FlyoutHomePage): no scaffold-level content.
    }

    private static View MakeTapLabel(string text, string automationId, Func<Task> onTapped)
    {
        var label = new Label
        {
            Text = text,
            AutomationId = automationId,
            FontSize = 16,
            TextColor = Colors.Blue,
            Padding = new Thickness(12, 8)
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => _ = onTapped();
        label.GestureRecognizers.Add(tap);

        return label;
    }
}
