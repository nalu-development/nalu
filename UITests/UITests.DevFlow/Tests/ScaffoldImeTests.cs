using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Soft-keyboard behavior on SCAFFOLD-hosted pages (the "Scaffold IME Tests" harness):
/// <c>HideSoftInputOnTapped</c> is gated on <c>Page.HasNavigatedTo</c>, which MAUI's own hosts
/// raise via internal navigation events — the Scaffold must raise them too
/// (ScaffoldPageNavigationEvents), or the feature silently dies on every scaffold page.
/// Android-only: keyboard state via adb, and the feature reacts only to REAL input taps.
/// </summary>
public class ScaffoldImeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold IME Tests";

    private async Task<bool> IsAndroidAsync()
        => (await App.GetPlatformAsync()).Contains("android", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        Assert.SkipWhen(!await IsAndroidAsync(), "Android-only: soft-keyboard state is asserted via adb.");

        await App.OpenTestPageAsync(PageName);
        await App.WaitForBoundsAsync("ScaffoldImeHome", b => b.Y > 0);
    }

    private async Task PushEntriesPageAsync()
    {
        await App.TapAsync("PushScaffoldIme");
        await App.WaitForBoundsAsync("ScaffoldImeItemEntry1", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync()
    {
        if (await IsAndroidAsync())
        {
            await App.ResetAsync();
        }
    }

    private async Task FocusEntryRaisingKeyboardAsync(string automationId)
    {
        for (var attempt = 0; ; attempt++)
        {
            await App.FocusAsync(automationId);

            try
            {
                await App.WaitForAndroidSoftKeyboardAsync(visible: true, TimeSpan.FromSeconds(3));

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
        await FocusEntryRaisingKeyboardAsync("ScaffoldImeHomeEntry");

        await App.AndroidRealTapAsync("ScaffoldImeHomeTapTarget");

        await App.WaitForAndroidSoftKeyboardAsync(visible: false);
    }

    [Fact]
    public async Task KeyboardHidesWhenNavigatingAway()
    {
        // A PLAIN entry (not virtualized): Android never dismisses the IME when the focused
        // hierarchy is torn down, so a push must hide it explicitly or it orphans over the
        // incoming page. (The programmatic push does not travel the tap-to-hide path.)
        await FocusEntryRaisingKeyboardAsync("ScaffoldImeHomeEntry");

        await PushEntriesPageAsync();

        await App.WaitForAndroidSoftKeyboardAsync(visible: false);
    }

    [Fact]
    public async Task HideSoftInputOnTappedWorksOnScaffoldPushedPages()
    {
        await PushEntriesPageAsync();
        await FocusEntryRaisingKeyboardAsync("ScaffoldImeItemEntry2");

        // A real tap on the page must dismiss the keyboard: this only works when the pushed
        // page received NavigatedTo from the scaffold host.
        await App.AndroidRealTapAsync("ScaffoldImeTapTarget");

        await App.WaitForAndroidSoftKeyboardAsync(visible: false);
    }

    [Fact]
    public async Task KeyboardHidesWhenFocusedItemIsRecycledInScaffold()
    {
        await PushEntriesPageAsync();
        await FocusEntryRaisingKeyboardAsync("ScaffoldImeItemEntry2");

        for (var i = 0; i < 5 && await App.IsAndroidSoftKeyboardVisibleAsync(); i++)
        {
            await App.SwipeAsync("ScaffoldImeList", "up");
        }

        await App.WaitForAndroidSoftKeyboardAsync(visible: false);
    }
}
