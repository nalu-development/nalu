using System.Reflection;
using Nalu.Internals;

namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// The per-page context binding seam: <c>NavBarContextRelay</c> resolves the context of the page
/// the bound element belongs to, from page content and from bar content alike, and the ad-hoc
/// typed switch covers the context's whole surface.
/// </summary>
public class NavBarContextBindingTests
{
    public NavBarContextBindingTests() => DispatcherProvider.SetCurrent(new DispatcherProviderStub());

    private static (Scaffold Scaffold, ScaffoldRoot Root) BuildScaffold()
    {
        var root = new ScaffoldRoot();
        var area = new ScaffoldArea();
        area.Roots.Add(root);
        var scaffold = new Scaffold();
        scaffold.Areas.Add(area);

        return (scaffold, root);
    }

    [Fact(DisplayName = "Every public ScaffoldNavBarContext property compiles to a typed binding")]
    public void TypedSwitchCoversTheWholeContext()
    {
        var uncovered = typeof(ScaffoldNavBarContext)
                        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                        .Select(property => property.Name)
                        .Where(name => !NavBarContextBindings.CoversTypedPath(name))
                        .ToArray();

        uncovered.Should()
                 .BeEmpty("a context property with no case silently falls back to the reflection path, "
                          + "which is the trimming/AOT hazard the typed switch exists to avoid");
    }

    [Fact(DisplayName = "Page content resolves its OWN page's context, not the current page's")]
    public void PageContentResolvesItsOwnPage()
    {
        var (scaffold, root) = BuildScaffold();

        var first = new ContentPage { Title = "First" };
        var second = new ContentPage { Title = "Second" };
        var firstLabel = new Label();
        var secondLabel = new Label();
        first.Content = firstLabel;
        second.Content = secondLabel;

        scaffold.AttachPage(root, first);
        scaffold.AttachPage(root, second);

        firstLabel.SetBinding(Label.TextProperty, NavBarBindings.Create(firstLabel, nameof(ScaffoldNavBarContext.Title)));
        secondLabel.SetBinding(Label.TextProperty, NavBarBindings.Create(secondLabel, nameof(ScaffoldNavBarContext.Title)));

        firstLabel.Text.Should().Be("First");
        secondLabel.Text.Should().Be("Second", "two pages are alive during a transition and each reads its own state");
    }

    [Fact(DisplayName = "A binding declared before parenting resolves once the element is in the page")]
    public void ResolvesAfterLateParenting()
    {
        var (scaffold, root) = BuildScaffold();
        var page = new ContentPage { Title = "Late" };
        scaffold.AttachPage(root, page);

        var label = new Label();
        label.SetBinding(Label.TextProperty, NavBarBindings.Create(label, nameof(ScaffoldNavBarContext.Title)));
        label.Text.Should().BeNull("nothing resolvable yet");

        page.Content = new VerticalStackLayout { Children = { label } };

        label.Text.Should().Be("Late");

        page.Title = "Renamed";
        label.Text.Should().Be("Renamed", "the page's own context stays live");
    }

    [Fact(DisplayName = "Bar content resolves the bar host's context (a hosted title view included)")]
    public void BarContentResolvesTheBarHostContext()
    {
        var (scaffold, root) = BuildScaffold();
        var page = new ContentPage { Title = "Barred" };
        scaffold.AttachPage(root, page);

        var barHost = new ScaffoldNavBarHost(scaffold) { Context = scaffold.GetPageHost(page)!.Context };
        var titleView = new Label();
        barHost.SetBar(new VerticalStackLayout { Children = { titleView } });
        scaffold.AddLogicalChild(barHost);

        titleView.SetBinding(Label.TextProperty, NavBarBindings.Create(titleView, nameof(ScaffoldNavBarContext.Title)));

        titleView.Text.Should().Be("Barred", "bar content climbs to the bar host, never to the page");
    }

    [Fact(DisplayName = "Re-pointing a bar host at another page's context re-resolves its bindings")]
    public void BarHostContextSwapReresolves()
    {
        var (scaffold, root) = BuildScaffold();
        var first = new ContentPage { Title = "First" };
        var second = new ContentPage { Title = "Second" };
        scaffold.AttachPage(root, first);
        scaffold.AttachPage(root, second);

        var barHost = new ScaffoldNavBarHost(scaffold) { Context = scaffold.GetPageHost(first)!.Context };
        var titleView = new Label();
        barHost.SetBar(new VerticalStackLayout { Children = { titleView } });
        scaffold.AddLogicalChild(barHost);
        titleView.SetBinding(Label.TextProperty, NavBarBindings.Create(titleView, nameof(ScaffoldNavBarContext.Title)));
        titleView.Text.Should().Be("First");

        barHost.Context = scaffold.GetPageHost(second)!.Context;

        titleView.Text.Should().Be("Second", "no ancestry change happens here — the relay follows the host's context");
    }

    [Fact(DisplayName = "Deep reflection paths still resolve through the relay")]
    public void DeepPathsStillWork()
    {
        var (scaffold, root) = BuildScaffold();
        var page = new ContentPage { Title = "Deep" };
        var label = new Label();
        page.Content = label;
        scaffold.AttachPage(root, page);

        // CurrentPage.Title: two hops past the context — beyond anything a typed map could express.
        label.SetBinding(Label.TextProperty, NavBarBindings.Create(label, "CurrentPage.Title"));

        label.Text.Should().Be("Deep", "the documented CurrentPage.BindingContext.X escape hatch must survive");
    }

    [Fact(DisplayName = "FindNavBarContext resolves per page, and falls back to the current page outside one")]
    public void FindNavBarContextIsPerPage()
    {
        var (scaffold, root) = BuildScaffold();
        var first = new ContentPage();
        var second = new ContentPage();
        var firstLabel = new Label();
        first.Content = firstLabel;
        scaffold.AttachPage(root, first);
        scaffold.AttachPage(root, second);

        Scaffold.FindNavBarContext(firstLabel).Should().BeSameAs(scaffold.GetPageHost(first)!.Context);

        // An element parented to the scaffold but to no page: the current page's context.
        var loose = new Label();
        scaffold.AddLogicalChild(loose);
        Scaffold.FindNavBarContext(loose).Should().BeSameAs(scaffold.NavBarContext);

        Scaffold.FindNavBarContext(new Label()).Should().BeNull("not in a scaffold's tree");
    }

    [Fact(DisplayName = "Scaffold.NavBarContext is never null before a page is presented")]
    public void ScaffoldContextHasADetachedFallback()
    {
        var (scaffold, _) = BuildScaffold();

        // The public contract is non-nullable; with no current page it yields a detached
        // context rather than null. (The forwarding itself is covered where the engine runs —
        // see ScaffoldProxyTests.)
        scaffold.NavBarContext.Should().NotBeNull();
        scaffold.NavBarContext.Should().BeSameAs(scaffold.NavBarContext, "the fallback is stable");
    }

    [Fact(DisplayName = "A popped page's host is disposed and its context released")]
    public void PoppedPageHostIsDisposed()
    {
        var (scaffold, root) = BuildScaffold();
        var page = new ContentPage { Title = "Transient" };
        scaffold.AttachPage(root, page);

        var context = scaffold.GetPageHost(page)!.Context;
        context.Title.Should().Be("Transient");

        scaffold.DetachPage(page);

        scaffold.GetPageHost(page).Should().BeNull();

        page.Title = "Changed";
        context.Title.Should().Be("Transient", "the detached context stops observing its page");
    }
}
