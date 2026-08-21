using System.Globalization;
using Nalu.Internals;

namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// The Brush leg of the scroll-value interpolations (<see cref="ScrollValueBrushInterpolator"/>):
/// solid ↔ solid, solid ↔ gradient and gradient ↔ gradient lerps (stop-offset union, geometry),
/// the reused-and-mutated single output instance with its fresh-instances escape hatch, and the
/// wiring through both value converters.
/// </summary>
public class ScrollValueBrushTests
{
    private static LinearGradientBrush Linear(Point start, Point end, params (Color Color, float Offset)[] stops)
        => new([.. stops.Select(s => new GradientStop(s.Color, s.Offset))], start, end);

    private static RadialGradientBrush Radial(Point center, double radius, params (Color Color, float Offset)[] stops)
        => new([.. stops.Select(s => new GradientStop(s.Color, s.Offset))], center, radius);

    private static object? Convert(ScrollInterpolationConverter converter, double offset, AppTheme theme = AppTheme.Light)
        => converter.Convert([offset, 0.0, 100.0, theme], typeof(object), null, CultureInfo.InvariantCulture);

    [Fact(DisplayName = "A solid pair reuses ONE output instance, mutated in place")]
    public void SolidPairReusesOneInstance()
    {
        var converter = new ScrollInterpolationConverter { Kind = ScrollValueKind.Brush, FromLight = Colors.Black, ToLight = Colors.White };

        var first = (SolidColorBrush)Convert(converter, 0)!;
        first.Color.Should().Be(Colors.Black);

        var mid = (SolidColorBrush)Convert(converter, 50)!;
        mid.Should().BeSameAs(first);
        mid.Color.Red.Should().BeApproximately(0.5f, 0.01f);

        ((SolidColorBrush)Convert(converter, 100)!).Should().BeSameAs(first);
        first.Color.Should().Be(Colors.White);
    }

    [Fact(DisplayName = "The escape hatch emits a fresh instance per evaluation")]
    public void EscapeHatchEmitsFreshInstances()
    {
        var interpolator = new ScrollValueBrushInterpolator { ReuseOverride = false };

        var first = interpolator.Materialize(Colors.Black, Colors.White, 0);
        var second = interpolator.Materialize(Colors.Black, Colors.White, 0.5);

        second.Should().NotBeSameAs(first);
        ((SolidColorBrush)second).Color.Red.Should().BeApproximately(0.5f, 0.01f);
    }

    [Fact(DisplayName = "Solid ↔ gradient: the solid side expands over the gradient's stops")]
    public void SolidToGradientExpands()
    {
        var interpolator = new ScrollValueBrushInterpolator();
        var gradient = Linear(new Point(0, 0), new Point(1, 0), (Colors.Black, 0f), (Colors.White, 1f));

        var atStart = (LinearGradientBrush)interpolator.Materialize(Colors.Red, gradient, 0);
        atStart.GradientStops.Select(s => s.Offset).Should().Equal(0f, 1f);
        atStart.GradientStops.Should().OnlyContain(s => s.Color.Equals(Colors.Red));

        var atEnd = (LinearGradientBrush)interpolator.Materialize(Colors.Red, gradient, 1);
        atEnd.Should().BeSameAs(atStart);
        atEnd.GradientStops[0].Color.Should().Be(Colors.Black);
        atEnd.GradientStops[1].Color.Should().Be(Colors.White);
        atEnd.StartPoint.Should().Be(new Point(0, 0));
        atEnd.EndPoint.Should().Be(new Point(1, 0));
    }

    [Fact(DisplayName = "Different stop counts/positions pair up on the union of offsets")]
    public void DifferentStopsPairOnTheUnion()
    {
        var interpolator = new ScrollValueBrushInterpolator();
        var from = Linear(new Point(0, 0), new Point(1, 0), (Colors.Black, 0f), (Colors.White, 1f));
        var to = Linear(new Point(0, 0), new Point(1, 0), (Colors.Red, 0f), (Colors.Green, 0.5f), (Colors.Blue, 1f));

        var atStart = (LinearGradientBrush)interpolator.Materialize(from, to, 0);
        atStart.GradientStops.Select(s => s.Offset).Should().Equal(0f, 0.5f, 1f);

        // The two-stop side is SAMPLED at 0.5: mid-gray, so adding the union stop changes nothing visually.
        atStart.GradientStops[1].Color.Red.Should().BeApproximately(0.5f, 0.01f);

        var atEnd = (LinearGradientBrush)interpolator.Materialize(from, to, 1);
        atEnd.GradientStops.Select(s => s.Color).Should().Equal(Colors.Red, Colors.Green, Colors.Blue);
    }

    [Fact(DisplayName = "Linear geometry lerps per side")]
    public void LinearGeometryLerps()
    {
        var interpolator = new ScrollValueBrushInterpolator();
        var from = Linear(new Point(0, 0), new Point(1, 0), (Colors.Black, 0f), (Colors.White, 1f));
        var to = Linear(new Point(0, 0), new Point(0, 1), (Colors.Black, 0f), (Colors.White, 1f));

        var mid = (LinearGradientBrush)interpolator.Materialize(from, to, 0.5);
        mid.EndPoint.Should().Be(new Point(0.5, 0.5));
    }

    [Fact(DisplayName = "Radial pairs lerp center and radius")]
    public void RadialPairsLerpCenterAndRadius()
    {
        var interpolator = new ScrollValueBrushInterpolator();
        var from = Radial(new Point(0, 0), 0.2, (Colors.Black, 0f), (Colors.White, 1f));
        var to = Radial(new Point(1, 1), 0.6, (Colors.White, 0f), (Colors.Black, 1f));

        var mid = (RadialGradientBrush)interpolator.Materialize(from, to, 0.5);
        mid.Center.Should().Be(new Point(0.5, 0.5));
        mid.Radius.Should().BeApproximately(0.4, 0.001);
        mid.GradientStops[0].Color.Red.Should().BeApproximately(0.5f, 0.01f);
    }

    [Fact(DisplayName = "Mismatched gradient types cannot interpolate")]
    public void MismatchedGradientTypesThrow()
    {
        var interpolator = new ScrollValueBrushInterpolator();
        var linear = Linear(new Point(0, 0), new Point(1, 0), (Colors.Black, 0f));
        var radial = Radial(new Point(0.5, 0.5), 0.5, (Colors.White, 0f));

        var act = () => interpolator.Materialize(linear, radial, 0);
        act.Should().Throw<InvalidOperationException>().WithMessage("*same gradient type*");
    }

    [Fact(DisplayName = "A gradient endpoint on a Color target is rejected")]
    public void GradientOnColorTargetThrows()
    {
        var converter = new ScrollInterpolationConverter
        {
            Kind = ScrollValueKind.Color,
            FromLight = Colors.Black,
            ToLight = Linear(new Point(0, 0), new Point(1, 0), (Colors.White, 1f))
        };

        var act = () => Convert(converter, 50);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Brush-typed target property*");
    }

    [Fact(DisplayName = "A theme switch rebuilds the plan and the output instance kind")]
    public void ThemeSwitchSwapsThePlan()
    {
        var converter = new ScrollInterpolationConverter
        {
            Kind = ScrollValueKind.Brush,
            FromLight = Colors.White,
            ToLight = Colors.Black,
            FromDark = Linear(new Point(0, 0), new Point(1, 0), (Colors.Red, 0f), (Colors.Blue, 1f)),
            ToDark = Linear(new Point(0, 0), new Point(1, 0), (Colors.Blue, 0f), (Colors.Red, 1f))
        };

        var light = Convert(converter, 0);
        light.Should().BeOfType<SolidColorBrush>();

        var dark = Convert(converter, 0, AppTheme.Dark);
        dark.Should().BeOfType<LinearGradientBrush>();
        ((LinearGradientBrush)dark!).GradientStops[0].Color.Should().Be(Colors.Red);

        Convert(converter, 0).Should().BeOfType<SolidColorBrush>();
    }

    [Fact(DisplayName = "ScrollDirectionValue steps a solid ↔ gradient background through one instance")]
    public void DirectionValueLerpsGradients()
    {
        var animator = new ScrollDirectionAnimator();

        var converter = new ScrollDirectionInterpolationConverter
        {
            Kind = ScrollValueKind.Brush,
            ActivateThreshold = 100,
            DeactivateThreshold = 50,
            ActivateDuration = 0,
            Animator = animator,
            DeactivatedLight = Colors.LightGray,
            ActivatedLight = Linear(new Point(0, 0), new Point(1, 0), (Colors.Red, 0f), (Colors.Blue, 1f))
        };

        object? DirectionConvert(double offset)
            => converter.Convert([offset, AppTheme.Light, animator.Progress], typeof(object), null, CultureInfo.InvariantCulture);

        var resting = (LinearGradientBrush)DirectionConvert(0)!;
        resting.GradientStops.Should().OnlyContain(s => s.Color.Equals(Colors.LightGray));

        var activated = (LinearGradientBrush)DirectionConvert(150)!;
        activated.Should().BeSameAs(resting);
        activated.GradientStops[0].Color.Should().Be(Colors.Red);
        activated.GradientStops[1].Color.Should().Be(Colors.Blue);

        ((LinearGradientBrush)DirectionConvert(80)!).GradientStops.Should().OnlyContain(s => s.Color.Equals(Colors.LightGray));
    }
}
