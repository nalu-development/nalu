namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// The nav bar appearance resolution: each attached property resolves INDEPENDENTLY through
/// page → current area → scaffold by UNSET detection, so a page-level value is a delta, not a
/// replacement. The title color is the one exception — it resolves level by level.
/// </summary>
public class ScaffoldNavBarAppearanceTests
{
    private static (Scaffold Scaffold, ScaffoldArea Area, ContentPage Page) Build()
    {
        var area = new ScaffoldArea();
        var scaffold = new Scaffold();
        scaffold.Areas.Add(area);
        scaffold.CurrentArea = area;

        return (scaffold, area, new ContentPage());
    }

    [Fact(DisplayName = "With nothing set, resolution yields the property's declared default")]
    public void ResolveGivenNothingSetYieldsTheDefault()
    {
        var (scaffold, _, page) = Build();

        scaffold.ResolveNavBarValue(page, Scaffold.NavBarOpacityProperty).Should().Be(1.0);
        scaffold.ResolveNavBarValue(page, Scaffold.NavBarOffsetYProperty).Should().Be(0.0);
        scaffold.ResolveNavBarValue(page, Scaffold.NavBarForegroundProperty).Should().BeNull();
        scaffold.ResolveNavBarValue(page, Scaffold.NavBarBackgroundProperty).Should().BeOfType<SolidColorBrush>();
    }

    [Fact(DisplayName = "A page value beats the area's, which beats the scaffold's")]
    public void ResolveIsMostSpecificWins()
    {
        var (scaffold, area, page) = Build();

        Scaffold.SetNavBarOpacity(scaffold, 0.75);
        scaffold.ResolveNavBarValue(page, Scaffold.NavBarOpacityProperty).Should().Be(0.75);

        Scaffold.SetNavBarOpacity(area, 0.5);
        scaffold.ResolveNavBarValue(page, Scaffold.NavBarOpacityProperty).Should().Be(0.5);

        Scaffold.SetNavBarOpacity(page, 0.25);
        scaffold.ResolveNavBarValue(page, Scaffold.NavBarOpacityProperty).Should().Be(0.25);
    }

    [Fact(DisplayName = "Each property resolves independently: a page value is a delta, not a replacement")]
    public void ResolveIsPerProperty()
    {
        var (scaffold, area, page) = Build();
        var scaffoldBackground = new SolidColorBrush(Colors.White);

        Scaffold.SetNavBarBackground(scaffold, scaffoldBackground);
        Scaffold.SetNavBarForeground(scaffold, Colors.Black);
        Scaffold.SetNavBarForeground(area, Colors.Red);
        Scaffold.SetNavBarOpacity(page, 0.5);

        scaffold.ResolveNavBarValue(page, Scaffold.NavBarOpacityProperty).Should().Be(0.5, "the page set it");
        scaffold.ResolveNavBarValue(page, Scaffold.NavBarForegroundProperty).Should().Be(Colors.Red, "the area is the most specific level that set it");
        scaffold.ResolveNavBarValue(page, Scaffold.NavBarBackgroundProperty).Should().BeSameAs(scaffoldBackground, "nothing nearer set it");
    }

    [Fact(DisplayName = "A value set explicitly TO the default still shadows outer levels")]
    public void ResolveUsesUnsetDetectionNotValueComparison()
    {
        var (scaffold, _, page) = Build();

        // 1.0 IS the declared default, but an explicit assignment must beat the scaffold's 0.5 —
        // which is why resolution asks IsSet rather than comparing values.
        Scaffold.SetNavBarOpacity(scaffold, 0.5);
        Scaffold.SetNavBarOpacity(page, 1.0);

        scaffold.ResolveNavBarValue(page, Scaffold.NavBarOpacityProperty).Should().Be(1.0);
    }

    [Fact(DisplayName = "A style-applied value counts as set, so it beats outer levels")]
    public void ResolveTreatsStyleValuesAsSet()
    {
        var (scaffold, _, page) = Build();

        Scaffold.SetNavBarOpacity(scaffold, 0.5);

        page.Style = new Style(typeof(ContentPage))
        {
            Setters = { new Setter { Property = Scaffold.NavBarOpacityProperty, Value = 0.25 } }
        };

        scaffold.ResolveNavBarValue(page, Scaffold.NavBarOpacityProperty).Should().Be(0.25);
    }

    /// <summary>
    /// The shape every real app has: <c>AppScaffold</c> derives from <see cref="Scaffold"/>, and
    /// the app-wide chrome arrives as an IMPLICIT style. MAUI matches implicit styles on the
    /// EXACT type, so such a style never reaches the app's scaffold unless it opts in — and the
    /// bar silently keeps the library defaults, immune to everything the style says, theme
    /// changes included. Nothing else here catches that: the other tests assign values (or a
    /// style) to the instance directly, which always applies.
    /// </summary>
    [Fact(DisplayName = "An implicit style reaches a DERIVED scaffold only with ApplyToDerivedTypes")]
    public void ImplicitStyleReachesADerivedScaffoldOnlyWhenItOptsIn()
    {
        static double ResolveThroughImplicitStyle(bool applyToDerivedTypes)
        {
            var scaffold = new DerivedScaffold();
            scaffold.Areas.Add(new ScaffoldArea());

            scaffold.Resources = new ResourceDictionary
            {
                new Style(typeof(Scaffold))
                {
                    ApplyToDerivedTypes = applyToDerivedTypes,
                    Setters = { new Setter { Property = Scaffold.NavBarOpacityProperty, Value = 0.25 } }
                }
            };

            return (double) scaffold.ResolveNavBarValue(new ContentPage(), Scaffold.NavBarOpacityProperty)!;
        }

        ResolveThroughImplicitStyle(false).Should().Be(1.0, "an exact-type implicit style skips the subclass, leaving the declared default");
        ResolveThroughImplicitStyle(true).Should().Be(0.25, "ApplyToDerivedTypes is what carries app-wide chrome onto the app's own scaffold");
    }

    private sealed class DerivedScaffold : Scaffold;

    [Fact(DisplayName = "The title color takes the first level that set EITHER title or foreground")]
    public void ResolveTitleForegroundIsLevelByLevel()
    {
        var (scaffold, area, page) = Build();

        scaffold.ResolveNavBarTitleForeground(page).Should().BeNull("nothing set anywhere");

        Scaffold.SetNavBarTitleForeground(scaffold, Colors.Blue);
        scaffold.ResolveNavBarTitleForeground(page).Should().Be(Colors.Blue);

        // The area sets only a foreground: it still decides the title, and the scaffold's title
        // color does NOT leak past it — that is what "level by level" buys over per-property.
        Scaffold.SetNavBarForeground(area, Colors.Red);
        scaffold.ResolveNavBarTitleForeground(page).Should().Be(Colors.Red);

        // At the same level, the title channel wins over the general one.
        Scaffold.SetNavBarForeground(page, Colors.Green);
        Scaffold.SetNavBarTitleForeground(page, Colors.Gold);
        scaffold.ResolveNavBarTitleForeground(page).Should().Be(Colors.Gold);
    }
}
