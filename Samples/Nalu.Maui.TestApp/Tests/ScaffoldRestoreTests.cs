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
/// Runtime toggle for the restore options: MauiProgram's <c>WithRestore</c> parks the live
/// options instance here; the harness scaffold flips <c>Enabled</c> around its own lifetime.
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

/// <summary>Root of the first tab: the startup destination — the replay runs after its first appearing.</summary>
[UsedImplicitly]
public class RestoreHomePage : ContentPage
{
    public RestoreHomePage(INavigationService navigationService)
    {
        Title = "Restore Home";

        Content = RestorePageFactory.BuildContent(
            "Home",
            navigationService,
            RestorePageFactory.MakeButton(
                "Push Detail (intent ctx-42)",
                "RestorePushDetailButton",
                () => navigationService.GoToAsync(Nav.Push<RestoreDetailPage>(new RestoreDetailIntent("ctx-42")))
            ),
            RestorePageFactory.MakeButton(
                "Go Other root",
                "RestoreGoOtherButton",
                () => navigationService.GoToAsync(Nav.Absolute().Root<RestoreOtherPage>())
            )
        );
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
/// Captured automatically WITH its intent (a registered intent type):
/// nothing to do — restore delivers the same intent through the normal pipeline.
/// </summary>
[UsedImplicitly]
public class RestoreDetailPage : ContentPage, IEnteringAware<RestoreDetailIntent>
{
    private readonly Label _intentLabel;

    public RestoreDetailPage(INavigationService navigationService)
    {
        Title = "Restore Detail";

        _intentLabel = new Label { Text = "(none)", AutomationId = "RestoreDetailIntentLabel", FontSize = 11 };

        Content = RestorePageFactory.BuildContent(
            "Detail",
            navigationService,
            _intentLabel,
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
