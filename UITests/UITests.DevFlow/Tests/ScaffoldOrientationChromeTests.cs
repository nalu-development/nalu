using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers what ROTATION does to the scaffold, against the "Scaffold Orientation Tests" harness:
/// six tab roots that overflow in portrait, plus the safe-area probe on the root page.
/// </summary>
/// <remarks>
/// The scaffold has several size-change-sensitive paths that no portrait-only suite can reach —
/// the tab bar's overflow set is recomputed per layout pass, a hidden strip must stay offscreen
/// across size changes, and the nav bar claims to keep clear of landscape notches. Rotation is
/// driven in-app (see <c>OrientationProbe</c>): no host-side tool can rotate an iOS simulator.
/// Every test restores portrait, since the app is shared by the whole run.
/// </remarks>
public class ScaffoldOrientationChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Orientation Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForBoundsAsync("OrRootPage", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync()
    {
        // Never leave the device rotated for the next class.
        await App.SetOrientationAsync(landscape: false);
        await App.ResetAsync();
    }

    /// <summary>
    /// The bar fits as many roots as the window allows: a wider window must take items BACK from
    /// the overflow panel, not keep the partition it computed in portrait.
    /// </summary>
    [Fact]
    public async Task RotatingRepartitionsTheTabBarOverflow()
    {
        // Portrait: the last roots do not fit and are parked offscreen in the overflow panel.
        var overflowedInPortrait = await App.GetBoundsAsync("TabSix");
        overflowedInPortrait.X.Should().BeLessThan(0, "six roots cannot fit a portrait phone bar");
        (await App.WaitForElementAsync("TabMore")).IsVisible.Should().BeTrue("the overflow item stands in for them");

        await App.SetOrientationAsync(landscape: true);

        // Landscape: the same root is now IN the bar.
        await App.WaitForBoundsAsync("TabSix", b => b.X > 0);

        await App.SetOrientationAsync(landscape: false);

        // …and back out again when the window narrows.
        await App.WaitForBoundsAsync("TabSix", b => b.X < 0);
    }

    /// <summary>
    /// A tab bar hidden by the current page is translated offscreen, not torn down. A rotation
    /// re-lays out the strip against a window of different proportions: it must stay offscreen,
    /// and come back at its resting frame once the page that hid it is popped.
    /// </summary>
    [Fact]
    public async Task HiddenTabBarStaysOffscreenAcrossRotation()
    {
        var resting = await App.WaitForStableBoundsAsync("TabOne");

        await App.TapAsync("PushOrDetail");
        await App.WaitForBoundsAsync("OrDetailPage", b => b.Y > 0);
        await App.WaitForElementGoneAsync("TabOne");

        await App.SetOrientationAsync(landscape: true);
        (await App.FindElementAsync("TabOne")).Should().BeNull("a strip hidden by the current page must not reappear when the window changes shape");

        await App.SetOrientationAsync(landscape: false);
        (await App.FindElementAsync("TabOne")).Should().BeNull("nor on the way back");

        // Popping the page that hid it brings it back where it was.
        await App.TapAsync("PopOrDetail");
        await App.WaitForBoundsAsync("OrRootPage", b => b.Y > 0);

        await App.WaitForBoundsAsync(
            "TabOne",
            b => Math.Abs(b.Y - resting.Y) <= 1 && Math.Abs(b.Height - resting.Height) <= 1
        );
    }

    /// <summary>
    /// A sheet with a MaxWidth spans the window while it is narrower than the cap, and floats
    /// CENTERED at the cap once rotation makes the window wider — still bottom-anchored.
    /// </summary>
    [Fact]
    public async Task CappedSheetFloatsCenteredOnceTheWindowIsWiderThanTheCap()
    {
        const double maxWidth = 500;

        await App.TapAsync("ShowOrSheet");
        var portraitSheet = await App.WaitForStableBoundsAsync("ScaffoldBottomSheet");
        var (portraitWidth, _) = await App.GetWindowSizeAsync();

        Assert.SkipWhen(portraitWidth > maxWidth, "This device is already wider than the cap in portrait: the two states would be identical.");
        portraitSheet.Width.Should().BeApproximately(portraitWidth, 2, "a window narrower than the cap gives the sheet its full width");

        await App.SetOrientationAsync(landscape: true);

        var (landscapeWidth, _) = await App.GetWindowSizeAsync();
        var landscapeSheet = await App.WaitForStableBoundsAsync("ScaffoldBottomSheet");

        landscapeSheet.Width.Should().BeApproximately(maxWidth, 2, "past the cap the sheet stops growing");
        landscapeSheet.CenterX.Should().BeApproximately(landscapeWidth / 2, 2, "and floats centered rather than hugging an edge");

        await App.TapAsync("SheetScrim");
        await App.SetOrientationAsync(landscape: false);
    }

    /// <summary>
    /// A sheet resting at a FRACTION detent must re-resolve that fraction when the window loses
    /// most of its height: keeping the portrait height would leave the sheet taller than the
    /// landscape window it now sits in.
    /// </summary>
    [Fact]
    public async Task ExpandedSheetShrinksToTheLandscapeHeight()
    {
        await App.TapAsync("ShowOrTallSheet");

        var portraitSheet = await App.WaitForStableBoundsAsync("ScaffoldBottomSheet");
        var (_, portraitHeight) = await App.GetWindowSizeAsync();
        portraitSheet.Height.Should().BeLessThanOrEqualTo(portraitHeight, "a sheet never exceeds the window it opens in");

        await App.SetOrientationAsync(landscape: true);

        var (_, landscapeHeight) = await App.GetWindowSizeAsync();
        var landscapeSheet = await App.WaitForStableBoundsAsync("ScaffoldBottomSheet");

        landscapeSheet.Height.Should().BeLessThanOrEqualTo(
            landscapeHeight,
            $"the fraction detent must be re-resolved against the shorter window (portrait {portraitSheet.Height:0} in {portraitHeight:0}, landscape {landscapeSheet.Height:0} in {landscapeHeight:0})"
        );

        landscapeSheet.Y.Should().BeGreaterThanOrEqualTo(-1, "and the sheet must not be pushed off the top of the screen");

        await App.TapAsync("SheetScrim");
        await App.SetOrientationAsync(landscape: false);
    }

    /// <summary>
    /// A CENTERED popup is placed against the window: a shape change must re-resolve that
    /// placement, or the popup stays where the old window's centre used to be.
    /// </summary>
    [Fact]
    public async Task CenteredPopupRecentersOnTheNewWindow()
    {
        await App.TapAsync("ShowOrPopup");

        var portrait = await App.WaitForStableBoundsAsync("OrPopupContent");
        var (portraitWidth, _) = await App.GetWindowSizeAsync();

        // Size FIRST: a wrap-content-collapsed popup can still be perfectly centered, so the
        // center assertions alone once passed while the popup rendered label-sized (Android
        // measured the platform view natively, bypassing WidthRequest/HeightRequest).
        portrait.Width.Should().BeApproximately(240, 2, "the popup takes its requested width");
        portrait.Height.Should().BeApproximately(180, 2, "and its requested height");

        portrait.CenterX.Should().BeApproximately(portraitWidth / 2, 2, "a centered popup starts centered");

        await App.SetOrientationAsync(landscape: true);

        var (landscapeWidth, landscapeHeight) = await App.GetWindowSizeAsync();
        var landscape = await App.WaitForStableBoundsAsync("OrPopupContent");

        landscape.CenterX.Should().BeApproximately(landscapeWidth / 2, 2, "and must re-center on the window it now sits in");
        landscape.CenterY.Should().BeApproximately(landscapeHeight / 2, 20, "vertically too, allowing for the safe-area area it is centered within");

        await App.TapAsync("PopupScrim");
        await App.SetOrientationAsync(landscape: false);
    }

    /// <summary>
    /// The overflow panel is DISMISSED by a shape change, not re-laid out: it is a transient menu
    /// hanging off the bar, and the set it lists is repartitioned for the new window.
    /// </summary>
    /// <remarks>
    /// The already-covered path is the set CHANGING (a wider window takes items back), which the
    /// bar reports and the panel closes on. This is the other one: a shape change that leaves the
    /// partition alone still has to close it, or the panel survives at the old window's geometry.
    /// Landscape here keeps at least one root overflowed, so the panel would have something to
    /// show and no reason to close by the existing path.
    /// </remarks>
    [Fact]
    public async Task RotatingClosesTheOverflowPanel()
    {
        await App.TapAsync("TabMore");
        await App.WaitForElementAsync("TabBarOverflowPanel");

        await App.SetOrientationAsync(landscape: true);

        await App.WaitForElementGoneAsync("TabBarOverflowPanel");

        // …and the bar is left usable: the panel can be opened again in the new orientation
        // (a close that tore down more than the panel would show up here).
        (await App.WaitForElementAsync("TabMore")).IsVisible.Should().BeTrue();

        await App.SetOrientationAsync(landscape: false);
    }

    /// <summary>
    /// An ANCHORED popup is placed against its anchor, not the window: a shape change moves the
    /// anchor, so the dropdown has to be re-placed or it stays where the anchor USED to be.
    /// </summary>
    /// <remarks>
    /// The contract is start-aligned and vertically adjacent — below by default, flipping above
    /// when it does not fit. Landscape is exactly where that flip becomes likely (the window keeps
    /// a fraction of its height), so the assertion allows either side rather than pinning "below"
    /// and calling a documented flip a failure.
    /// </remarks>
    [Fact]
    public async Task AnchoredDropdownFollowsItsAnchorAcrossRotation()
    {
        await App.TapAsync("ShowOrDropdown");

        var anchor = await App.WaitForStableBoundsAsync("ShowOrDropdown");
        var dropdown = await App.WaitForStableBoundsAsync("OrDropdownContent");

        // Size first, for the same reason as the centered popup: adjacency holds for a collapsed
        // dropdown too.
        dropdown.Width.Should().BeApproximately(200, 2, "the dropdown takes its requested width");
        dropdown.Height.Should().BeApproximately(120, 2, "and its requested height");

        AssertAnchored(dropdown, anchor, "portrait");

        await App.SetOrientationAsync(landscape: true);

        var rotatedAnchor = await App.WaitForStableBoundsAsync("ShowOrDropdown");
        var rotatedDropdown = await App.WaitForStableBoundsAsync("OrDropdownContent");
        AssertAnchored(rotatedDropdown, rotatedAnchor, "landscape");

        await App.TapAsync("PopupScrim");
        await App.SetOrientationAsync(landscape: false);

        static void AssertAnchored(ElementBounds dropdown, ElementBounds anchor, string orientation)
        {
            dropdown.X.Should().BeApproximately(anchor.X, 2, $"the dropdown is start-aligned with its anchor ({orientation})");

            var below = dropdown.Y >= anchor.Bottom - 1;
            var above = dropdown.Bottom <= anchor.Y + 1;

            (below || above).Should()
                            .BeTrue($"the dropdown must sit against its anchor in {orientation} — anchor {anchor}, dropdown {dropdown}");
        }
    }

    /// <summary>
    /// A flyout's width is a FRACTION of the window's and it is pinned to an edge over the full
    /// height: a shape change must recompute both, or it keeps the old window's proportions.
    /// </summary>
    [Fact]
    public async Task OpenFlyoutResizesToTheNewWindow()
    {
        const double widthRatio = 0.6;

        await App.TapAsync("OpenOrFlyout");

        var portrait = await App.WaitForStableBoundsAsync("OrFlyoutMenu");
        var (portraitWidth, portraitHeight) = await App.GetWindowSizeAsync();
        portrait.Width.Should().BeApproximately(portraitWidth * widthRatio, 2, "the flyout takes its share of the window");
        portrait.Height.Should().BeApproximately(portraitHeight, 2, "over its full height");

        await App.SetOrientationAsync(landscape: true);

        var (landscapeWidth, landscapeHeight) = await App.GetWindowSizeAsync();
        var landscape = await App.WaitForStableBoundsAsync("OrFlyoutMenu");

        landscape.Width.Should().BeApproximately(landscapeWidth * widthRatio, 2, "the share is re-taken from the new window");
        landscape.Height.Should().BeApproximately(landscapeHeight, 2, "and it still spans the full height");

        await App.SetOrientationAsync(landscape: false);
    }

    /// <summary>
    /// Landscape brings side insets into play (the notch edge). The page must keep clear of them:
    /// content starting at x = 0 would sit under the cutout.
    /// </summary>
    [Fact]
    public async Task PageKeepsClearOfTheSideInsetsInLandscape()
    {
        await App.SetOrientationAsync(landscape: true);

        var insets = await App.GetSafeAreaInsetsAsync();
        var side = Math.Max(insets.Left, insets.Right);
        Assert.SkipWhen(side <= 0, "This device has no side insets in landscape: nothing to keep clear of.");

        var marker = await App.WaitForStableBoundsAsync("OrRootPage");
        marker.X.Should().BeGreaterThanOrEqualTo(insets.Left - 1, "page content must not sit under the display cutout");

        await App.SetOrientationAsync(landscape: false);
    }
}
