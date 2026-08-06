using Nalu.Maui.UITests.Infrastructure;
using Xunit;

namespace Nalu.Maui.UITests.Tests;

/// <summary>
/// Covers the system-bar icon style resolution (§ system bars) against the
/// "Scaffold System Bar Tests" harness, asserting on the PLATFORM ground truth (the in-app
/// probe reads iOS's effective StatusBarManager style / Android's AppearanceLightStatusBars):
/// bar-luminance resolution across navigation, the transparent-bar declaration and its
/// materialized-bar override, the page-surface derivation and its live declaration override,
/// the theme fallback, and the flyout surface flip.
/// </summary>
public class ScaffoldSystemBarTests(NaluApp app) : BaseUiTest(app), IAsyncLifetime
{
    private const string PageName = "Scaffold System Bar Tests";

    public async ValueTask InitializeAsync() => await App.OpenTestPageAsync(PageName);

    public async ValueTask DisposeAsync() => await App.ResetAsync();

    /// <summary>
    /// Taps the probe until the platform reports the expected style: the flag flip is applied
    /// with a system-side fade (and iOS re-queries the VC asynchronously), so a single read
    /// right after an action may still see the previous value.
    /// </summary>
    private async Task AssertSystemBarsAsync(string marker, string expected)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        string? last = null;

        while (DateTime.UtcNow < deadline)
        {
            await App.TapAsync($"SysBarsProbe{marker}");
            last = await App.WaitForStableTextAsync($"SysBarsValue{marker}");

            if (last == expected)
            {
                return;
            }

            await Task.Delay(150);
        }

        Assert.Fail($"System bars for '{marker}' expected '{expected}' but stayed '{last}'.");
    }

    [Fact(DisplayName = "System bars, on a light opaque scaffold bar, use dark icons")]
    public async Task LightScaffoldBarUsesDarkIcons()
    {
        await App.WaitForElementAsync("SysBarsHome");
        await AssertSystemBarsAsync("Home", "dark-icons");
    }

    [Fact(DisplayName = "System bars, pushing a dark-bar page and popping back, follow the bar luminance")]
    public async Task DarkBarPageFollowsBarLuminance()
    {
        await App.TapAsync("PushSysBarsDarkBar");
        await App.WaitForElementAsync("SysBarsDarkBar");
        await AssertSystemBarsAsync("DarkBar", "light-icons");

        await App.TapAsync("PopSysBarsDarkBar");
        await App.WaitForElementAsync("SysBarsHome");
        await AssertSystemBarsAsync("Home", "dark-icons");
    }

    [Fact(DisplayName = "System bars, on a declared full-bleed page, hold the declaration until the bar materializes")]
    public async Task DeclaredPageHoldsUntilBarMaterializes()
    {
        await App.TapAsync("PushSysBarsDeclared");
        await App.WaitForElementAsync("SysBarsDeclared");

        // Transparent bar: the LightContent declaration rules over the dark full-bleed content.
        await AssertSystemBarsAsync("Declared", "light-icons");

        // The bar materializes WHITE (live appearance mutation — the scroll-driven channel):
        // the opaque bar outranks the declaration and flips the icons dark.
        await App.TapAsync("SysBarsMaterialize");
        await AssertSystemBarsAsync("Declared", "dark-icons");

        await App.TapAsync("PopSysBarsDeclared");
        await App.WaitForElementAsync("SysBarsHome");
        await AssertSystemBarsAsync("Home", "dark-icons");
    }

    [Fact(DisplayName = "System bars, on a bar-less page, derive from the page surface until declared otherwise")]
    public async Task SurfacePageDerivesFromPageSurface()
    {
        await App.TapAsync("PushSysBarsSurface");
        await App.WaitForElementAsync("SysBarsSurface");

        // No bar; the first child spans the top edge with a dark background.
        await AssertSystemBarsAsync("Surface", "light-icons");

        // A LIVE declaration outranks the derived surface.
        await App.TapAsync("SysBarsDeclareDark");
        await AssertSystemBarsAsync("Surface", "dark-icons");

        await App.TapAsync("PopSysBarsSurface");
        await App.WaitForElementAsync("SysBarsHome");
    }

    [Fact(DisplayName = "System bars, on a bare page, follow the app theme live")]
    public async Task BarePageFollowsThemeLive()
    {
        await App.TapAsync("PushSysBarsBare");
        await App.WaitForElementAsync("SysBarsBare");

        try
        {
            // Force a KNOWN starting theme (the harness toggles relative to the current one).
            await App.TapAsync("SysBarsToggleTheme");
            var afterFirstToggle = await ReadBareAsync();

            // Toggle again: the icons must flip to the opposite style.
            await App.TapAsync("SysBarsToggleTheme");
            await AssertSystemBarsAsync("Bare", Opposite(afterFirstToggle));
        }
        finally
        {
            await App.TapAsync("SysBarsResetTheme");
            await App.TapAsync("PopSysBarsBare");
        }
    }

    [Fact(DisplayName = "System bars, opening a dark flyout over the light bar, flip light and restore on close")]
    public async Task DarkFlyoutFlipsAndRestores()
    {
        await AssertSystemBarsAsync("Home", "dark-icons");

        // The scrim is the PRESENTED-state witness: the flyout view exists as a logical child
        // even while closed (and agent taps reach off-screen elements), so waiting on the
        // flyout's own content would pass without any presentation at all.
        await App.TapAsync("OpenSysBarsFlyout");
        await App.WaitForElementAsync("ScaffoldFlyoutScrim");
        await AssertSystemBarsAsync("Flyout", "light-icons");

        await App.TapAsync("SysBarsFlyoutClose");
        await App.WaitForElementGoneAsync("ScaffoldFlyoutScrim");
        await AssertSystemBarsAsync("Home", "dark-icons");
    }

    private async Task<string> ReadBareAsync()
    {
        await App.TapAsync("SysBarsProbeBare");

        var value = await App.WaitForStableTextAsync("SysBarsValueBare");
        Assert.True(value is "light-icons" or "dark-icons", $"Unexpected probe value '{value}'.");

        return value!;
    }

    private static string Opposite(string style) => style == "light-icons" ? "dark-icons" : "light-icons";
}
