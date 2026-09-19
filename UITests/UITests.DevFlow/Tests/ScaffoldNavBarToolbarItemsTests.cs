using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the default nav bar's rendering of standard MAUI <c>Page.ToolbarItems</c> against the
/// "Scaffold NavBar Tests" harness (its "Toolbar Title" page): primary items as buttons in
/// priority order, icon vs text, disabled items, the secondary-items overflow menu, runtime
/// mutations, and per-page ownership.
/// </summary>
public class ScaffoldNavBarToolbarItemsTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold NavBar Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await WaitDisplayedAsync("NavBarPageHome");

        await App.TapAsync("PushNavBarToolbar");
        await WaitDisplayedAsync("NavBarPageToolbar");
        await App.WaitForTextAsync("NavBarTitleLabel", "Toolbar Title");
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    /// <summary>Waits until the element is actually DISPLAYED (positioned in the window).</summary>
    private Task WaitDisplayedAsync(string automationId)
        => App.WaitForBoundsAsync(automationId, b => b.Y > 0);

    private async Task WaitForOpacityAsync(string automationId, double expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        double last;

        do
        {
            last = await App.GetDoublePropertyAsync(automationId, "Opacity");

            if (Math.Abs(last - expected) < 0.01)
            {
                return;
            }

            await Task.Delay(100);
        }
        while (DateTime.UtcNow < deadline);

        last.Should().BeApproximately(expected, 0.01);
    }

    [Fact]
    public async Task PrimaryItemsRenderByPriorityInTheTrailingSlot()
    {
        var title = await App.WaitForStableBoundsAsync("NavBarTitleLabel");
        var save = await App.WaitForStableBoundsAsync("ToolbarSave");
        var share = await App.WaitForStableBoundsAsync("ToolbarShare");
        var locked = await App.WaitForStableBoundsAsync("ToolbarLocked");

        // Declared as share, save, locked — rendered by priority: save (0), share (1), locked (2).
        save.X.Should().BeGreaterThanOrEqualTo(title.Right - 1, "toolbar items sit after the title, on the trailing side");
        share.X.Should().BeGreaterThanOrEqualTo(save.Right - 1, "priority 0 comes before priority 1");
        locked.X.Should().BeGreaterThanOrEqualTo(share.Right - 1, "priority 1 comes before priority 2");
        save.CenterY.Should().BeApproximately(title.CenterY, 2, "items share the title's row");

        share.Width.Should().BeApproximately(44, 1.5, "an icon item keeps the 44dp glyph-button footprint");
        save.Width.Should().BeGreaterThan(44, "a text item sizes to its label");

        (await App.WaitForElementAsync("NavBarToolbarOverflowButton")).IsVisible.Should().BeTrue("the page has secondary items");
        var overflow = await App.WaitForStableBoundsAsync("NavBarToolbarOverflowButton");
        overflow.X.Should().BeGreaterThanOrEqualTo(locked.Right - 1, "the overflow button follows the primary items");
    }

    [Fact]
    public async Task TappingAnItemRunsItsCommandOrClickedHandler()
    {
        await WaitDisplayedAsync("ToolbarSave");

        // Save is bound to the page model's command: the items inherit the page's binding context.
        await App.TapAsync("ToolbarSave");
        await App.WaitForTextAsync("ToolbarActivationLog", "save");

        await App.TapAsync("ToolbarShare");
        await App.WaitForTextAsync("ToolbarActivationLog", "share");
    }

    [Fact]
    public async Task DisabledItemIsDimmedAndInertUntilEnabled()
    {
        await WaitDisplayedAsync("ToolbarLocked");
        await WaitForOpacityAsync("ToolbarLocked", 0.38);

        await App.TapAsync("ToolbarLocked");
        await Task.Delay(300);
        (await App.WaitForStableTextAsync("ToolbarActivationLog")).Should().Be("-", "a disabled item never activates — not even its Clicked handler");

        await App.TapAsync("ToggleLockedItem");
        await WaitForOpacityAsync("ToolbarLocked", 1);

        await App.TapAsync("ToolbarLocked");
        await App.WaitForTextAsync("ToolbarActivationLog", "locked");
    }

    [Fact]
    public async Task OverflowMenuListsSecondaryItemsByPriorityAndActivatesThem()
    {
        await WaitDisplayedAsync("ToolbarSave");

        await App.TapAsync("NavBarToolbarOverflowButton");
        await WaitDisplayedAsync("NavBarToolbarOverflowPanel");

        var panel = await App.WaitForStableBoundsAsync("NavBarToolbarOverflowPanel");
        var overflow = await App.WaitForStableBoundsAsync("NavBarToolbarOverflowButton");
        var about = await App.WaitForStableBoundsAsync("ToolbarAbout");
        var settings = await App.WaitForStableBoundsAsync("ToolbarSettings");

        about.Y.Should().BeLessThan(settings.Y, "priority 0 is listed first");
        panel.Y.Should().BeGreaterThanOrEqualTo(overflow.Bottom - 1, "the menu hangs below its button");
        panel.Right.Should().BeApproximately(overflow.Right, 2, "the menu is end-aligned with its button, opening inward");
        panel.X.Should().BeGreaterThanOrEqualTo(0, "the menu never leaves the screen");

        // A menu, not a sheet: the panel hugs its widest row (icon + "Settings" is well under
        // half a phone width) and every row stretches to the panel so the press highlight and
        // the tap target span it edge to edge.
        var (windowWidth, _) = await App.GetWindowSizeAsync();
        panel.Width.Should().BeLessThan(windowWidth / 2, "the menu measures to its content, never to the presentation area");
        panel.Width.Should().BeGreaterThanOrEqualTo(200 - 1, "the menu keeps the default minimum width");
        settings.Width.Should().BeApproximately(panel.Width, 1.5, "rows stretch to the panel width");
        about.Width.Should().BeApproximately(panel.Width, 1.5, "rows stretch to the panel width");

        // A row dismisses the menu, then activates its item.
        await App.TapAsync("ToolbarSettings");
        await App.WaitForTextAsync("ToolbarActivationLog", "settings");
        await App.WaitForElementGoneAsync("NavBarToolbarOverflowPanel");
    }

    [Fact]
    public async Task ScrimTapClosesTheOverflowMenu()
    {
        await WaitDisplayedAsync("ToolbarSave");

        await App.TapAsync("NavBarToolbarOverflowButton");
        await WaitDisplayedAsync("NavBarToolbarOverflowPanel");

        await App.TapAsync("PopupScrim");
        await App.WaitForElementGoneAsync("NavBarToolbarOverflowPanel");
        (await App.WaitForStableTextAsync("ToolbarActivationLog")).Should().Be("-", "dismissing activates nothing");
    }

    [Fact]
    public async Task RuntimeMutationsAreReflectedImmediately()
    {
        await WaitDisplayedAsync("ToolbarSave");

        // An item added after the page is presented shows up (native toolbars often ignore this).
        await App.TapAsync("AddToolbarItem");
        await WaitDisplayedAsync("ToolbarExtra1");
        await App.TapAsync("ToolbarExtra1");
        await App.WaitForTextAsync("ToolbarActivationLog", "extra1");

        // Moving an item to the secondary surface takes it out of the bar and into the menu. The
        // page's ToolbarItem keeps carrying the id (it never leaves the tree): what must go is
        // the rendered button.
        await App.TapAsync("MoveShareToOverflow");
        await App.WaitForElementNotDisplayedAsync("ToolbarShare");

        await App.TapAsync("NavBarToolbarOverflowButton");
        await WaitDisplayedAsync("NavBarToolbarOverflowPanel");
        await WaitDisplayedAsync("ToolbarShare");

        await App.TapAsync("ToolbarShare");
        await App.WaitForTextAsync("ToolbarActivationLog", "share");
        await App.WaitForElementGoneAsync("NavBarToolbarOverflowPanel");
    }

    /// <summary>
    /// The "if room" contract on DEFAULT settings (the fold page keeps the bar's title
    /// reservation; the toolbar page lifts it): primary items are taken in priority order while they
    /// fit the width left beside the fixed buttons and the title's floor; the rest fold into the
    /// overflow menu ahead of the secondary items. Native bars let a long list eat the title
    /// (and this bar once pushed the back button off screen) — pinned on geometry.
    /// </summary>
    [Fact]
    public async Task ManyPrimaryItemsFoldIntoTheOverflowInsteadOfEatingTheTitle()
    {
        await WaitDisplayedAsync("ToolbarSave");
        await App.TapAsync("PopNavBarToolbar");

        // The covered home page never left the element tree, so "home displayed" is true at
        // once; the pop has SETTLED when the popped page is gone — a push before that is
        // rejected by the engine's busy gate.
        await App.WaitForElementGoneAsync("NavBarPageToolbar");
        await WaitDisplayedAsync("NavBarPageHome");

        await App.TapAsync("PushNavBarFold");
        await WaitDisplayedAsync("NavBarPageFold");
        await App.WaitForTextAsync("NavBarTitleLabel", "Fold Title");
        await WaitDisplayedAsync("ToolbarFold1");

        var (windowWidth, _) = await App.GetWindowSizeAsync();
        var back = await App.WaitForStableBoundsAsync("NavBarBackButton");
        var titleSlot = await App.WaitForStableBoundsAsync("NavBarTitle");
        var titleLabel = await App.WaitForStableBoundsAsync("NavBarTitleLabel");
        var strip = await App.WaitForStableBoundsAsync("NavBarToolbarItems");
        var overflow = await App.WaitForStableBoundsAsync("NavBarToolbarOverflowButton");
        var first = await App.WaitForStableBoundsAsync("ToolbarFold1");

        titleSlot.Width.Should().BeGreaterThanOrEqualTo(96 - 1, "the title keeps its floor whatever the items ask for");
        titleLabel.Width.Should().BeGreaterThan(40, "the title text is still readable");
        titleSlot.X.Should().BeGreaterThanOrEqualTo(back.Right - 1, "the title still starts after the back button");
        back.X.Should().BeGreaterThanOrEqualTo(0, "the back button is not pushed off screen");
        strip.Right.Should().BeLessThanOrEqualTo(windowWidth + 1, "the strip never leaves the screen");
        first.X.Should().BeGreaterThanOrEqualTo(titleSlot.Right - 1, "the first item by priority stays in the bar");
        overflow.Width.Should().BeApproximately(44, 1.5, "the overflow button shows for the folded items");
        overflow.X.Should().BeGreaterThanOrEqualTo(first.Right - 1, "the overflow button is the strip's last slot");

        // Eight icon items never fit beside the title on a phone: the last one folds —
        // hidden in the bar, listed in the menu before the secondary item.
        (await App.WaitForElementAsync("ToolbarFold8")).IsVisible.Should().BeFalse("a folded item leaves the bar");

        await App.TapAsync("NavBarToolbarOverflowButton");
        await WaitDisplayedAsync("NavBarToolbarOverflowPanel");
        var folded = await App.WaitForBoundsAsync("ToolbarFold8", b => b.Y > 0 && b.Width > 0);
        var secondary = await App.WaitForStableBoundsAsync("ToolbarFoldSecondary");
        folded.Y.Should().BeLessThan(secondary.Y, "folded primary items are listed ahead of the secondary ones");

        await App.TapAsync("ToolbarFold8");
        await App.WaitForTextAsync("FoldActivationLog", "fold8");
        await App.WaitForElementGoneAsync("NavBarToolbarOverflowPanel");

        await App.TapAsync("PopNavBarFold");
        await WaitDisplayedAsync("NavBarPageHome");
    }

    [Fact]
    public async Task ItemsBelongToTheirPage()
    {
        await WaitDisplayedAsync("ToolbarSave");

        await App.TapAsync("PopNavBarToolbar");
        await WaitDisplayedAsync("NavBarPageHome");
        await App.WaitForTextAsync("NavBarTitleLabel", "Home Title");

        // The popped page's bar is gone with it; the home bar has no items and no overflow.
        await App.WaitForElementGoneAsync("ToolbarSave");
        (await App.WaitForElementAsync("NavBarToolbarOverflowButton")).IsVisible.Should().BeFalse("the home page has no secondary items");
    }
}
