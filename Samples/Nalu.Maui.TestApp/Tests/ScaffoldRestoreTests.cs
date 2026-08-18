using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

// "Scaffold Restore Tests" — navigation-state snapshot & restore harness (engine-level
// feature, Scaffold-verified). Capture is AUTOMATIC: pages enter the snapshot with the intent
// they were navigated with; a page calls INavigationRestore.ForgetAsync() to end the
// restorable stack at itself.
//
// The kill-and-relaunch UI test flow:
//   1. open this page (the ctor ENABLES restore at runtime — before the scaffold handler
//      connects, so capture AND boot-restore are active while this harness lives),
//   2. navigate (capture is automatic),
//   3. kill + relaunch the app (host-side adb / simctl — no Dispose runs),
//   4. reopen this page: the engine boots the initial root, then replays the captured stack.
// The Exit button path DISPOSES the scaffold, which turns restore back off — other suites
// never see a restore-enabled engine.

/// <summary>Intent captured for <see cref="RestoreDetailPage"/>; round-trips through the snapshot.</summary>
public sealed record RestoreDetailIntent(string Value);

/// <summary>
/// Runtime toggle for the restore options: MauiProgram's <c>UseNaluNavigationRestore</c> parks
/// the live options instance here; the harness scaffold flips <c>Enabled</c> around its own lifetime.
/// </summary>
public static class ScaffoldRestoreTestSupport
{
    public static NavigationRestoreOptions? Options { get; set; }

    public static void Enable() => Set(true);

    public static void Disable() => Set(false);

    private static void Set(bool enabled)
    {
        if (Options is { } options)
        {
            options.Enabled = enabled;
        }
    }
}

/// <summary>
/// One-shot arming for the auto-navigation-during-replay scenario, PERSISTED so it survives
/// the kill: pages fire their auto-navigations only in a process STARTED AFTER arming (the
/// live navigations of the arming session must not be affected), and only within a short TTL
/// so an aborted test run cannot leak the behavior into other suites.
/// </summary>
public static class RestoreAutoNavSupport
{
    private const string _armedAtKey = "RestoreAutoNavArmedAt";
    private static readonly DateTime _processStartUtc = DateTime.UtcNow;

    public static void Arm() => Preferences.Default.Set(_armedAtKey, DateTime.UtcNow);

    public static void Disarm() => Preferences.Default.Remove(_armedAtKey);

    public static bool ShouldFire
    {
        get
        {
            var armedAt = Preferences.Default.Get(_armedAtKey, DateTime.MinValue);

            return armedAt != DateTime.MinValue
                   && armedAt < _processStartUtc
                   && DateTime.UtcNow - armedAt < TimeSpan.FromSeconds(90);
        }
    }
}

[UsedImplicitly]
[TestPage("Scaffold Restore Tests")]
public class RestoreScaffold : Scaffold
{
    public RestoreScaffold()
    {
        // BEFORE the handler connects (boot happens on handler connection): capture and
        // boot-restore are live while this harness is presented.
        ScaffoldRestoreTestSupport.Enable();

        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "RestHome", PageType = typeof(RestoreHomePage) },
                    new ScaffoldRoot { Title = "RestOther", PageType = typeof(RestoreOtherPage) }
                }
            }
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // The Exit path: later suites (and later app launches) run with restore off.
            // A kill skips this on purpose — that is what keeps restore armed for the relaunch.
            ScaffoldRestoreTestSupport.Disable();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// The "init root outside any area" shape: a standalone <see cref="ScaffoldRoot"/> boots the
/// app, the tab bar area (with the roots the user actually lives in) comes after. Restore must
/// land on the captured TAB root even when its stack is empty.
/// </summary>
[UsedImplicitly]
[TestPage("Scaffold Restore Standalone Tests")]
public class RestoreStandaloneScaffold : Scaffold
{
    public RestoreStandaloneScaffold()
    {
        ScaffoldRestoreTestSupport.Enable();

        Areas.Add(new ScaffoldRoot { Title = "RestHome", PageType = typeof(RestoreHomePage) });
        Areas.Add(
            new ScaffoldTabBar
            {
                Roots =
                {
                    new ScaffoldRoot { Title = "RestOther", PageType = typeof(RestoreOtherPage) },
                    new ScaffoldRoot { Title = "RestDeep", PageType = typeof(RestoreDeepPage) }
                }
            }
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ScaffoldRestoreTestSupport.Disable();
        }

        base.Dispose(disposing);
    }
}

internal static class RestorePageFactory
{
    public static Button MakeButton(string text, string automationId, Func<Task> action)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
        button.Clicked += async (_, _) =>
        {
            try
            {
                await action();
            }
            catch
            {
                // Harness buttons: failures surface through the UI test's element assertions.
            }
        };

        return button;
    }

    public static View BuildContent(string name, INavigationService navigationService, params View[] extraViews)
    {
        var stack = new VerticalStackLayout { Spacing = 6, Padding = 16 };

        stack.Add(new Label { Text = $"Restore {name}", AutomationId = $"Restore{name}Page", FontSize = 22, FontAttributes = FontAttributes.Bold });

        var exitButton = new Button { Text = "Exit", AutomationId = $"ExitRestore{name}", FontSize = 11, BackgroundColor = Colors.IndianRed };
        exitButton.Clicked += (_, _) => ((App) Application.Current!).ResetToMainPage();
        stack.Add(exitButton);

        // Baseline convergence for the UI tests: from ANY page back to the home root with an
        // empty stack (per-page AutomationId — covered scaffold pages stay in the tree, so a
        // shared id would be ambiguous).
        stack.Add(
            MakeButton(
                "Go Home root",
                $"RestoreGoHomeRoot{name}Button",
                () => navigationService.GoToAsync(Nav.Absolute().Root<RestoreHomePage>())
            )
        );

        foreach (var view in extraViews)
        {
            stack.Add(view);
        }

        return new ScrollView { Content = stack };
    }
}

/// <summary>
/// Root of the first tab: the startup destination — the replay runs after its first
/// appearing. When the auto-nav scenario is armed, this INITIALIZATION root dispatches a
/// redirect from its appearing (the classic init-flow pattern): with a restore pending it
/// must be deterministically IGNORED (the dispatched redirect drains before the next replay
/// step, inside the suppression window).
/// </summary>
[UsedImplicitly]
public class RestoreHomePage : ContentPage, IAppearingAware
{
    private readonly INavigationService _navigationService;
    private readonly Label _redirectLabel;

    public RestoreHomePage(INavigationService navigationService)
    {
        _navigationService = navigationService;
        Title = "Restore Home";

        _redirectLabel = new Label { Text = "redirect:none", AutomationId = "RestoreHomeRedirectLabel", FontSize = 11 };

        Content = RestorePageFactory.BuildContent(
            "Home",
            navigationService,
            _redirectLabel,
            RestorePageFactory.MakeButton(
                "Push Detail (intent ctx-42)",
                "RestorePushDetailButton",
                () => navigationService.GoToAsync(Nav.Push<RestoreDetailPage>(new RestoreDetailIntent("ctx-42")))
            ),
            RestorePageFactory.MakeButton(
                "Go Other root",
                "RestoreGoOtherButton",
                () => navigationService.GoToAsync(Nav.Absolute().Root<RestoreOtherPage>())
            ),
            RestorePageFactory.MakeButton(
                "Arm auto-nav",
                "RestoreArmAutoNavButton",
                () =>
                {
                    RestoreAutoNavSupport.Arm();

                    return Task.CompletedTask;
                }
            )
        );
    }

    public ValueTask OnAppearingAsync()
    {
        if (RestoreAutoNavSupport.ShouldFire)
        {
            // The prescribed navigate-from-lifecycle pattern: dispatched, not inline.
            _ = Dispatcher.DispatchAsync(async () =>
                {
                    try
                    {
                        var executed = await _navigationService.GoToAsync(Nav.Absolute().Root<RestoreOtherPage>());
                        _redirectLabel.Text = $"redirect:{executed}";
                    }
                    catch (Exception ex)
                    {
                        _redirectLabel.Text = $"redirect:{ex.GetType().Name}";
                    }
                }
            );
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>Second root: restoring the last-used root selection lands here (captured automatically).</summary>
[UsedImplicitly]
public class RestoreOtherPage : ContentPage
{
    public RestoreOtherPage(INavigationService navigationService)
    {
        Title = "Restore Other";

        Content = RestorePageFactory.BuildContent("Other", navigationService);
    }
}

/// <summary>
/// Captured automatically WITH its intent: nothing to do — restore delivers the same intent
/// through the normal pipeline. When the auto-nav scenario is armed, this page — the LAST
/// restored destination — dispatches an auto-navigation from its appearing: the suppression
/// window lifted just before its replay navigation, so this one must EXECUTE.
/// </summary>
[UsedImplicitly]
public class RestoreDetailPage : ContentPage, IEnteringAware<RestoreDetailIntent>, IAppearingAware
{
    private readonly INavigationService _navigationService;
    private readonly Label _intentLabel;
    private readonly Label _redirectLabel;

    public RestoreDetailPage(INavigationService navigationService)
    {
        _navigationService = navigationService;
        Title = "Restore Detail";

        _intentLabel = new Label { Text = "(none)", AutomationId = "RestoreDetailIntentLabel", FontSize = 11 };
        _redirectLabel = new Label { Text = "redirect:none", AutomationId = "RestoreDetailRedirectLabel", FontSize = 11 };

        Content = RestorePageFactory.BuildContent(
            "Detail",
            navigationService,
            _intentLabel,
            _redirectLabel,
            RestorePageFactory.MakeButton(
                "Push Forgotten",
                "RestorePushForgottenButton",
                () => navigationService.GoToAsync(Nav.Push<RestoreForgottenPage>())
            )
        );
    }

    public ValueTask OnEnteringAsync(RestoreDetailIntent intent)
    {
        _intentLabel.Text = intent.Value;

        return ValueTask.CompletedTask;
    }

    public ValueTask OnAppearingAsync()
    {
        if (RestoreAutoNavSupport.ShouldFire)
        {
            // One-shot: this is the scenario's last consumer.
            RestoreAutoNavSupport.Disarm();

            _ = Dispatcher.DispatchAsync(async () =>
                {
                    try
                    {
                        var executed = await _navigationService.GoToAsync(Nav.Push<RestoreDeepPage>());
                        _redirectLabel.Text = $"redirect:{executed}";
                    }
                    catch (Exception ex)
                    {
                        _redirectLabel.Text = $"redirect:{ex.GetType().Name}";
                    }
                }
            );
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Calls <see cref="INavigationRestore.ForgetAsync"/> in its entering (the wizard-page
/// pattern): the restorable stack ends here — this page and anything above it never resurrect.
/// </summary>
[UsedImplicitly]
public class RestoreForgottenPage : ContentPage, IEnteringAware
{
    private readonly INavigationRestore _restore;

    public RestoreForgottenPage(INavigationService navigationService, INavigationRestore restore)
    {
        _restore = restore;
        Title = "Restore Forgotten";

        Content = RestorePageFactory.BuildContent(
            "Forgotten",
            navigationService,
            RestorePageFactory.MakeButton(
                "Push Deep",
                "RestorePushDeepButton",
                () => navigationService.GoToAsync(Nav.Push<RestoreDeepPage>())
            )
        );
    }

    public ValueTask OnEnteringAsync() => new(_restore.ForgetAsync());
}

/// <summary>
/// Restorable by itself (no intent), but pushed ABOVE the forgotten page: must NOT resurrect —
/// the restorable stack ended below it.
/// </summary>
[UsedImplicitly]
public class RestoreDeepPage : ContentPage
{
    public RestoreDeepPage(INavigationService navigationService)
    {
        Title = "Restore Deep";

        Content = RestorePageFactory.BuildContent("Deep", navigationService);
    }
}
