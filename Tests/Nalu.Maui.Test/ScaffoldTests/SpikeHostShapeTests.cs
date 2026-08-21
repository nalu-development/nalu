namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// SPIKE (navbar-per-page plan §2.1, step 1): go/no-go on the <c>ScaffoldPageHost</c> tree shape.
/// These tests PIN THE OBSERVED MAUI BEHAVIOUR the decision rests on — they are evidence, not
/// production contracts.
/// </summary>
public class SpikeHostShapeTests
{
    private sealed class PlainElementHost : Element;

    private sealed class PageHost : Page;

    [Fact(DisplayName = "Option A is impossible: a Page rejects any non-Page Element parent")]
    public void PlainElementParentIsRejected()
    {
        var host = new PlainElementHost();

        var act = () => host.AddLogicalChild(new ContentPage());

        act.Should()
           .Throw<InvalidOperationException>()
           .WithMessage("Parent of a Page must also be a Page",
               "Page.OnParentSet allows only Application/Window/Page/BaseShellItem/null");
    }

    [Fact(DisplayName = "Option A': a Page-derived host keeps window resolution, the scaffold walk and style isolation")]
    public void PageDerivedHostWorks()
    {
        var scaffold = new Scaffold();
        _ = new Window { Page = scaffold };

        var host = new PageHost();
        var page = new ContentPage
        {
            Resources = new ResourceDictionary
            {
                new Style(typeof(Label)) { Setters = { new Setter { Property = Label.TextColorProperty, Value = Colors.Magenta } } }
            }
        };

        var pageLabel = new Label();
        page.Content = pageLabel;

        scaffold.AddLogicalChild(host);
        host.AddLogicalChild(page);

        page.Parent.Should().BeSameAs(host);
        page.GetScaffold().Should().BeSameAs(scaffold, "the ancestor walk is unaffected by the extra node");
        page.Window.Should().NotBeNull("Element.Window still climbs the chain");

        // The bar is a SIBLING of the page: outside the page's resource scope.
        var barLabel = new Label();
        host.AddLogicalChild(new VerticalStackLayout { Children = { barLabel } });

        pageLabel.TextColor.Should().Be(Colors.Magenta, "page content is inside the page's resource scope");
        barLabel.TextColor.Should().NotBe(Colors.Magenta, "chrome parented to the host escapes page styles");
    }

    [Fact(DisplayName = "Option B costs style isolation: a page implicit style DOES restyle bar content")]
    public void OptionBLeaksPageStylesIntoTheBar()
    {
        var scaffold = new Scaffold();
        var page = new ContentPage
        {
            Resources = new ResourceDictionary
            {
                new Style(typeof(Label)) { Setters = { new Setter { Property = Label.TextColorProperty, Value = Colors.Magenta } } }
            }
        };
        scaffold.AddLogicalChild(page);

        // Option B parents the nav bar host to the PAGE.
        var barLabel = new Label();
        page.AddLogicalChild(new VerticalStackLayout { Children = { barLabel } });

        barLabel.TextColor.Should().Be(Colors.Magenta,
            "DOCUMENTED REGRESSION: under Option B an app's page-level implicit style reaches library chrome");
    }
}
