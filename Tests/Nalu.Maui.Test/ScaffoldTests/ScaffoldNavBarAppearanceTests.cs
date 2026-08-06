namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// The per-property appearance merge (§5.2 revision): each
/// <see cref="ScaffoldNavBarAppearance"/> property resolves independently through the
/// page → area → scaffold chain via UNSET detection — a page-level appearance is a delta,
/// not a replacement.
/// </summary>
public class ScaffoldNavBarAppearanceTests
{
    private static T Resolve<T>(
        BindableProperty property,
        ScaffoldNavBarAppearance? page,
        ScaffoldNavBarAppearance? area,
        ScaffoldNavBarAppearance? scaffold,
        T fallback
    ) => ScaffoldNavBarAppearance.Resolve(property, page, area, scaffold, fallback);

    [Fact(DisplayName = "Resolve, given no appearance in the chain, returns the fallback")]
    public void ResolveGivenNoAppearanceReturnsFallback()
    {
        Resolve<Color?>(ScaffoldNavBarAppearance.ForegroundProperty, null, null, null, null).Should().BeNull();
        Resolve(ScaffoldNavBarAppearance.OpacityProperty, null, null, null, 1.0).Should().Be(1.0);
    }

    [Fact(DisplayName = "Resolve, given appearances with unset properties, returns the fallback")]
    public void ResolveGivenUnsetPropertiesReturnsFallback()
    {
        var page = new ScaffoldNavBarAppearance();
        var scaffold = new ScaffoldNavBarAppearance();

        Resolve<Brush?>(ScaffoldNavBarAppearance.BackgroundProperty, page, null, scaffold, null).Should().BeNull();
        Resolve(ScaffoldNavBarAppearance.OffsetYProperty, page, null, scaffold, 0.0).Should().Be(0.0);
    }

    [Fact(DisplayName = "Resolve, given a page-level value, beats area and scaffold values")]
    public void ResolveGivenPageValueBeatsOuterLevels()
    {
        var page = new ScaffoldNavBarAppearance { Opacity = 0.25 };
        var area = new ScaffoldNavBarAppearance { Opacity = 0.5 };
        var scaffold = new ScaffoldNavBarAppearance { Opacity = 0.75 };

        Resolve(ScaffoldNavBarAppearance.OpacityProperty, page, area, scaffold, 1.0).Should().Be(0.25);
    }

    [Fact(DisplayName = "Resolve, given a page-level delta, resolves unset properties from outer levels per property")]
    public void ResolveGivenPageDeltaMergesPerProperty()
    {
        var scaffoldBackground = new SolidColorBrush(Colors.White);
        var page = new ScaffoldNavBarAppearance { Opacity = 0.5 };
        var area = new ScaffoldNavBarAppearance { Foreground = Colors.Red };
        var scaffold = new ScaffoldNavBarAppearance { Background = scaffoldBackground, Foreground = Colors.Black };

        Resolve(ScaffoldNavBarAppearance.OpacityProperty, page, area, scaffold, 1.0).Should().Be(0.5);
        Resolve<Color?>(ScaffoldNavBarAppearance.ForegroundProperty, page, area, scaffold, null).Should().Be(Colors.Red);
        Resolve<Brush?>(ScaffoldNavBarAppearance.BackgroundProperty, page, area, scaffold, null).Should().BeSameAs(scaffoldBackground);
    }

    [Fact(DisplayName = "Resolve, given a value explicitly set to the property default, still wins over outer levels")]
    public void ResolveGivenExplicitDefaultValueWins()
    {
        // Unset detection, not value comparison: opacity 1.0 IS the bindable default, but an
        // explicit assignment must shadow the scaffold-level 0.5.
        var page = new ScaffoldNavBarAppearance { Opacity = 1.0 };
        var scaffold = new ScaffoldNavBarAppearance { Opacity = 0.5 };

        Resolve(ScaffoldNavBarAppearance.OpacityProperty, page, null, scaffold, 1.0).Should().Be(1.0);
    }

    [Fact(DisplayName = "Resolve, given a cleared property, falls back to outer levels again")]
    public void ResolveGivenClearedPropertyFallsBack()
    {
        var page = new ScaffoldNavBarAppearance { Foreground = Colors.White };
        var scaffold = new ScaffoldNavBarAppearance { Foreground = Colors.Black };

        Resolve<Color?>(ScaffoldNavBarAppearance.ForegroundProperty, page, null, scaffold, null).Should().Be(Colors.White);

        page.ClearValue(ScaffoldNavBarAppearance.ForegroundProperty);

        Resolve<Color?>(ScaffoldNavBarAppearance.ForegroundProperty, page, null, scaffold, null).Should().Be(Colors.Black);
    }

    [Fact(DisplayName = "GetNavBarAppearanceChain resolves page, current area and scaffold attachments")]
    public void ChainResolvesAttachments()
    {
        var scaffold = new Scaffold();
        var area = new ScaffoldArea();
        scaffold.Areas.Add(area);
        scaffold.CurrentArea = area;

        var page = new ContentPage();
        var pageAppearance = new ScaffoldNavBarAppearance();
        var areaAppearance = new ScaffoldNavBarAppearance();
        var scaffoldAppearance = new ScaffoldNavBarAppearance();

        Scaffold.SetNavBarAppearance(page, pageAppearance);
        Scaffold.SetNavBarAppearance(area, areaAppearance);
        Scaffold.SetNavBarAppearance(scaffold, scaffoldAppearance);

        var chain = scaffold.GetNavBarAppearanceChain(page);

        chain.Page.Should().BeSameAs(pageAppearance);
        chain.Area.Should().BeSameAs(areaAppearance);
        chain.Scaffold.Should().BeSameAs(scaffoldAppearance);
    }

    [Fact(DisplayName = "An attached appearance inherits the element's binding context")]
    public void AttachedAppearanceInheritsBindingContext()
    {
        var page = new ContentPage();
        var appearance = new ScaffoldNavBarAppearance();

        Scaffold.SetNavBarAppearance(page, appearance);
        appearance.BindingContext.Should().BeNull();

        var model = new object();
        page.BindingContext = model;
        appearance.BindingContext.Should().BeSameAs(model);

        // Attaching to an element that already has a context propagates immediately.
        var otherPage = new ContentPage { BindingContext = model };
        var otherAppearance = new ScaffoldNavBarAppearance();
        Scaffold.SetNavBarAppearance(otherPage, otherAppearance);
        otherAppearance.BindingContext.Should().BeSameAs(model);
    }
}
