using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class MoRootPageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushDetail() => navigationService.GoToAsync(Navigation.Relative().Push<MoDetailPageModel>());

    public Task PushCustom() => navigationService.GoToAsync(Navigation.Relative().Push<MoCustomPageModel>());

    public Task PushShared() => navigationService.GoToAsync(Navigation.Relative().Push<MoSharedPageModel>());
}

[UsedImplicitly]
public partial class MoDetailPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class MoCustomPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class MoSharedPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

/// <summary>Base of the motion harness pages: one flat, unmistakable colour to sample.</summary>
public abstract class MoPageBase : ContentPage
{
    /// <summary>The colour a UI test looks for when it samples this page's pixels.</summary>
    public static readonly Color RootColor = Color.FromRgb(0, 160, 0);

    /// <summary>The pushed page's colour — far enough from <see cref="RootColor"/> to be unmistakable.</summary>
    public static readonly Color DetailColor = Color.FromRgb(0, 0, 200);

    protected MoPageBase(Color background, string label, params View[] controls)
    {
        BackgroundColor = background;

        // No chrome: the whole window belongs to the page, so window-relative sampling needs no
        // correction for a nav bar strip.
        Scaffold.SetIsNavBarVisible(this, false);

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = label, AutomationId = label, FontSize = 22, TextColor = Colors.White });

        foreach (var control in controls)
        {
            stack.Add(control);
        }

        // Controls stay at the TOP: tests sample the lower half, where nothing but the page's
        // own background is ever drawn.
        Content = new Grid { Children = { stack } };
    }

    /// <summary>
    /// The source/destination of the shared-element pair: a plain coloured box, sized differently
    /// on each page so a flight that lands is visible in the geometry alone.
    /// </summary>
    protected static View MakeHero(string automationId, Color color, double size)
    {
        var hero = new BoxView
                   {
                       AutomationId = automationId,
                       Color = color,
                       WidthRequest = size,
                       HeightRequest = size,
                       HorizontalOptions = LayoutOptions.Start
                   };

        Scaffold.SetTransitionName(hero, "MoHero");

        return hero;
    }
}

/// <summary>Root of the motion harness.</summary>
[UsedImplicitly]
public class MoRootPage : MoPageBase
{
    public MoRootPage(MoRootPageModel model)
        : base(
            RootColor,
            "MoRootPage",
            NavPageFactory.MakeButton("Push detail", "PushMoDetail", model.PushDetail),
            NavPageFactory.MakeButton("Push custom", "PushMoCustom", model.PushCustom),
            NavPageFactory.MakeButton("Push shared", "PushMoShared", model.PushShared),
            MakeHero("MoRootHero", Colors.Orange, 80),
            MakeExitButton()
        )
        => BindingContext = model;


    private static Button MakeExitButton()
    {
        var exitButton = new Button { Text = "Exit", AutomationId = "ExitMoRoot", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();

        return exitButton;
    }
}

/// <summary>
/// Pushed page carrying a DELIBERATELY SLOW stock slide: the motion is the same one the default
/// spec plays, stretched so that a screenshot taken between two agent round trips lands in the
/// middle of it.
/// </summary>
[UsedImplicitly]
public class MoDetailPage : MoPageBase
{
    /// <summary>The harness duration — long enough to sample, short enough not to drag a suite.</summary>
    public const double TransitionSeconds = 1.5;

    public MoDetailPage(MoDetailPageModel model)
        : base(DetailColor, "MoDetailPage", NavPageFactory.MakeButton("Pop", "PopMoDetail", model.Pop))
    {
        BindingContext = model;

        Scaffold.SetPageTransition(
            this,
            new ScaffoldPageTransition(
                new ScaffoldTransitionMotion(FractionX: 1),
                new ScaffoldTransitionMotion(),
                TransitionSeconds
            )
        );
    }
}

/// <summary>
/// Pushed page carrying a CUSTOM spec: it enters from the BOTTOM and fades, and pushes the page
/// it covers away with a scale + dim (a Behind motion that is not identity). Slowed to the same
/// harness duration so both halves can be sampled mid-flight.
/// </summary>
[UsedImplicitly]
public class MoCustomPage : MoPageBase
{
    /// <summary>The colour a UI test looks for when this page is on screen.</summary>
    public static readonly Color CustomColor = Color.FromRgb(200, 0, 0);

    public MoCustomPage(MoCustomPageModel model)
        : base(CustomColor, "MoCustomPage", NavPageFactory.MakeButton("Pop", "PopMoCustom", model.Pop))
    {
        BindingContext = model;

        Scaffold.SetPageTransition(
            this,
            new ScaffoldPageTransition(
                new ScaffoldTransitionMotion(FractionY: 1),
                new ScaffoldTransitionMotion(Scale: 0.9, Opacity: 0.5),
                MoDetailPage.TransitionSeconds
            )
        );
    }
}

/// <summary>
/// Pushed page carrying a SHARED ELEMENT (<c>Scaffold.TransitionName</c> "MoHero") plus its own
/// slow spec: the flight and the page motion have to coexist — the outgoing page must still be
/// held on screen, and the hero must land at this page's (larger) geometry.
/// </summary>
[UsedImplicitly]
public class MoSharedPage : MoPageBase
{
    /// <summary>The colour a UI test looks for when this page is on screen.</summary>
    public static readonly Color SharedColor = Color.FromRgb(120, 0, 160);

    public MoSharedPage(MoSharedPageModel model)
        : base(
            SharedColor,
            "MoSharedPage",
            NavPageFactory.MakeButton("Pop", "PopMoShared", model.Pop),
            MakeHero("MoSharedHero", Colors.Orange, 200)
        )
    {
        BindingContext = model;

        Scaffold.SetPageTransition(
            this,
            new ScaffoldPageTransition(
                new ScaffoldTransitionMotion(FractionX: 1),
                new ScaffoldTransitionMotion(),
                MoDetailPage.TransitionSeconds
            )
        );
    }
}

/// <summary>
/// Harness for the page-motion contract (§8.2) — what is ON SCREEN while a transition plays,
/// which the end-state transition tests cannot see: the covered page must stay visible UNDER a
/// pushed page (its disappearance flashes the window background), and a popped page must stay
/// visible ABOVE the page it reveals until it is gone.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Motion Tests")]
public class MotionScaffold : Scaffold
{
    public MotionScaffold(INavigationService navigationService)
    {
        _ = navigationService;

        Areas.Add(new ScaffoldRoot { Title = "Motion", PageType = typeof(MoRootPage) });
    }
}
