using JetBrains.Annotations;
using MauiReactor;
using MauiControls = Microsoft.Maui.Controls;

namespace Nalu.Maui.TestApp.Tests;

// Component-based navigation harness (MauiReactor): NO pages, NO page models — MauiReactor
// components registered with AddPage<TComponent>() are the navigation destinations AND the
// lifecycle targets (IEnteringAware/IAppearingAware/ILeavingGuard/typed intents implemented
// directly on the component). The native page is produced by the app-side
// MauiReactorComponentPageFactory (TemplateHost) and Nalu drives it like any other page:
// DI scope, lifecycle, disposal.
// Components are created by the engine through DI (constructor-injected INavigationService).
// Registration is source-generated: [AutoNavigationPage] on a non-Page class is the OPT-IN that
// makes the generated AddPages() emit the model-less AddPage<TComponent>() for it.

public class ReactorLifecycleState
{
    public int Entering { get; set; }
    public int Appearing { get; set; }
    public int Disappearing { get; set; }
}

/// <summary>
/// Component root page: lifecycle counters rendered from component STATE (every bump is a
/// SetState re-render into the same native page), a push-with-intent and a push-to-guard button.
/// </summary>
[UsedImplicitly]
[AutoNavigationPage]
public class ReactorOnePage(INavigationService navigationService) : Component<ReactorLifecycleState>, IEnteringAware, IAppearingAware, IDisappearingAware
{
    public ValueTask OnEnteringAsync()
    {
        SetState(s => s.Entering++);

        return default;
    }

    public ValueTask OnAppearingAsync()
    {
        SetState(s => s.Appearing++);

        return default;
    }

    public ValueTask OnDisappearingAsync()
    {
        SetState(s => s.Disappearing++);

        return default;
    }

    public override VisualNode Render()
        => ContentPage(
            VStack(
                    Label("Reactor ONE root")
                        .AutomationId("ReactorOnePage")
                        .FontSize(22),
                    Label($"E{State.Entering} A{State.Appearing} D{State.Disappearing}")
                        .AutomationId("ReactorOneLifecycle")
                        .FontSize(14),
                    Button("Push detail (intent 42)")
                        .AutomationId("PushReactorDetail")
                        .FontSize(11)
                        .OnClicked(() => navigationService.GoToAsync(Nav.Push<ReactorDetailPage>(42))),
                    Button("Push guard page")
                        .AutomationId("PushReactorGuard")
                        .FontSize(11)
                        .OnClicked(() => navigationService.GoToAsync(Nav.Push<ReactorGuardPage>())),
                    ReactorScaffold.ExitButton("ExitReactorOne")
                )
                .Spacing(6)
                .Padding(16)
        ).Title("One");
}

/// <summary>Second component root: the tab-switch target for stack-preservation checks.</summary>
[UsedImplicitly]
[AutoNavigationPage]
public class ReactorTwoPage : Component
{
    public override VisualNode Render()
        => ContentPage(
            VStack(
                    Label("Reactor TWO root")
                        .AutomationId("ReactorTwoPage")
                        .FontSize(22),
                    ReactorScaffold.ExitButton("ExitReactorTwo")
                )
                .Spacing(6)
                .Padding(16)
        ).Title("Two");
}

public class ReactorDetailState
{
    public int Intent { get; set; }
}

/// <summary>
/// Pushed component receiving a typed intent directly on the component
/// (<see cref="IEnteringAware{TIntent}" /> with no page model).
/// </summary>
[UsedImplicitly]
[AutoNavigationPage]
public class ReactorDetailPage(INavigationService navigationService) : Component<ReactorDetailState>, IEnteringAware<int>
{
    public ValueTask OnEnteringAsync(int intent)
    {
        SetState(s => s.Intent = intent);

        return default;
    }

    public override VisualNode Render()
        => ContentPage(
            VStack(
                    Label("Reactor detail")
                        .AutomationId("ReactorDetailPage")
                        .FontSize(22),
                    Label(State.Intent.ToString())
                        .AutomationId("ReactorDetailIntent")
                        .FontSize(14),
                    Button("Pop")
                        .AutomationId("PopReactorDetail")
                        .FontSize(11)
                        .OnClicked(() => navigationService.GoToAsync(Nav.Pop())),
                    ReactorScaffold.ExitButton("ExitReactorDetail")
                )
                .Spacing(6)
                .Padding(16)
        ).Title("Detail");
}

public class ReactorGuardState
{
    public int Checks { get; set; }
    public bool AllowLeave { get; set; }
}

/// <summary>
/// Pushed component implementing <see cref="ILeavingGuard" /> directly: every leave path
/// (pop button, system back, edge swipe) must consult the COMPONENT's guard. Starts in DENY mode.
/// </summary>
[UsedImplicitly]
[AutoNavigationPage]
public class ReactorGuardPage(INavigationService navigationService) : Component<ReactorGuardState>, ILeavingGuard
{
    public ValueTask<bool> CanLeaveAsync()
    {
        SetState(s => s.Checks++);

        return ValueTask.FromResult(State.AllowLeave);
    }

    public override VisualNode Render()
        => ContentPage(
            VStack(
                    Label("Reactor guard")
                        .AutomationId("ReactorGuardPage")
                        .FontSize(22),
                    Label(State.Checks.ToString())
                        .AutomationId("ReactorGuardChecks")
                        .FontSize(14),
                    Label(State.AllowLeave ? "allow" : "deny")
                        .AutomationId("ReactorGuardAllow")
                        .FontSize(14),
                    Button("Toggle allow")
                        .AutomationId("ReactorGuardToggle")
                        .FontSize(11)
                        .OnClicked(() => SetState(s => s.AllowLeave = !s.AllowLeave)),
                    Button("Pop")
                        .AutomationId("PopReactorGuard")
                        .FontSize(11)
                        .OnClicked(() => navigationService.GoToAsync(Nav.Pop())),
                    ReactorScaffold.ExitButton("ExitReactorGuard")
                )
                .Spacing(6)
                .Padding(16)
        ).Title("Guard");
}

/// <summary>
/// Scaffold harness for MauiReactor component navigation: two tab roots, both components —
/// lifecycle, intents, guards and tab-stack preservation all ride the component-as-target path.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Reactor Tests")]
public class ReactorScaffold : Scaffold
{
    public ReactorScaffold()
    {
        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    MakeRoot<ReactorOnePage>("One", ""), // home
                    MakeRoot<ReactorTwoPage>("Two", "") // search
                }
            }
        );
    }

    internal static VisualNode ExitButton(string automationId)
        => new MauiReactor.Button()
           .Text("Exit")
           .AutomationId(automationId)
           .FontSize(11)
           .BackgroundColor(Microsoft.Maui.Graphics.Colors.IndianRed)
           .OnClicked(() => ((App) MauiControls.Application.Current!).ResetToMainPage());

    private static ScaffoldRoot MakeRoot<TComponent>(string title, string glyph)
        where TComponent : Component
        => new()
        {
            Title = title,
            PageType = typeof(TComponent),
            Icon = new MauiControls.FontImageSource
            {
                FontFamily = "Material",
                Glyph = glyph,
                Color = Microsoft.Maui.Graphics.Color.FromArgb("#8A8A8E"),
                Size = 24
            }
        };
}
