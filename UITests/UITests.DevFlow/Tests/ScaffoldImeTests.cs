using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Soft-keyboard behavior on SCAFFOLD-hosted pages (the "Scaffold IME Tests" harness):
/// <c>HideSoftInputOnTapped</c> is gated on <c>Page.HasNavigatedTo</c>, which MAUI's own hosts
/// raise via internal navigation events — the Scaffold must raise them too
/// (ScaffoldPageNavigationEvents), or the feature silently dies on every scaffold page.
/// </summary>
/// <remarks>
/// Runs on BOTH platforms. The two Android-specific pieces have platform-agnostic wrappers:
/// keyboard state comes from <c>dumpsys</c> on Android and from the harness's in-app
/// <c>SoftKeyboardProbe</c> on Apple platforms, and the REAL input taps the feature requires (agent
/// taps run in-process and never reach the platform input pipeline) are injected with <c>adb</c>
/// and <c>axe</c> respectively. Both presenters implement the navigation events under test
/// separately, so skipping iOS left its half unguarded.
/// </remarks>
public class ScaffoldImeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold IME Tests";

    /// <summary>Keyboard probe of the page a test is currently observing (Apple platforms).</summary>
    private const string _homeKeyboardProbe = "ScaffoldImeKeyboardHome";

    private const string _entriesKeyboardProbe = "ScaffoldImeKeyboardEntries";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForBoundsAsync("ScaffoldImeHome", b => b.Y > 0);
    }

    private async Task PushEntriesPageAsync()
    {
        await App.TapAsync("PushScaffoldIme");
        await App.WaitForBoundsAsync("ScaffoldImeItemEntry1", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private async Task FocusEntryRaisingKeyboardAsync(string automationId, string keyboardProbe)
    {
        for (var attempt = 0; ; attempt++)
        {
            await App.FocusAsync(automationId);

            try
            {
                await App.WaitForSoftKeyboardAsync(visible: true, keyboardProbe, TimeSpan.FromSeconds(3));

                return;
            }
            catch (TimeoutException) when (attempt < 2)
            {
            }
        }
    }

    [Fact]
    public async Task HideSoftInputOnTappedWorksOnTheScaffoldInitialPage()
    {
        // The ROOT page presented by the scaffold on startup must receive NavigatedTo too:
        // no push involved, HideSoftInputOnTapped must already be armed.
        await FocusEntryRaisingKeyboardAsync("ScaffoldImeHomeEntry", _homeKeyboardProbe);

        await App.RealTapAsync("ScaffoldImeHomeTapTarget");

        await App.WaitForSoftKeyboardAsync(visible: false, _homeKeyboardProbe);
    }

    [Fact]
    public async Task KeyboardHidesWhenNavigatingAway()
    {
        // A PLAIN entry (not virtualized): Android never dismisses the IME when the focused
        // hierarchy is torn down, so a push must hide it explicitly or it orphans over the
        // incoming page. (The programmatic push does not travel the tap-to-hide path.)
        await FocusEntryRaisingKeyboardAsync("ScaffoldImeHomeEntry", _homeKeyboardProbe);

        await PushEntriesPageAsync();

        await App.WaitForSoftKeyboardAsync(visible: false, _entriesKeyboardProbe);
    }

    [Fact]
    public async Task HideSoftInputOnTappedWorksOnScaffoldPushedPages()
    {
        await PushEntriesPageAsync();
        await FocusEntryRaisingKeyboardAsync("ScaffoldImeItemEntry2", _entriesKeyboardProbe);

        // A real tap on the page must dismiss the keyboard: this only works when the pushed
        // page received NavigatedTo from the scaffold host.
        await App.RealTapAsync("ScaffoldImeTapTarget");

        await App.WaitForSoftKeyboardAsync(visible: false, _entriesKeyboardProbe);
    }

    /// <summary>
    /// Android-only for now, and NOT because of tooling: the behavior itself differs. On iOS the
    /// focused cell survives scrolling far out of view — verified by hand at 490dp above the
    /// viewport, still attached, still first responder, keyboard still up — so recycling never
    /// dismisses it. That is a VirtualScroll-on-iOS question (the same harness shape as
    /// VirtualScrollImeTests, also Android-only) rather than a Scaffold one; asserting the current
    /// iOS behavior here would enshrine it.
    /// </summary>
    [Fact]
    public async Task KeyboardHidesWhenFocusedItemIsRecycledInScaffold()
    {
        Assert.SkipWhen(await App.IsAppleAsync(), "iOS keeps the focused cell alive while scrolling: recycling does not dismiss the keyboard there.");

        await PushEntriesPageAsync();
        await FocusEntryRaisingKeyboardAsync("ScaffoldImeItemEntry2", _entriesKeyboardProbe);

        for (var i = 0; i < 5 && await App.IsSoftKeyboardVisibleAsync(_entriesKeyboardProbe); i++)
        {
            await App.SwipeAsync("ScaffoldImeList", "up");
        }

        await App.WaitForSoftKeyboardAsync(visible: false, _entriesKeyboardProbe);
    }
}
