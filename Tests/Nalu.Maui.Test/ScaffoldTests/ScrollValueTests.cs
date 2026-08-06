using System.Globalization;
using Nalu.Internals;

namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// The scroll-value interpolation engine behind {nalu:ScrollValue} / {nalu:ThemeScrollValue}:
/// ramp math (explicit and page-level defaults), clamp/extend, theme endpoint selection and
/// typed lerp semantics.
/// </summary>
public class ScrollValueTests
{
    private static object? Convert(
        ScrollInterpolationConverter converter,
        double offset,
        double defaultRampStart = 0,
        double defaultRampEnd = 100,
        AppTheme theme = AppTheme.Light
    ) => converter.Convert([offset, defaultRampStart, defaultRampEnd, theme], typeof(object), null, CultureInfo.InvariantCulture);

    [Fact(DisplayName = "Doubles interpolate over the explicit window and clamp outside it")]
    public void DoublesInterpolateAndClamp()
    {
        var converter = new ScrollInterpolationConverter { Kind = ScrollValueKind.Double, RampStart = 100, RampEnd = 200, FromLight = 0.0, ToLight = 1.0 };

        Convert(converter, 0).Should().Be(0.0);
        Convert(converter, 150).Should().Be(0.5);
        Convert(converter, 400).Should().Be(1.0);
    }

    [Fact(DisplayName = "Extend keeps extrapolating linearly (parallax factor)")]
    public void ExtendExtrapolates()
    {
        var converter = new ScrollInterpolationConverter
        {
            Kind = ScrollValueKind.Double,
            RampStart = 0,
            RampEnd = 100,
            FromLight = 0.0,
            ToLight = 50.0,
            Extrapolation = ScrollValueExtrapolation.Extend
        };

        Convert(converter, 300).Should().Be(150.0);
        Convert(converter, -40).Should().Be(-20.0);
    }

    [Fact(DisplayName = "Omitted RampStart/RampEnd fall back to the page-level ramp values")]
    public void OmittedRampUsesPageDefaults()
    {
        var converter = new ScrollInterpolationConverter { Kind = ScrollValueKind.Double, FromLight = 0.0, ToLight = 1.0 };

        Convert(converter, 150, defaultRampStart: 100, defaultRampEnd: 200).Should().Be(0.5);
    }

    [Fact(DisplayName = "Colors lerp channel-wise; string endpoints parse")]
    public void ColorsLerp()
    {
        var converter = new ScrollInterpolationConverter { Kind = ScrollValueKind.Color, RampStart = 0, RampEnd = 100, FromLight = "#00000000", ToLight = Colors.White };

        var mid = (Color)Convert(converter, 50)!;
        mid.Alpha.Should().BeApproximately(0.5f, 0.01f);
        mid.Red.Should().BeApproximately(0.5f, 0.01f);
    }

    [Fact(DisplayName = "Brush targets accept color endpoints and produce a solid brush")]
    public void BrushTargetsProduceSolidBrush()
    {
        var converter = new ScrollInterpolationConverter { Kind = ScrollValueKind.Brush, RampStart = 0, RampEnd = 100, FromLight = Colors.Transparent, ToLight = new SolidColorBrush(Colors.Black) };

        var brush = (SolidColorBrush)Convert(converter, 100)!;
        brush.Color.Should().Be(Colors.Black);
    }

    [Fact(DisplayName = "Dark theme picks the dark endpoints, falling back to light when omitted")]
    public void DarkThemeSelectsEndpoints()
    {
        var converter = new ScrollInterpolationConverter
        {
            Kind = ScrollValueKind.Color,
            RampStart = 0,
            RampEnd = 100,
            FromLight = Colors.White,
            ToLight = Colors.Black,
            ToDark = Colors.Red
        };

        ((Color)Convert(converter, 100, theme: AppTheme.Dark)!).Should().Be(Colors.Red);

        // FromDark omitted: falls back to FromLight.
        ((Color)Convert(converter, 0, theme: AppTheme.Dark)!).Should().Be(Colors.White);

        ((Color)Convert(converter, 100)!).Should().Be(Colors.Black);
    }

    [Fact(DisplayName = "A zero-length window snaps at the threshold")]
    public void ZeroLengthWindowSnaps()
    {
        var converter = new ScrollInterpolationConverter { Kind = ScrollValueKind.Double, RampStart = 100, RampEnd = 100, FromLight = 0.0, ToLight = 1.0 };

        Convert(converter, 99).Should().Be(0.0);
        Convert(converter, 100).Should().Be(1.0);
    }

    [Fact(DisplayName = "KindFor maps numeric, Color and Brush targets and rejects others")]
    public void KindForMapsTargetTypes()
    {
        ScrollInterpolationConverter.KindFor(typeof(double)).Should().Be(ScrollValueKind.Double);
        ScrollInterpolationConverter.KindFor(typeof(Color)).Should().Be(ScrollValueKind.Color);
        ScrollInterpolationConverter.KindFor(typeof(Brush)).Should().Be(ScrollValueKind.Brush);
        ScrollInterpolationConverter.KindFor(typeof(string)).Should().BeNull();
    }
}
