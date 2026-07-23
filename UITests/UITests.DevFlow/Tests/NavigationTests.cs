using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// DevFlow tests for Nalu.Maui.Navigation against the NavShell harness.
/// Shell structure: ShellItem 1 = tabs HomeTab (Home) + SearchTab (Search);
/// ShellItem 2 = Settings. Editor is a guarded page (ILeavingGuard), Detail receives intents.
/// Lifecycle log format (see the TestApp's NavigationTests.cs): +E entering, +A appearing,
/// -D disappearing, -L leaving, -X disposed, ?G{y|n} guard, :I intent, :R= awaitable result.
/// </summary>
/// <remarks>
/// The shell harness has no harness ResetButton (Shell pages are not decorated):
/// every page exposes an Exit{Name} button invoking the app-level reset instead.
/// </remarks>
public class NavigationTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Navigation Tests";

    private async Task OpenShellAsync()
    {
        // NaluApp.ResetAsync (via OpenTestPageAsync) leaves a previously opened shell
        // through the visible "Exit" button (Shell pages have no decorated ResetButton).
        await App.OpenTestPageAsync(PageName);
        await App.WaitForElementAsync("NavPageHome");

        // Wait for the initial navigation to fully settle before the test starts tapping.
        await WaitForLogAsync("Home", "Home+A");
    }

    private async Task<string> LogAsync(string page)
        => (await App.WaitForElementAsync($"Log{page}")).Text ?? "";

    /// <summary>
    /// Waits for a lifecycle entry to show up in a page's log label.
    /// Nalu silently ignores navigations requested while another one is in flight,
    /// so tests MUST wait for the target page's "+A" (appearing, sent after the
    /// navigation commits) before triggering the next navigation.
    /// </summary>
    private Task WaitForLogAsync(string page, string entry)
        => App.WaitForTextMatchAsync($"Log{page}", t => t?.Contains(entry) == true);

    private static void AssertOrdered(string log, params string[] entries)
    {
        var lastIndex = -1;

        foreach (var entry in entries)
        {
            var index = log.IndexOf(entry, lastIndex + 1, StringComparison.Ordinal);
            index.Should().BeGreaterThan(lastIndex, $"'{entry}' must appear (in order) in log '{log}'");
            lastIndex = index;
        }
    }

    [Fact]
    public async Task RootPageRunsEnteringThenAppearing()
    {
        await OpenShellAsync();

        await App.WaitForTextAsync("LogHome", "Home+E,Home+A");
    }

    [Fact]
    public async Task PushAndPopRunFullLifecycle()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await App.WaitForElementAsync("NavPageDetail");
        await App.WaitForTextAsync("LogDetail", "Home+E,Home+A,Home-D,Detail+E,Detail+A");

        await App.TapAsync("PopDetailButton");
        await App.WaitForElementAsync("NavPageHome");

        // Disposal happens after the pop animation completes.
        await App.WaitForTextAsync("LogHome", "Home+E,Home+A,Home-D,Detail+E,Detail+A,Detail-D,Detail-L,Home+A,Detail-X");
    }

    [Fact]
    public async Task PushWithIntentDeliversItBeforeAppearing()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailIntentButton");

        await App.WaitForTextAsync("DetailIntentLabel", "42");
        await App.WaitForTextMatchAsync("LogDetail", t => t?.Contains("Detail+A") == true);
        AssertOrdered(await LogAsync("Detail"), "Detail:I42", "Detail+A");
    }

    [Fact]
    public async Task AwaitableIntentReturnsResultToCaller()
    {
        await OpenShellAsync();

        await App.TapAsync("ResolvePickButton");
        await App.WaitForTextAsync("DetailIntentLabel", "pick");
        await WaitForLogAsync("Detail", "Detail+A");

        await App.TapAsync("SetResultButton");
        await App.WaitForElementAsync("NavPageHome");

        await App.WaitForTextAsync("ResolvedLabel", "picked");
        (await LogAsync("Home")).Should().Contain("Home:R=picked");
    }

    [Fact]
    public async Task ReplaceSwapsTopPage()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");

        // Pop().Push<Editor>() — Detail leaves and is disposed, Editor takes its place.
        await App.TapAsync("ReplaceWithEditorButton");
        await App.WaitForElementAsync("NavPageEditor");

        await App.WaitForTextMatchAsync("LogEditor", t => t?.Contains("Detail-X") == true);
        AssertOrdered(await LogAsync("Editor"), "Detail-L", "Editor+E", "Editor+A");
    }

    [Fact]
    public async Task LeavingGuardBlocksPop()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");
        await App.TapAsync("PushEditorButton");
        await WaitForLogAsync("Editor", "Editor+A");

        await App.TapAsync("PopEditorButton");

        // The guard denies: still on Editor, guard was evaluated, Editor never left.
        await App.WaitForTextMatchAsync("LogEditor", t => t?.Contains("Editor?Gn") == true);
        (await App.WaitForElementAsync("NavPageEditor")).IsVisible.Should().BeTrue();
        (await LogAsync("Editor")).Should().NotContain("Editor-L");
    }

    [Fact]
    public async Task LeavingGuardAllowsPopWhenSatisfied()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");
        await App.TapAsync("PushEditorButton");
        await WaitForLogAsync("Editor", "Editor+A");

        await App.TapAsync("ToggleGuardButton");
        await App.WaitForTextAsync("GuardStateLabel", "CanLeave: True");

        await App.TapAsync("PopEditorButton");
        await App.WaitForElementAsync("NavPageDetail");

        await App.WaitForTextMatchAsync("LogDetail", t => t?.Contains("Editor-X") == true);
        AssertOrdered(await LogAsync("Detail"), "Editor?Gy", "Editor-L", "Editor-X");
    }

    [Fact]
    public async Task IgnoreGuardsBehaviorBypassesGuard()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");
        await App.TapAsync("PushEditorButton");
        await WaitForLogAsync("Editor", "Editor+A");

        await App.TapAsync("PopIgnoreGuardsButton");
        await App.WaitForElementAsync("NavPageDetail");
        await WaitForLogAsync("Detail", "Editor-X");

        var log = await LogAsync("Detail");
        log.Should().NotContain("Editor?G", "IgnoreGuards must skip the guard entirely");
        AssertOrdered(log, "Editor-L");
    }

    [Fact]
    public async Task AbsoluteRootSwitchesTabPreservingStack()
    {
        await OpenShellAsync();

        await App.TapAsync("GoSearchRootButton");
        await App.WaitForElementAsync("NavPageSearch");

        await App.WaitForTextMatchAsync("LogSearch", t => t?.Contains("Search+A") == true);
        var log = await LogAsync("Search");
        AssertOrdered(log, "Home-D", "Search+E", "Search+A");

        // Same ShellItem, different section: Home's stack is preserved, not disposed.
        log.Should().NotContain("Home-L").And.NotContain("Home-X");

        // Returning shows Home again without re-creating it.
        await App.TapAsync("GoHomeRootButton");
        await App.WaitForElementAsync("NavPageHome");

        // Home+A must appear again AFTER Search-D (the first Home+A is from startup).
        await App.WaitForTextMatchAsync(
            "LogHome",
            t => t is not null && t.Contains("Search-D") && t.LastIndexOf("Home+A", StringComparison.Ordinal) > t.IndexOf("Search-D", StringComparison.Ordinal)
        );

        var backLog = await LogAsync("Home");
        AssertOrdered(backLog, "Search-D", "Home+A");
        backLog.Split("Home+E").Length.Should().Be(2, "Home must not be re-created on tab return");
    }

    [Fact]
    public async Task ReplaceGuardedPageIgnoringGuards()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");
        await App.TapAsync("PushEditorButton");
        await WaitForLogAsync("Editor", "Editor+A");

        // Relative(IgnoreGuards).Pop().Push<Detail>() — replace the guarded page without
        // evaluating its guard (same pattern as the sample app's "Replace Six").
        await App.TapAsync("ReplaceIgnoreGuardsButton");
        await App.WaitForElementAsync("NavPageDetail");

        await WaitForLogAsync("Detail", "Editor-X");
        var log = await LogAsync("Detail");
        log.Should().NotContain("Editor?G", "IgnoreGuards must skip the guard entirely");
        AssertOrdered(log, "Editor-L", "Detail+E", "Detail+A", "Editor-X");
    }

    [Fact]
    public async Task AbsoluteRootIgnoringGuardsPopsGuardedStack()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");
        await App.TapAsync("PushEditorButton");
        await WaitForLogAsync("Editor", "Editor+A");

        // Absolute(IgnoreGuards).Root<Home>() — pop the whole stack, guard included,
        // without evaluating it (same pattern as the sample app's "Pop to One").
        await App.TapAsync("GoHomeRootIgnoreGuardsButton");
        await App.WaitForElementAsync("NavPageHome");

        await WaitForLogAsync("Home", "Detail-X");
        var log = await LogAsync("Home");
        log.Should().NotContain("Editor?G", "IgnoreGuards must skip the guard entirely");
        AssertOrdered(log, "Editor-L", "Detail-L", "Home+A");
        log.Should().Contain("Editor-X").And.Contain("Detail-X");
    }

    [Fact]
    public async Task CrossItemRootWithAddBuildsStackOnOtherItem()
    {
        await OpenShellAsync();

        await App.TapAsync("GoSettingsRootButton");
        await WaitForLogAsync("Settings", "Home-X");

        // From the Settings ShellItem: Absolute().Root<Home>().Add<Detail>() re-creates Home
        // on the other item and pushes Detail on it in a single navigation.
        await App.TapAsync("GoHomeAddDetailButton");
        await App.WaitForElementAsync("NavPageDetail");

        await WaitForLogAsync("Detail", "Detail+A");
        AssertOrdered(await LogAsync("Detail"), "Settings-L", "Home+E", "Detail+E", "Detail+A");
    }

    [Fact]
    public async Task AbsoluteRootToTabWithStackPopsToItsRoot()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");

        await App.TapAsync("GoSearchRootButton");
        await WaitForLogAsync("Search", "Search+A");

        // Absolute().Root<Home>() explicitly targets Home's ROOT: the Detail page preserved
        // on the Home tab must be popped and disposed on return. (Regression: this used to
        // crash with "Unable to find page instance for specified route" because the popped
        // segment was left in the committed route.)
        await App.TapAsync("GoHomeRootButton");
        await App.WaitForElementAsync("NavPageHome");
        await WaitForLogAsync("Home", "Detail-X");

        AssertOrdered(await LogAsync("Home"), "Detail-L", "Home+A", "Detail-X");
    }

    [Fact]
    public async Task NativeTabSwitchPreservesPushedPages()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");

        // Tab taps hit the NaluTabBar buttons (real MAUI views): they navigate via
        // Shell.GoToAsync, which Nalu's OnNavigating cancels and replays through the
        // navigation service — the same pipeline a native tab tap goes through.
        await App.TapByTextAsync("SearchTab");
        await App.WaitForElementAsync("NavPageSearch");
        await WaitForLogAsync("Search", "Search+A");

        await App.TapByTextAsync("HomeTab");
        await App.WaitForElementAsync("NavPageDetail");

        // Detail+A must appear again AFTER the search tab visit.
        await App.WaitForTextMatchAsync(
            "LogDetail",
            t => t is not null && t.Contains("Search-D") && t.LastIndexOf("Detail+A", StringComparison.Ordinal) > t.IndexOf("Search-D", StringComparison.Ordinal)
        );

        (await LogAsync("Detail")).Should().NotContain("Detail-X", "the HomeTab stack must survive the tab switch");
    }

    [Fact]
    public async Task AbsoluteRootWithAddPushesOnTarget()
    {
        await OpenShellAsync();

        await App.TapAsync("GoSearchAddEditorButton");
        await App.WaitForElementAsync("NavPageEditor");

        await App.WaitForTextMatchAsync("LogEditor", t => t?.Contains("Editor+A") == true);
        AssertOrdered(await LogAsync("Editor"), "Search+E", "Editor+E", "Editor+A");

        // Leave cleanly for the next test (the guard blocks by default).
        await App.TapAsync("PopIgnoreGuardsButton");
        await WaitForLogAsync("Search", "Editor-X");
    }

    [Fact]
    public async Task ShellItemSwitchClearsAllStacks()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");

        // Navigating to a different ShellItem pops and disposes the whole current stack.
        await App.TapAsync("GoSettingsFromDetailButton");
        await App.WaitForElementAsync("NavPageSettings");

        await App.WaitForTextMatchAsync("LogSettings", t => t?.Contains("Detail-X") == true && t.Contains("Home-X"));
        var settingsLog = await LogAsync("Settings");
        settingsLog.Should().Contain("Detail-L").And.Contain("Home-L").And.Contain("Settings+A");

        // Going back re-creates Home from scratch (a second Home+E).
        await App.TapAsync("GoHomeFromSettingsButton");
        await App.WaitForElementAsync("NavPageHome");
        await App.WaitForTextMatchAsync("LogHome", t => t?.Split("Home+E").Length >= 3);
        var log = await LogAsync("Home");
        log.Split("Home+E").Length.Should().BeGreaterThanOrEqualTo(3, "Home must be re-created after the item switch cleared it");
    }

    [Fact]
    public async Task GuardBlocksShellItemSwitch()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");
        await App.TapAsync("PushEditorButton");
        await WaitForLogAsync("Editor", "Editor+A");

        // The guarded page vetoes clearing the stack for the item switch.
        await App.TapAsync("GoSettingsFromEditorButton");

        await App.WaitForTextMatchAsync("LogEditor", t => t?.Contains("Editor?Gn") == true);
        (await App.WaitForElementAsync("NavPageEditor")).IsVisible.Should().BeTrue();
        (await LogAsync("Editor")).Should().NotContain("Editor-X");
    }

    [Fact]
    public async Task NativeBackButtonPopsWithLifecycle()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");

        await App.BackAsync();
        await App.WaitForElementAsync("NavPageHome");

        await App.WaitForTextMatchAsync("LogHome", t => t?.Contains("Detail-X") == true);
        AssertOrdered(await LogAsync("Home"), "Detail-D", "Detail-L", "Home+A", "Detail-X");
    }

    [Fact]
    public async Task NativeBackButtonHonorsGuard()
    {
        await OpenShellAsync();

        await App.TapAsync("PushDetailButton");
        await WaitForLogAsync("Detail", "Detail+A");
        await App.TapAsync("PushEditorButton");
        await WaitForLogAsync("Editor", "Editor+A");

        await App.BackAsync();

        await App.WaitForTextMatchAsync("LogEditor", t => t?.Contains("Editor?Gn") == true);
        (await App.WaitForElementAsync("NavPageEditor")).IsVisible.Should().BeTrue();
    }
}
