using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class MgHomePageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushModal() => navigationService.GoToAsync(Navigation.Relative().Push<MgModalPageModel>());
}

[UsedImplicitly]
public partial class MgModalPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Close() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class MgOtherPageModel : ObservableObject;

/// <summary>
/// Tab root that a modal covers. Edge-anchored markers witness the page's usable band: where they
/// land IS the geometry the modal must give back untouched on dismiss.
/// </summary>
[UsedImplicitly]
public class MgHomePage : ContentPage
{
    public MgHomePage(MgHomePageModel model)
    {
        BindingContext = model;
        Title = "Mg home";
        BackgroundColor = Colors.WhiteSmoke;

        var exit = new Button { Text = "Exit", AutomationId = "ExitMgHome", FontSize = 11, BackgroundColor = Colors.IndianRed };
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
                        new Label { Text = "MgHomePage", AutomationId = "MgHomePage", FontSize = 20, FontAttributes = FontAttributes.Bold },
                        OrientationProbe.CreateControls(),
                        NavPageFactory.MakeButton("Push modal", "PushMgModal", model.PushModal),
                        exit
                    }
                },
                new BoxView
                {
                    AutomationId = "MgHomeTopMarker",
                    Color = Colors.DarkOrange,
                    HeightRequest = 10,
                    VerticalOptions = LayoutOptions.Start
                },
                new BoxView
                {
                    AutomationId = "MgHomeBottomMarker",
                    Color = Colors.DarkOrange,
                    HeightRequest = 10,
                    VerticalOptions = LayoutOptions.End
                }
            }
        };
    }
}

/// <summary>Second tab root, present so the harness has a real tab bar for the modal to cover.</summary>
[UsedImplicitly]
public class MgOtherPage : ContentPage
{
    public MgOtherPage(MgOtherPageModel model)
    {
        BindingContext = model;
        Title = "Mg other";

        Content = new VerticalStackLayout
        {
            Padding = 16,
            Children = { new Label { Text = "MgOtherPage", AutomationId = "MgOtherPage", FontSize = 20 } }
        };
    }
}

/// <summary>
/// The <see cref="ScaffoldPageMode.DismissableModal"/> page whose GEOMETRY is under test: edge
/// markers witness its usable band (top = the modal nav bar's bottom edge, bottom = the band the
/// covered tab bar used to occupy), the safe-area probe gives the platform inset ground truth,
/// and the rotation controls let a test change the window shape while the modal is presented.
/// </summary>
[UsedImplicitly]
public class MgModalPage : ContentPage
{
    public MgModalPage(MgModalPageModel model)
    {
        BindingContext = model;
        Title = "Mg modal";
        Scaffold.SetPageMode(this, ScaffoldPageMode.DismissableModal);
        BackgroundColor = Colors.Lavender;

        var exit = new Button { Text = "Exit", AutomationId = "ExitMgModal", FontSize = 11, BackgroundColor = Colors.IndianRed };
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
                        new Label { Text = "MgModalPage", AutomationId = "MgModalPage", FontSize = 20, FontAttributes = FontAttributes.Bold },
                        OrientationProbe.CreateControls(),
                        SafeAreaProbe.CreateProbe(this),
                        NavPageFactory.MakeButton("Close", "CloseMgModal", model.Close),
                        exit
                    }
                },
                new BoxView
                {
                    AutomationId = "MgModalTopMarker",
                    Color = Colors.MediumPurple,
                    HeightRequest = 10,
                    VerticalOptions = LayoutOptions.Start
                },
                new BoxView
                {
                    AutomationId = "MgModalBottomMarker",
                    Color = Colors.MediumPurple,
                    HeightRequest = 10,
                    VerticalOptions = LayoutOptions.End
                }
            }
        };
    }
}

/// <summary>
/// Harness for the GEOMETRY of modal presentation (§7.1): what a modal page's usable edges are
/// while it covers the tab bar, and what it must give back on dismiss. The chrome/back-channel
/// half of the modal contract lives in the "Scaffold Modal Tests" harness; this one carries the
/// witnesses that half cannot ask for — edge markers on both the covered page and the modal, the
/// safe-area probe on the modal, and rotation controls on both pages.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Modal Geometry Tests")]
public class ModalGeometryScaffold : Scaffold
{
    public ModalGeometryScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "MgOne", PageType = typeof(MgHomePage) },
                    new ScaffoldRoot { Title = "MgTwo", PageType = typeof(MgOtherPage) }
                }
            }
        );
    }
}
