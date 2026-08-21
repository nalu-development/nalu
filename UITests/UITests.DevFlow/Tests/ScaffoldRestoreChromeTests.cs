using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers navigation-state snapshot &amp; restore (engine-level, Scaffold-verified) against the
/// "Scaffold Restore Tests" harness with a REAL kill-and-relaunch
/// (<see cref="NaluApp.RestartAppAsync"/>): the app must land exactly where it was — root
/// selection, captured stack, replayed intents — with a ForgetAsync page ending the restorable
/// stack, and the initial root always running before the replay.
/// </summary>
public class ScaffoldRestoreChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Restore Tests";

    /// <summary>Longer than the service's 500ms write debounce: the snapshot is on disk after this.</summary>
    private static readonly TimeSpan _snapshotSettle = TimeSpan.FromMilliseconds(1300);

    private static readonly string[] _pageNames = ["Home", "Other", "Detail", "Forgotten", "Deep"];

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);

        // A stale snapshot from an earlier (aborted) run may have replayed on open: converge
        // to the baseline — home root, empty stack — before the scenario starts.
        await ConvergeToHomeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Leave the persisted snapshot at the harmless baseline for the NEXT run, then exit
        // (the Exit path disposes the harness scaffold, turning restore off for other suites).
        try
        {
            await ConvergeToHomeAsync();
            await Task.Delay(_snapshotSettle);
        }
        finally
        {
            await App.ResetAsync();
        }
    }

    private async Task<bool> IsDisplayedAsync(string automationId)
        => await App.FindElementAsync(automationId) is { } element
           && (element.WindowBounds ?? element.Bounds) is { Y: > 0 };

    private async Task<string> WaitForAnyRestorePageAsync()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (DateTime.UtcNow < deadline)
        {
            foreach (var name in _pageNames)
            {
                if (await IsDisplayedAsync($"Restore{name}Page"))
                {
                    return name;
                }
            }

            await Task.Delay(250);
        }

        throw new TimeoutException("No restore harness page became displayed.");
    }

    /// <summary>
    /// Brings the harness to its baseline — home root, empty stack — and does not return until it
    /// STAYS there. Arriving is not converging: this suite persists a snapshot and replays it on
    /// open, so a replay still in flight navigates away from a Home the convergence just reached,
    /// a few hundred milliseconds later. Every flake in this class was that: the tap landed, Home
    /// appeared, the settle re-check found it gone.
    /// A replay's suppression window also ignores taps outright, so the tap itself is retried.
    /// </summary>
    private async Task ConvergeToHomeAsync()
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);

        while (DateTime.UtcNow < deadline)
        {
            var displayed = await WaitForAnyRestorePageAsync();

            if (displayed != "Home")
            {
                await App.TapAsync($"RestoreGoHomeRoot{displayed}Button");
                await Task.Delay(350);

                continue;
            }

            // Home, and still Home once anything already in flight would have landed.
            await Task.Delay(400);

            if (await IsDisplayedAsync("RestoreHomePage"))
            {
                return;
            }
        }

        // Out of budget: fail with the bounds, which say what the app settled on instead.
        await App.WaitForSettledDisplayAsync("RestoreHomePage");
    }

    [Fact]
    public async Task KillAndRelaunchRestoresCapturedStackAndReplaysIntent()
    {
        // Build the stack: Home → Detail(intent ctx-42, captured automatically) →
        // Forgotten(ForgetAsync in entering) → Deep.
        await App.TapAsync("RestorePushDetailButton");
        await App.WaitForSettledDisplayAsync("RestoreDetailPage");
        await App.WaitForTextAsync("RestoreDetailIntentLabel", "ctx-42");

        await App.TapAsync("RestorePushForgottenButton");
        await App.WaitForSettledDisplayAsync("RestoreForgottenPage");
        await App.TapAsync("RestorePushDeepButton");
        await App.WaitForSettledDisplayAsync("RestoreDeepPage");

        await Task.Delay(_snapshotSettle, TestContext.Current.CancellationToken);
        await App.RestartAppAsync();
        await App.OpenTestPageAsync(_pageName);

        // The restorable stack restores: Home → Detail with its intent replayed through the
        // normal pipeline. The forgotten page ended the restorable stack, so neither it nor
        // the page above it may resurrect.
        await App.WaitForSettledDisplayAsync("RestoreDetailPage");
        await App.WaitForTextAsync("RestoreDetailIntentLabel", "ctx-42");

        (await App.FindElementAsync("RestoreForgottenPage")).Should().BeNull("ForgetAsync pages never enter the snapshot");
        (await App.FindElementAsync("RestoreDeepPage")).Should().BeNull("the restorable stack ended at the forgotten page below");

        // The restored stack is real engine state: popping to the home root works normally.
        await App.TapAsync("RestoreGoHomeRootDetailButton");
        await App.WaitForSettledDisplayAsync("RestoreHomePage");
    }

    [Fact]
    public async Task KillAndRelaunchRestoresRootSelection()
    {
        await App.TapAsync("RestoreGoOtherButton");
        await App.WaitForSettledDisplayAsync("RestoreOtherPage");

        await Task.Delay(_snapshotSettle, TestContext.Current.CancellationToken);
        await App.RestartAppAsync();
        await App.OpenTestPageAsync(_pageName);

        // Root selection is captured automatically: the app lands on the OTHER root (the
        // initial root — Home — ran first; the replay then switched).
        await App.WaitForSettledDisplayAsync("RestoreOtherPage");
    }

    /// <summary>
    /// Init root OUTSIDE any area + a tab bar area: the captured tab root (empty stack) must be
    /// restored — the boot runs the standalone init root first, then the replay switches area.
    /// </summary>
    [Fact]
    public async Task KillAndRelaunchRestoresATabRootBehindAStandaloneInitRoot()
    {
        const string standaloneHarness = "Scaffold Restore Standalone Tests";

        // Leave the default harness (its Dispose turns restore off; the standalone one turns it on).
        await App.ResetAsync();
        await App.OpenTestPageAsync(standaloneHarness);
        await App.WaitForSettledDisplayAsync("RestoreHomePage");

        await App.TapAsync("RestoreGoOtherButton");
        await App.WaitForSettledDisplayAsync("RestoreOtherPage");

        await Task.Delay(_snapshotSettle, TestContext.Current.CancellationToken);
        await App.RestartAppAsync();
        await App.OpenTestPageAsync(standaloneHarness);

        await App.WaitForSettledDisplayAsync("RestoreOtherPage");

        // Back to the default harness so DisposeAsync's convergence finds its pages.
        await App.ResetAsync();
        await App.OpenTestPageAsync(_pageName);
    }

    /// <summary>
    /// A ONE-step restore (a root with an empty stack) while the initialization root dispatches
    /// its own redirect from its boot appearing: the redirect is queued BEFORE the single replay
    /// step, so lifting the suppression window ahead of that step let it win — the app landed
    /// wherever the init root sent it instead of on the restored root. The window must lift
    /// inside the replay step: the init redirect drains ignored, the restored root lands.
    /// </summary>
    [Fact]
    public async Task InitRootRedirectDoesNotBeatAOneStepRestore()
    {
        await App.TapAsync("RestoreArmAutoNavButton");
        await App.TapAsync("RestoreGoOtherButton");
        await App.WaitForSettledDisplayAsync("RestoreOtherPage");

        await Task.Delay(_snapshotSettle, TestContext.Current.CancellationToken);
        await App.RestartAppAsync();
        await App.OpenTestPageAsync(_pageName);

        // The restored root lands…
        await App.WaitForSettledDisplayAsync("RestoreOtherPage");

        // …and the initialization root's dispatched redirect was IGNORED (it reports its result
        // on its own page, still in the tree as the Home root).
        await App.WaitForTextAsync("RestoreHomeRedirectLabel", "redirect:False");
    }

    [Fact]
    public async Task AutoNavigationsDuringReplayAreSuppressedExceptOnTheFinalDestination()
    {
        // Arm the auto-nav scenario (it fires only in a process started AFTER arming), then
        // build the restorable stack: Home → Detail(intent ctx-42).
        await App.TapAsync("RestoreArmAutoNavButton");
        await App.TapAsync("RestorePushDetailButton");
        await App.WaitForSettledDisplayAsync("RestoreDetailPage");
        await App.WaitForTextAsync("RestoreDetailIntentLabel", "ctx-42");

        await Task.Delay(_snapshotSettle, TestContext.Current.CancellationToken);
        await App.RestartAppAsync();
        await App.OpenTestPageAsync(_pageName);

        // The LAST restored destination (Detail) dispatched an auto-navigation from its
        // appearing: the suppression window lifted before its replay step, so it EXECUTES.
        await App.WaitForSettledDisplayAsync("RestoreDeepPage");
        await App.WaitForTextAsync("RestoreDetailRedirectLabel", "redirect:True");

        // The INITIALIZATION root's dispatched redirect (fired from its boot appearing, the
        // classic init-flow pattern) drained inside the window: deterministically ignored.
        // (The Other root page may still sit in the element tree from an earlier visit in this
        // app session — roots stay alive across selections — so the label is the proof, not
        // the page's absence.)
        await App.WaitForTextAsync("RestoreHomeRedirectLabel", "redirect:False");

        await App.TapAsync("RestoreGoHomeRootDeepButton");
        await App.WaitForSettledDisplayAsync("RestoreHomePage");
    }

    [Fact]
    public async Task InProcessResetDoesNotRestoreOnReopen()
    {
        // Navigate somewhere restorable, persist, then leave through the EXIT path (in-process
        // reset: the harness scaffold is disposed, the app keeps running).
        await App.TapAsync("RestorePushDetailButton");
        await App.WaitForSettledDisplayAsync("RestoreDetailPage");
        await Task.Delay(_snapshotSettle, TestContext.Current.CancellationToken);
        await App.ResetAsync();

        // Restore runs ONCE PER APP LAUNCH — a later scaffold in the same process boots the
        // default destination even though a snapshot exists (a logout/login scaffold swap must
        // never resurrect old navigation).
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForSettledDisplayAsync("RestoreHomePage");
        (await App.FindElementAsync("RestoreDetailPage")).Should().BeNull("restore replays only at app launch, not on in-process scaffold swaps");
    }
}
