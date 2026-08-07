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
    private const string PageName = "Scaffold Restore Tests";

    /// <summary>Longer than the service's 500ms write debounce: the snapshot is on disk after this.</summary>
    private static readonly TimeSpan _snapshotSettle = TimeSpan.FromMilliseconds(1300);

    private static readonly string[] _pageNames = ["Home", "Other", "Detail", "Forgotten", "Deep"];

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(PageName);

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

    private async Task ConvergeToHomeAsync()
    {
        var displayed = await WaitForAnyRestorePageAsync();

        if (displayed != "Home")
        {
            // A replay may still be in flight (its suppression window ignores taps): retry
            // until the convergence navigation actually lands.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

            while (DateTime.UtcNow < deadline && displayed != "Home")
            {
                await App.TapAsync($"RestoreGoHomeRoot{displayed}Button");
                await Task.Delay(350);
                displayed = await WaitForAnyRestorePageAsync();
            }
        }

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

        await Task.Delay(_snapshotSettle);
        await App.RestartAppAsync();
        await App.OpenTestPageAsync(PageName);

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

        await Task.Delay(_snapshotSettle);
        await App.RestartAppAsync();
        await App.OpenTestPageAsync(PageName);

        // Root selection is captured automatically: the app lands on the OTHER root (the
        // initial root — Home — ran first; the replay then switched).
        await App.WaitForSettledDisplayAsync("RestoreOtherPage");
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

        await Task.Delay(_snapshotSettle);
        await App.RestartAppAsync();
        await App.OpenTestPageAsync(PageName);

        // The LAST restored destination (Detail) dispatched an auto-navigation from its
        // appearing: the suppression window lifted before its replay step, so it EXECUTES.
        await App.WaitForSettledDisplayAsync("RestoreDeepPage");
        await App.WaitForTextAsync("RestoreDetailRedirectLabel", "redirect:True");

        // The INITIALIZATION root's dispatched redirect (fired from its boot appearing, the
        // classic init-flow pattern) drained inside the window: deterministically ignored.
        await App.WaitForTextAsync("RestoreHomeRedirectLabel", "redirect:False");
        (await App.FindElementAsync("RestoreOtherPage")).Should().BeNull("the initialization root's redirect must be suppressed during the replay");

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
        await Task.Delay(_snapshotSettle);
        await App.ResetAsync();

        // Restore runs ONCE PER APP LAUNCH — a later scaffold in the same process boots the
        // default destination even though a snapshot exists (a logout/login scaffold swap must
        // never resurrect old navigation).
        await App.OpenTestPageAsync(PageName);
        await App.WaitForSettledDisplayAsync("RestoreHomePage");
        (await App.FindElementAsync("RestoreDetailPage")).Should().BeNull("restore replays only at app launch, not on in-process scaffold swaps");
    }
}
