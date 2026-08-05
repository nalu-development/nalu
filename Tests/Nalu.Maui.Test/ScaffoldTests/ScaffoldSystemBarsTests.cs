using Nalu.Internals;

namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// The system-bar icon style resolution (§ system bars): the icons contrast with the VISIBLE
/// surface stack — overlay → opaque nav bar (by luminance) → declared style (page → area →
/// scaffold) → the page's own top-of-screen surface color → the app theme.
/// True = light (white) icons.
/// </summary>
public class ScaffoldSystemBarsTests
{
    private static ScaffoldSystemBarSnapshot Snapshot(
        ScaffoldSystemBarStyle page = ScaffoldSystemBarStyle.Auto,
        ScaffoldSystemBarStyle area = ScaffoldSystemBarStyle.Auto,
        ScaffoldSystemBarStyle scaffold = ScaffoldSystemBarStyle.Auto,
        bool navBarVisible = false,
        Brush? barBackground = null,
        double barOpacity = 1,
        Color? overlaySurface = null,
        Color? pageSurface = null,
        double? sampledLuminance = null,
        bool darkTheme = false
    ) => new(page, area, scaffold, navBarVisible, barBackground, barOpacity, overlaySurface, pageSurface, sampledLuminance, darkTheme);

    private static bool Resolve(in ScaffoldSystemBarSnapshot snapshot) => ScaffoldSystemBars.ResolveLightIcons(snapshot);

    [Fact(DisplayName = "Resolution, given nothing, follows the theme")]
    public void ResolutionGivenNothingFollowsTheTheme()
    {
        Resolve(Snapshot()).Should().BeFalse("light theme background wants dark icons");
        Resolve(Snapshot(darkTheme: true)).Should().BeTrue("dark theme background wants light icons");
    }

    [Fact(DisplayName = "Resolution, given a visible opaque nav bar, follows the bar luminance")]
    public void ResolutionGivenOpaqueNavBarFollowsBarLuminance()
    {
        Resolve(Snapshot(navBarVisible: true, barBackground: new SolidColorBrush(Colors.White))).Should().BeFalse();
        Resolve(Snapshot(navBarVisible: true, barBackground: new SolidColorBrush(Colors.DarkBlue))).Should().BeTrue();
    }

    [Fact(DisplayName = "Resolution, given a hidden nav bar, ignores its background")]
    public void ResolutionGivenHiddenNavBarIgnoresBackground()
        => Resolve(Snapshot(navBarVisible: false, barBackground: new SolidColorBrush(Colors.DarkBlue))).Should().BeFalse();

    [Fact(DisplayName = "Resolution, given a transparent or faded nav bar, falls through")]
    public void ResolutionGivenTransparentBarFallsThrough()
    {
        // Alpha under the threshold — the bar is not the visible surface.
        Resolve(Snapshot(navBarVisible: true, barBackground: new SolidColorBrush(Colors.Transparent), darkTheme: true)).Should().BeTrue();

        // Same via the bar OPACITY channel (scroll-driven appearance fading the whole bar).
        Resolve(Snapshot(navBarVisible: true, barBackground: new SolidColorBrush(Colors.White), barOpacity: 0.2, darkTheme: true)).Should().BeTrue();

        // At/above the threshold the bar wins again.
        Resolve(Snapshot(navBarVisible: true, barBackground: new SolidColorBrush(Colors.White), barOpacity: 0.6)).Should().BeFalse();
    }

    [Fact(DisplayName = "Resolution, given an opaque nav bar, outranks the declared style")]
    public void ResolutionGivenOpaqueBarOutranksDeclaration() =>

        // The declaration describes the page's own surface; an opaque bar covering the
        // status-bar region is the actual visible surface.
        Resolve(Snapshot(page: ScaffoldSystemBarStyle.LightContent, navBarVisible: true, barBackground: new SolidColorBrush(Colors.White)))
            .Should().BeFalse();

    [Fact(DisplayName = "Resolution, given declarations, resolves page over area over scaffold")]
    public void ResolutionGivenDeclarationsResolvesMostSpecific()
    {
        Resolve(Snapshot(page: ScaffoldSystemBarStyle.LightContent)).Should().BeTrue();
        Resolve(Snapshot(area: ScaffoldSystemBarStyle.LightContent, scaffold: ScaffoldSystemBarStyle.DarkContent, darkTheme: true)).Should().BeTrue();
        Resolve(Snapshot(scaffold: ScaffoldSystemBarStyle.DarkContent, darkTheme: true)).Should().BeFalse();
        Resolve(Snapshot(page: ScaffoldSystemBarStyle.DarkContent, area: ScaffoldSystemBarStyle.LightContent)).Should().BeFalse();
    }

    [Fact(DisplayName = "Resolution, given a declaration, outranks the page surface and the theme")]
    public void ResolutionGivenDeclarationOutranksPageSurface()
        => Resolve(Snapshot(page: ScaffoldSystemBarStyle.LightContent, pageSurface: Colors.White)).Should().BeTrue();

    [Fact(DisplayName = "Resolution, given a page surface color, follows its luminance")]
    public void ResolutionGivenPageSurfaceFollowsLuminance()
    {
        Resolve(Snapshot(pageSurface: Colors.MidnightBlue)).Should().BeTrue();
        Resolve(Snapshot(pageSurface: Colors.WhiteSmoke, darkTheme: true)).Should().BeFalse();
    }

    [Fact(DisplayName = "Resolution, given a pixel sample, follows it over the page surface and theme")]
    public void ResolutionGivenSampleFollowsItOverSemanticFallbacks()
    {
        // The sample is the rendered ground truth (a dark photo over a light-theme page).
        Resolve(Snapshot(sampledLuminance: 0.2, pageSurface: Colors.White)).Should().BeTrue();
        Resolve(Snapshot(sampledLuminance: 0.8, darkTheme: true)).Should().BeFalse();
    }

    [Fact(DisplayName = "Resolution, given a pixel sample, is outranked by declarations, the opaque bar and overlays")]
    public void ResolutionGivenSampleIsOutrankedByUpperLayers()
    {
        Resolve(Snapshot(page: ScaffoldSystemBarStyle.DarkContent, sampledLuminance: 0.2)).Should().BeFalse("author intent wins over the sample");
        Resolve(Snapshot(navBarVisible: true, barBackground: new SolidColorBrush(Colors.White), sampledLuminance: 0.2)).Should().BeFalse();
        Resolve(Snapshot(overlaySurface: Colors.White, sampledLuminance: 0.2)).Should().BeFalse();
    }

    [Fact(DisplayName = "Resolution, given an overlay surface, outranks everything")]
    public void ResolutionGivenOverlayOutranksEverything()
    {
        // White flyout over a dark opaque nav bar and a LightContent page: dark icons.
        var snapshot = Snapshot(
            page: ScaffoldSystemBarStyle.LightContent,
            navBarVisible: true,
            barBackground: new SolidColorBrush(Colors.DarkBlue),
            overlaySurface: Colors.White,
            darkTheme: true);

        Resolve(snapshot).Should().BeFalse();
        Resolve(snapshot with { OverlaySurface = Colors.Black, DarkTheme = false }).Should().BeTrue();
    }

    [Fact(DisplayName = "Resolution, given a gradient bar background, follows its first stop")]
    public void ResolutionGivenGradientBarFollowsFirstStop()
    {
        var gradient = new LinearGradientBrush(
        [
            new GradientStop(Colors.Black, 0),
            new GradientStop(Colors.White, 1)
        ], new Point(0, 0), new Point(0, 1));

        Resolve(Snapshot(navBarVisible: true, barBackground: gradient)).Should().BeTrue();
    }

    private static (ScaffoldSystemBars Bars, Func<int> RefreshCount) CreateTracked()
    {
        var bars = new ScaffoldSystemBars(new Scaffold());
        var count = 0;
        bars.SetThemeRefresher(() => count++);

        return (bars, () => count);
    }

    [Fact(DisplayName = "Theme refresh, given repeated identical solid backgrounds, fires once")]
    public void ThemeRefreshGivenRepeatedIdenticalSolidBackgroundsFiresOnce()
    {
        var (bars, count) = CreateTracked();

        bars.UpdateBar(null, new SolidColorBrush(Colors.White), 1);
        bars.UpdateBar(null, new SolidColorBrush(Colors.White), 1);
        bars.UpdateBar(null, new SolidColorBrush(Colors.White), 1);

        count().Should().Be(1, "only the first (significant) surface change triggers");
    }

    [Fact(DisplayName = "Theme refresh, when the effective transparency crosses a 10% step, fires")]
    public void ThemeRefreshWhenTransparencyCrossesADecileFires()
    {
        var (bars, count) = CreateTracked();

        bars.UpdateBar(null, new SolidColorBrush(Colors.White.WithAlpha(0.71f)), 1);
        var baseline = count();

        // Same decile: no trigger.
        bars.UpdateBar(null, new SolidColorBrush(Colors.White.WithAlpha(0.7999f)), 1);
        count().Should().Be(baseline);

        // Crossing 79.99% -> 80%: trigger.
        bars.UpdateBar(null, new SolidColorBrush(Colors.White.WithAlpha(0.80f)), 1);
        count().Should().Be(baseline + 1);

        // And back below: trigger again.
        bars.UpdateBar(null, new SolidColorBrush(Colors.White.WithAlpha(0.7999f)), 1);
        count().Should().Be(baseline + 2);
    }

    [Fact(DisplayName = "Theme refresh, when the color changes with identical alpha, fires")]
    public void ThemeRefreshWhenColorChangesWithIdenticalAlphaFires()
    {
        var (bars, count) = CreateTracked();

        bars.UpdateBar(null, new SolidColorBrush(Colors.White.WithAlpha(0.5f)), 1);
        var baseline = count();

        bars.UpdateBar(null, new SolidColorBrush(Colors.Black.WithAlpha(0.5f)), 1);

        count().Should().Be(baseline + 1, "the RGB changed even though the alpha did not");
    }

    [Fact(DisplayName = "Theme refresh, given a non-solid background, fires on every background change")]
    public void ThemeRefreshGivenNonSolidBackgroundFiresOnEveryChange()
    {
        var (bars, count) = CreateTracked();

        LinearGradientBrush MakeGradient() => new(
        [
            new GradientStop(Colors.Black, 0),
            new GradientStop(Colors.White, 1)
        ], new Point(0, 0), new Point(0, 1));

        var gradient = MakeGradient();
        bars.UpdateBar(null, gradient, 1);
        var baseline = count();

        // Same instance, same opacity: not a change.
        bars.UpdateBar(null, gradient, 1);
        count().Should().Be(baseline);

        // A new instance always triggers (its pixels cannot be reasoned about).
        bars.UpdateBar(null, MakeGradient(), 1);
        count().Should().Be(baseline + 1);

        // An opacity change on the same instance triggers too.
        bars.UpdateBar(null, gradient, 0.5);
        count().Should().Be(baseline + 2);
    }

    [Fact(DisplayName = "Theme refresh, when the bar opacity crosses a decile with a solid brush, fires")]
    public void ThemeRefreshWhenBarOpacityCrossesADecileFires()
    {
        var (bars, count) = CreateTracked();

        bars.UpdateBar(null, new SolidColorBrush(Colors.White), 0.85);
        var baseline = count();

        bars.UpdateBar(null, new SolidColorBrush(Colors.White), 0.89);
        count().Should().Be(baseline, "0.85 and 0.89 share the 80% decile");

        bars.UpdateBar(null, new SolidColorBrush(Colors.White), 0.9);
        count().Should().Be(baseline + 1, "0.9 enters the 90% decile");
    }
}
