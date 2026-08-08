using CommunityToolkit.Mvvm.ComponentModel;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

[UsedImplicitly]
public partial class MoRootPageModel(INavigationService navigationService) : ObservableObject
{
    public Task PushDetail() => navigationService.GoToAsync(Navigation.Relative().Push<MoDetailPageModel>());

    public Task PushCustom() => navigationService.GoToAsync(Navigation.Relative().Push<MoCustomPageModel>());

    public Task PushShared() => navigationService.GoToAsync(Navigation.Relative().Push<MoSharedPageModel>());

    /// <summary>Selects a root in ANOTHER area — the switch that cross-fades.</summary>
    public Task GoFar() => navigationService.GoToAsync(Navigation.Absolute().Root<MoFarPageModel>());

    /// <summary>Pushes a page that keeps its nav bar — the one whose INSETS are under test.</summary>
    public Task PushInset() => navigationService.GoToAsync(Navigation.Relative().Push<MoInsetPageModel>());
}

/// <summary>Gives the second tab root a page model (kept from the pre-source-generator era; the generated AddPages() maps it by naming convention).</summary>
[UsedImplicitly]
public partial class MoSecondPageModel : ObservableObject;

[UsedImplicitly]
public partial class MoInsetPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Pop() => navigationService.GoToAsync(Navigation.Relative().Pop());
}

[UsedImplicitly]
public partial class MoFarPageModel(INavigationService navigationService) : ObservableObject
{
    public Task Back() => navigationService.GoToAsync(Navigation.Absolute().Root<MoRootPageModel>());
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
        : this(background, label, navBar: false, controls)
    {
    }

    protected MoPageBase(Color background, string label, bool navBar, params View[] controls)
    {
        BackgroundColor = background;

        // No chrome by default: the whole window belongs to the page, so window-relative sampling
        // needs no correction for a nav bar strip. MoInsetPage opts back in — its whole point is
        // that a page mounted DURING a transition is padded for the chrome from its first frame.
        Scaffold.SetIsNavBarVisible(this, navBar);

        var stack = new VerticalStackLayout { Spacing = 12, Padding = 16 };
        stack.Add(new Label { Text = label, AutomationId = label, FontSize = 22, TextColor = Colors.White });

        foreach (var control in controls)
        {
            stack.Add(control);
        }

        // Every page of the harness, not just the root: a test left on any of them (a tab root,
        // the cross-area root) must still be able to reset the app.
        var exitButton = new Button { Text = "Exit", AutomationId = $"Exit{label}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

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

/// <summary>Second root of the SAME area: switching to it slides, both pages travelling together.</summary>
[UsedImplicitly]
public class MoSecondPage : MoPageBase
{
    /// <summary>The colour a UI test looks for when this root is on screen.</summary>
    public static readonly Color SecondColor = Color.FromRgb(200, 120, 0);

    public MoSecondPage(MoSecondPageModel model)
        : base(SecondColor, "MoSecondPage")
        => BindingContext = model;
}

/// <summary>Root of ANOTHER area: switching to it cross-fades instead of sliding.</summary>
[UsedImplicitly]
public class MoFarPage : MoPageBase
{
    /// <summary>The colour a UI test looks for when this root is on screen.</summary>
    public static readonly Color FarColor = Color.FromRgb(0, 90, 160);

    public MoFarPage(MoFarPageModel model)
        : base(FarColor, "MoFarPage", NavPageFactory.MakeButton("Back", "MoAreaBackSelector", model.Back))
        => BindingContext = model;
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
            NavPageFactory.MakeButton("Go far", "MoAreaFarSelector", model.GoFar),
            NavPageFactory.MakeButton("Push inset", "PushMoInset", model.PushInset),
            MakeHero("MoRootHero", Colors.Orange, 80)
        )
        => BindingContext = model;
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
/// Pushed page that KEEPS its nav bar, on the same slow spec. A page mounted while a transition
/// plays must be laid out for the chrome it will land under from its very first frame: laid out
/// against stale insets it sits too high and snaps down when the transition ends.
/// </summary>
[UsedImplicitly]
public class MoInsetPage : MoPageBase
{
    public MoInsetPage(MoInsetPageModel model)
        : base(Color.FromRgb(60, 60, 60), "MoInsetPage", navBar: true, NavPageFactory.MakeButton("Pop", "PopMoInset", model.Pop))
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

        // Root switches take their duration from the SCAFFOLD-level spec: stretched here like the
        // page transitions, so a test can sample what is on screen while the two roots travel.
        SetPageTransition(
            this,
            new ScaffoldPageTransition(
                new ScaffoldTransitionMotion(FractionX: 1),
                new ScaffoldTransitionMotion(),
                MoDetailPage.TransitionSeconds
            )
        );

        // Two roots in ONE area (their switch slides) plus a root in ANOTHER area (its switch
        // cross-fades): the two root-switch choreographies, over flat colours a test can sample.
        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "One", PageType = typeof(MoRootPage), AutomationId = "MoTabOne" },
                    new ScaffoldRoot { Title = "Two", PageType = typeof(MoSecondPage), AutomationId = "MoTabTwo" }
                }
            }
        );

        Areas.Add(new ScaffoldRoot { Title = "Far", PageType = typeof(MoFarPage), AutomationId = "MoAreaFar" });
    }
}
