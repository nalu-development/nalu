using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// On-device sanity for the chain × margins × visibility playground: gone margins applied through the
/// animated toggle, and weighted redistribution of hidden members.
/// </summary>
public class MagnetChainPlaygroundTests(NaluApp app) : BaseUiTest(app)
{
    [Fact]
    public async Task TogglingAMemberAppliesGoneMarginsAndRedistributesWeights()
    {
        await App.OpenTestPageAsync("Magnet Chain Playground");
        var root = await App.WaitForStableBoundsAsync("pkRoot");
        var a = await App.GetBoundsAsync("pkA");
        var b = await App.GetBoundsAsync("pkB");
        var c = await App.GetBoundsAsync("pkC");

        // Packed row: gaps 8 and 16.
        a.X.Should().BeApproximately(root.X, 1);
        b.X.Should().BeApproximately(a.Right + 8, 1);
        c.X.Should().BeApproximately(b.Right + 16, 1);

        var wtA = await App.GetBoundsAsync("wtA");
        wtA.Width.Should().BeApproximately(83, 1.5, "weighted 1:2:1 over 340 minus 8 of gaps");

        // Hide every B (animated): packed C ends at A.right + gone(4); weighted A grows to (340-4)/2.
        await App.TapAsync("ToggleB");
        await App.WaitForBoundsAsync("pkC", bounds => Math.Abs(bounds.X - (a.Right + 4)) < 1, TimeSpan.FromSeconds(5));
        await App.WaitForBoundsAsync("wtA", bounds => Math.Abs(bounds.Width - 168) < 1.5, TimeSpan.FromSeconds(5));

        // Show it back: everything returns.
        await App.TapAsync("ToggleB");
        await App.WaitForBoundsAsync("pkC", bounds => Math.Abs(bounds.X - (b.Right + 16)) < 1, TimeSpan.FromSeconds(5));
        await App.WaitForBoundsAsync("wtA", bounds => Math.Abs(bounds.Width - 83) < 1.5, TimeSpan.FromSeconds(5));
    }
}
