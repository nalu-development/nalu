using FluentAssertions;
using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Chrome must take touches only where it is VISIBLE. Both bars are strips spanning the full
/// width while what you can see of them does not: the tab bar draws a centred pill with empty
/// margins either side, and a nav bar offset out of the window leaves its whole band empty.
/// Everything in those bands that is not drawn belongs to the page underneath — a strip that
/// swallows touches there is an invisible dead zone, and nothing about the app's appearance would
/// explain it to the person tapping.
/// </summary>
/// <remarks>
/// Driven by REAL taps (adb <c>input tap</c>), never the agent's own: agent taps are in-process
/// and invoke an element's handlers directly, so they reach elements that are covered, offscreen
/// or not presented at all — they cannot tell transparent glass from a strip that eats everything
/// under it, which is the entire question here. That confines these to Android; the iOS simulator
/// offers no touch injection, and the suite skips its other real-touch tests for the same reason.
/// </remarks>
public class ScaffoldChromeHitChromeTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string _pageName = "Scaffold Chrome Hit Tests";

    public async ValueTask InitializeAsync()
    {
        await App.OpenTestPageAsync(_pageName);
        await App.WaitForBoundsAsync("HitPageLabel", b => b.Y > 0);
    }

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    private async Task<bool> IsAndroidAsync()
        => (await App.GetPlatformAsync()).Contains("android", StringComparison.OrdinalIgnoreCase);

    private async Task<int> TapCountAsync()
    {
        var text = await App.GetPropertyAsync("HitCount", "Text") ?? "taps:0";
        var digits = text["taps:".Length..].Split(' ')[0];

        return int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Taps a window point for real, and reports whether the PAGE received it.</summary>
    private async Task<bool> PageReceivesTapAtAsync(double x, double y)
    {
        var before = await TapCountAsync();
        await App.AndroidRealTapAtPointAsync(x, y);

        // Long enough for the touch to be delivered — and, when it is not, long enough to show
        // that nothing arrived.
        await Task.Delay(600, TestContext.Current.CancellationToken);

        return await TapCountAsync() > before;
    }

    [Fact(DisplayName = "The empty margin beside the tab bar pill belongs to the page")]
    public async Task TheTabBarStripPassesTouchesThroughItsMargins()
    {
        Assert.SkipWhen(!await IsAndroidAsync(), "Real taps are injected host-side via adb.");

        var pill = await App.WaitForStableBoundsAsync("TabHit");
        var (windowWidth, _) = await App.GetWindowSizeAsync();

        (await PageReceivesTapAtAsync(3, pill.CenterY))
            .Should().BeTrue("the strip spans the full width but only the pill is drawn there");

        (await PageReceivesTapAtAsync(windowWidth - 3, pill.CenterY))
            .Should().BeTrue("both margins belong to the page, not to the chrome");

        // The control: the pill must still consume its own touches, or the tab bar would be
        // unusable — and a strip that passed EVERYTHING through would satisfy the two above.
        (await PageReceivesTapAtAsync(pill.CenterX, pill.CenterY))
            .Should().BeFalse("the pill takes its own touches");
    }

    [Fact(DisplayName = "A nav bar takes touches on its surface and gives the band back when offset")]
    public async Task AnOffsetNavBarPassesTouchesThrough()
    {
        Assert.SkipWhen(!await IsAndroidAsync(), "Real taps are injected host-side via adb.");

        var bar = await App.WaitForStableBoundsAsync("NavBarSurface");

        // Low in the bar and centred: clear of the back and flyout buttons, and clear of the
        // system status bar, which owns the top of that band and would eat the touch itself.
        var probeX = bar.CenterX;
        var probeY = bar.Y + (bar.Height * 0.75);

        (await PageReceivesTapAtAsync(probeX, probeY))
            .Should().BeFalse("a bar that is DRAWN there takes the touch — a visible bar must not "
                              + "operate the content hidden behind it");

        try
        {
            // Offset by -100 the bar travels out of the window, and the band it vacated belongs
            // to the page again. A strip still taking touches where nothing is drawn is the bug.
            await App.TapAsync("HitOffsetNavBar");
            await App.WaitForBoundsAsync("NavBarSurface", b => b.Y <= bar.Y - 90, TimeSpan.FromSeconds(5));

            (await PageReceivesTapAtAsync(probeX, probeY))
                .Should().BeTrue("the bar was moved out of the window: its old band is page again");
        }
        finally
        {
            await App.TapAsync("HitRestoreNavBar");
        }
    }
}
