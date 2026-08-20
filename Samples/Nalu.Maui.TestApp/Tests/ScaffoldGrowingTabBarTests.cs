using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class GrowBarPageModel : ObservableObject;

/// <summary>
/// Page hosted under the growing bar: carries the toggle and a bottom-anchored marker whose
/// position witnesses the inset the bar contributes.
/// </summary>
[UsedImplicitly]
public class GrowBarPage : ContentPage
{
    public GrowBarPage(GrowBarPageModel model)
    {
        BindingContext = model;
        Title = "Grow bar";

        // The page gets the GROWING nav bar too: same runtime-height contract, top edge.
        Scaffold.SetIsNavBarVisible(this, true);
        Scaffold.SetNavBarTemplate(this, new DataTemplate(GrowingBarScaffold.CreateNavBar));

        var toggle = new Button { Text = "Toggle bar height", AutomationId = "GrowBarToggle", FontSize = 12 };
        toggle.Clicked += (_, _) => GrowingBarScaffold.ToggleBand();

        var navToggle = new Button { Text = "Toggle nav height", AutomationId = "GrowNavToggle", FontSize = 12 };
        navToggle.Clicked += (_, _) => GrowingBarScaffold.ToggleNavBand();

        var exit = new Button { Text = "Exit", AutomationId = "ExitGrowBar", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exit.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();

        Content = new Grid
        {
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 8,
                    Padding = 16,
                    Children =
                    {
                        new Label { Text = "GrowBarPage", AutomationId = "GrowBarPage", FontSize = 20 },
                        toggle,
                        navToggle,
                        exit
                    }
                },

                // Edge-anchored within the page: where these land IS the page's usable top and
                // bottom, so they must track the bars' footprints exactly.
                new BoxView
                {
                    AutomationId = "GrowBarTopMarker",
                    Color = Colors.DarkOrange,
                    HeightRequest = 10,
                    VerticalOptions = LayoutOptions.Start
                },
                new BoxView
                {
                    AutomationId = "GrowBarBottomMarker",
                    Color = Colors.DarkOrange,
                    HeightRequest = 10,
                    VerticalOptions = LayoutOptions.End
                }
            }
        };
    }
}

/// <summary>
/// Harness for a custom tab bar whose HEIGHT CHANGES AT RUNTIME: a button toggles the bar's band
/// between two heights. No other harness does this — every other bar answers the same measure for
/// its whole lifetime, so the "bar content changed, re-measure and re-inset" path (MAUI's measure
/// invalidation propagating from the bar's subtree up through the strip to the host) had no
/// coverage at all.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Growing TabBar Tests")]
public class GrowingBarScaffold : Scaffold
{
    public const double CompactHeight = 60;
    public const double TallHeight = 120;

    public const double CompactNavHeight = 44;
    public const double TallNavHeight = 88;

    private static BoxView? _band;
    private static BoxView? _navBand;

    /// <summary>Static on purpose: the page button reaches the CURRENT harness instance's band.</summary>
    public static void ToggleBand()
    {
        if (_band is { } band)
        {
            band.HeightRequest = band.HeightRequest >= TallHeight ? CompactHeight : TallHeight;
        }
    }

    /// <summary>Same runtime-height contract as the tab bar band, on the nav bar.</summary>
    public static void ToggleNavBand()
    {
        if (_navBand is { } band)
        {
            band.HeightRequest = band.HeightRequest >= TallNavHeight ? CompactNavHeight : TallNavHeight;
        }
    }

    /// <summary>
    /// A custom nav bar whose height changes at runtime: the top-edge mirror of the growing tab
    /// bar. The root consumes the top inset (Container), so the strip's measure includes it and
    /// the page's top inset must track the CONTENT height across toggles.
    /// </summary>
    public static View CreateNavBar()
    {
        _navBand = new BoxView
        {
            AutomationId = "GrowNavBand",
            Color = Colors.MediumPurple,
            HeightRequest = CompactNavHeight,
            VerticalOptions = LayoutOptions.End
        };

        return new Grid
        {
            SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.Container, SafeAreaRegions.None, SafeAreaRegions.None),
            BackgroundColor = Colors.DarkSlateBlue,
            AutomationId = "GrowNavRoot",
            Children = { _navBand }
        };
    }

    public GrowingBarScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        _band = new BoxView
        {
            AutomationId = "GrowBarBand",
            Color = Colors.MediumSeaGreen,
            HeightRequest = CompactHeight,
            VerticalOptions = LayoutOptions.Start
        };

        Areas.Add(
            new ScaffoldTabBar
            {
                TabBarView = new Grid
                {
                    SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.None, SafeAreaRegions.Container),
                    BackgroundColor = Colors.DarkSlateGray,
                    AutomationId = "GrowBarRoot",
                    Children = { _band }
                },
                Roots =
                {
                    new ScaffoldRoot { Title = "Grow", PageType = typeof(GrowBarPage) }
                }
            }
        );
    }
}
