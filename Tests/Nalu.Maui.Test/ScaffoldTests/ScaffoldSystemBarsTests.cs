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
}
