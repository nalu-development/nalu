namespace Nalu.Maui.Test.ScaffoldTests;

/// <summary>
/// Guards the scaffold's place in the MAUI page tree as analytics SDKs read it: a plain
/// <see cref="Page"/> that exposes the presented page through <see cref="IPageContainer{T}"/>.
/// Pendo's MAUI plugin (and tree-walking SDKs alike) tests <c>is ContentPage</c> BEFORE
/// <c>is IPageContainer&lt;Page&gt;</c>; a <c>ContentPage</c>-derived scaffold would be reported
/// as the screen itself forever. See conceptual_docs/scaffold-analytics.md.
/// </summary>
public class ScaffoldPageTreeTests
{
    [Fact(DisplayName = "Scaffold is a plain Page (not ContentPage/TemplatedPage) exposing IPageContainer<Page>")]
    public void ScaffoldIsAPlainPageContainer()
    {
        var scaffold = new Scaffold();

        scaffold.Should().BeAssignableTo<Page>();
        scaffold.Should().NotBeAssignableTo<TemplatedPage>("tree-walking analytics SDKs test ContentPage/TemplatedPage before IPageContainer<Page> and would stop at the scaffold");
        scaffold.Should().BeAssignableTo<IPageContainer<Page>>();
        scaffold.Should().NotBeAssignableTo<Shell>();
    }
}
