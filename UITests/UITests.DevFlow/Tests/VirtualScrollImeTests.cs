using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Android soft-keyboard behavior with entries INSIDE virtualized items, against the
/// "Virtual Scroll IME Tests" harness. Android-only: keyboard state is read host-side via adb,
/// and <c>HideSoftInputOnTapped</c> requires a REAL input tap (agent taps never travel the
/// platform input pipeline — see <see cref="NaluApp.AndroidRealTapAsync"/>).
/// </summary>
public class VirtualScrollImeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Virtual Scroll IME Tests";

    private async Task<bool> IsAndroidAsync()
        => (await App.GetPlatformAsync()).Contains("android", StringComparison.OrdinalIgnoreCase);

    public async ValueTask InitializeAsync()
    {
        Assert.SkipWhen(!await IsAndroidAsync(), "Android-only: soft-keyboard state is asserted via adb.");

        await App.OpenTestPageAsync(_pageName);
        await App.TapAsync("OpenTestPage");
        await App.WaitForElementAsync("ImeItemEntry1");
    }

    public async ValueTask DisposeAsync()
    {
        if (await IsAndroidAsync())
        {
            await App.ResetAsync();
        }
    }

    /// <summary>
    /// Focus with a bounded retry: a focus request landing while the push animation is still
    /// settling can miss the IME show — re-issuing it is what a user retry would do.
    /// </summary>
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
    public async Task KeyboardHidesWhenFocusedItemIsRecycled()
    {
        await FocusEntryRaisingKeyboardAsync("ImeItemEntry2");

        // Swipe the focused item off-screen: recycling its cell must close the orphaned
        // keyboard (Android never closes the IME on detach by itself, and MAUI's
        // HideSoftInputOnTapped watcher dies with the focus — leaving it undismissable).
        for (var i = 0; i < 5 && await App.IsAndroidSoftKeyboardVisibleAsync(); i++)
        {
            await App.SwipeAsync("ImeList", "up");
        }

        await App.WaitForAndroidSoftKeyboardAsync(visible: false);
    }

    [Fact]
    public async Task HideSoftInputOnTappedWorksOverTheList()
    {
        await FocusEntryRaisingKeyboardAsync("ImeItemEntry2");

        // A real tap on the page (outside any input) must dismiss the keyboard while an
        // item entry holds focus.
        await App.AndroidRealTapAsync("ImeTapTarget");

        await App.WaitForAndroidSoftKeyboardAsync(visible: false);
    }
}
