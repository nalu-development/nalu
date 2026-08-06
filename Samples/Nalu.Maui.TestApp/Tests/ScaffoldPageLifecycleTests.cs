using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

// Harness for MAUI's Page.OnAppearing / OnDisappearing on scaffold-hosted pages.
// Events are recorded in a STATIC, order-preserving log ("One+ One- Detail+ …") because a popped
// page is disposed and leaves the visual tree — its own label could not be asserted afterwards.
// Every page renders the whole log, so any live page is a valid witness.

/// <summary>Shared recorder for the lifecycle harness.</summary>
internal static class ScaffoldLifecycleLog
{
    private static readonly List<string> _entries = [];

    public static event Action? Changed;

    public static string Text => string.Join(" ", _entries);

    public static void Clear()
    {
        _entries.Clear();
        Changed?.Invoke();
    }

    public static void Appeared(string page) => Add($"{page}+");

    public static void Disappeared(string page) => Add($"{page}-");

    private static void Add(string entry)
    {
        _entries.Add(entry);
        Changed?.Invoke();
    }
}

/// <summary>
/// Base for the harness pages: records its own appearing/disappearing and renders the shared log.
/// </summary>
public abstract class ScaffoldLifecyclePageBase : ContentPage
{
    private readonly string _name;
    private readonly Label _logLabel;

    protected ScaffoldLifecyclePageBase(string name, params IView[] extraContent)
    {
        _name = name;
        Title = name;

        _logLabel = new Label
                    {
                        AutomationId = $"LifecycleLog{name}",
                        FontSize = 13,
                        Text = ScaffoldLifecycleLog.Text
                    };

        ScaffoldLifecycleLog.Changed += OnLogChanged;

        var stack = new VerticalStackLayout
                    {
                        Spacing = 6,
                        Padding = 16,
                        Children =
                        {
                            new Label
                            {
                                Text = $"Lifecycle {name}",
                                AutomationId = $"LifecyclePage{name}",
                                FontSize = 22,
                                FontAttributes = FontAttributes.Bold
                            },
                            _logLabel
                        }
                    };

        foreach (var extra in extraContent)
        {
            stack.Add((IView) extra);
        }

        var exitButton = new Button { Text = "Exit", AutomationId = $"LifecycleExit{name}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        Content = stack;
    }

    private void OnLogChanged() => _logLabel.Text = ScaffoldLifecycleLog.Text;

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        ScaffoldLifecycleLog.Appeared(_name);
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        ScaffoldLifecycleLog.Disappeared(_name);
    }
}

[UsedImplicitly]
public class ScaffoldLifecycleOnePage : ScaffoldLifecyclePageBase
{
    public ScaffoldLifecycleOnePage(INavigationService navigationService)
        : base(
            "One",
            NavPageFactory.MakeButton("Push detail", "LifecyclePushDetail", () => navigationService.GoToAsync(Nav.Push<ScaffoldLifecycleDetailPage>()))
        )
    {
    }
}

[UsedImplicitly]
public class ScaffoldLifecycleTwoPage : ScaffoldLifecyclePageBase
{
    public ScaffoldLifecycleTwoPage()
        : base("Two")
    {
    }
}

[UsedImplicitly]
public class ScaffoldLifecycleDetailPage : ScaffoldLifecyclePageBase
{
    public ScaffoldLifecycleDetailPage(INavigationService navigationService)
        : base(
            "Detail",
            NavPageFactory.MakeButton("Pop", "LifecyclePopDetail", () => navigationService.GoToAsync(Nav.Pop()))
        )
    {
    }
}

/// <summary>
/// Scaffold harness asserting that hosted pages receive MAUI's <c>Page.OnAppearing</c> /
/// <c>OnDisappearing</c> across every presentation change: initial display, push, pop and tab
/// switch.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Page Lifecycle Tests")]
public class ScaffoldLifecycleScaffold : Scaffold
{
    public ScaffoldLifecycleScaffold()
    {
        // A fresh log per harness run: the pages are recreated, the static log is not.
        ScaffoldLifecycleLog.Clear();

        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    MakeRoot<ScaffoldLifecycleOnePage>("One", "\ue88a"),
                    MakeRoot<ScaffoldLifecycleTwoPage>("Two", "\ue8b6")
                }
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
                          Color = Color.FromArgb("#8A8A8E"),
                          Size = 24
                      }
           };
}
