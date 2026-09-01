using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the <c>Magnet</c> constraint layout: anchors, GONE margins, chains, barriers, transitions and XAML syntax.
/// </summary>
public class MagnetTests(NaluApp app) : BaseUiTest(app)
{
    private const string PageName = "Magnet Tests";
    private const string XamlPageName = "Magnet XAML Tests";

    [Fact]
    public async Task AnchorsResolveRelativeToTheStageAndSiblings()
    {
        await App.OpenTestPageAsync(PageName);
        var root = await App.WaitForStableBoundsAsync("MagnetRoot");
        var avatar = await App.GetBoundsAsync("avatar");
        var title = await App.GetBoundsAsync("title");
        var subtitle = await App.GetBoundsAsync("subtitle");
        var badge = await App.GetBoundsAsync("badge");

        avatar.X.Should().BeApproximately(root.X + 16, 1);
        avatar.Y.Should().BeApproximately(root.Y + 16, 1);
        avatar.Width.Should().BeApproximately(48, 1);
        title.X.Should().BeApproximately(avatar.Right + 12, 1);
        title.Y.Should().BeApproximately(avatar.Y, 1);
        subtitle.X.Should().BeApproximately(title.X, 1);
        subtitle.Y.Should().BeApproximately(title.Bottom + 2, 1);
        badge.Right.Should().BeApproximately(root.Right - 16, 1);
        root.Width.Should().BeApproximately(320, 1);
    }

    [Fact]
    public async Task ChainSpreadsMembersBelowTheBarrier()
    {
        await App.OpenTestPageAsync(PageName);
        var root = await App.WaitForStableBoundsAsync("MagnetRoot");
        var avatar = await App.GetBoundsAsync("avatar");
        var subtitle = await App.GetBoundsAsync("subtitle");
        var f1 = await App.GetBoundsAsync("f1");
        var f2 = await App.GetBoundsAsync("f2");
        var f3 = await App.GetBoundsAsync("f3");

        // Barrier = max(avatar.Bottom, subtitle.Bottom) + 8
        var barrier = Math.Max(avatar.Bottom, subtitle.Bottom) + 8;
        f1.Y.Should().BeApproximately(barrier, 1);

        // Spread: equal gaps around three 40-wide members between the 16dp margins.
        var gap = (root.Width - 32 - 120) / 4;
        f1.X.Should().BeApproximately(root.X + 16 + gap, 1);
        f2.X.Should().BeApproximately(f1.Right + gap, 1);
        f3.X.Should().BeApproximately(f2.Right + gap, 1);
        root.Bottom.Should().BeApproximately(f1.Bottom + 16, 1, "the layout hugs its content");
    }

    [Fact]
    public async Task CollapsedTargetUsesGoneMargin()
    {
        await App.OpenTestPageAsync(PageName);
        var root = await App.WaitForStableBoundsAsync("MagnetRoot");
        var titleBefore = await App.GetBoundsAsync("title");

        await App.TapAsync("ToggleAvatarButton");

        var title = await App.WaitForBoundsAsync("title", b => b.X < titleBefore.X - 10, TimeSpan.FromSeconds(5));
        title.X.Should().BeApproximately(root.X, 1, "gone margin is 0 and the collapsed avatar drops its own margin");
        var rootAfter = await App.WaitForStableBoundsAsync("MagnetRoot");
        rootAfter.Height.Should().BeLessThan(root.Height, "the avatar no longer contributes to the height");

        await App.TapAsync("ToggleAvatarButton");
        await App.WaitForBoundsAsync("title", b => Math.Abs(b.X - titleBefore.X) < 1, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ValuesTransitionMovesTheAvatar()
    {
        await App.OpenTestPageAsync(PageName);
        var root = await App.WaitForStableBoundsAsync("MagnetRoot");

        await App.TapAsync("TransitionButton");

        var avatar = await App.WaitForBoundsAsync("avatar", b => Math.Abs(b.X - (root.X + 80)) < 1, TimeSpan.FromSeconds(5));
        avatar.Y.Should().BeApproximately(root.Y + 80, 1);
        var title = await App.WaitForStableBoundsAsync("title");
        title.X.Should().BeApproximately(avatar.Right + 12, 1);
    }

    [Fact]
    public async Task StructureTransitionSwapsTheBadgeSide()
    {
        await App.OpenTestPageAsync(PageName);
        var root = await App.WaitForStableBoundsAsync("MagnetRoot");

        await App.TapAsync("SwapBadgeButton");

        var badge = await App.WaitForBoundsAsync("badge", b => Math.Abs(b.X - (root.X + 16)) < 1, TimeSpan.FromSeconds(5));
        badge.Y.Should().BeApproximately(root.Y + 16, 1);
    }

    [Fact]
    public async Task SceneSwapFadesTheHiddenNodeAndRelayoutsSiblings()
    {
        await App.OpenTestPageAsync(PageName);
        var root = await App.WaitForStableBoundsAsync("SceneRoot");
        var text = await App.GetBoundsAsync("sceneText");

        // Scene A: icon (24) at x+16, text 12 after it.
        text.X.Should().BeApproximately(root.X + 52, 1);

        // Scene B hides the icon via ApplyVisibility: the text animates to the gone position (gone margin 0).
        await App.TapAsync("SceneButton");
        await App.WaitForBoundsAsync("sceneText", b => Math.Abs(b.X - root.X) < 1, TimeSpan.FromSeconds(5));

        // Back to scene A: the icon fades back in and the text returns after it.
        await App.TapAsync("SceneButton");
        await App.WaitForBoundsAsync("sceneText", b => Math.Abs(b.X - (root.X + 52)) < 1, TimeSpan.FromSeconds(5));
        var icon = await App.GetBoundsAsync("sceneIcon");
        icon.X.Should().BeApproximately(root.X + 16, 1);
    }

    [Fact]
    public async Task XamlAttachedPropertiesAndDefinitionNodesWork()
    {
        await App.OpenTestPageAsync(XamlPageName);
        var root = await App.WaitForStableBoundsAsync("XamlMagnetRoot");
        var avatar = await App.GetBoundsAsync("avatar");
        var title = await App.GetBoundsAsync("title");
        var subtitle = await App.GetBoundsAsync("subtitle");
        var half = await App.GetBoundsAsync("half");

        avatar.X.Should().BeApproximately(root.X + 16, 1);
        title.X.Should().BeApproximately(avatar.Right + 12, 1);
        subtitle.Y.Should().BeApproximately(title.Bottom + 2, 1);

        // Guideline at 50% → half spans from the middle to the right margin, below the barrier.
        half.X.Should().BeApproximately(root.X + (root.Width / 2), 1);
        half.Right.Should().BeApproximately(root.Right - 16, 1);
        half.Y.Should().BeApproximately(Math.Max(avatar.Bottom, subtitle.Bottom) + 8, 1);
        root.Bottom.Should().BeApproximately(half.Bottom + 16, 1);
    }
}
