namespace Nalu.Internals;

/// <summary>
/// One "screen": a page in a navigation stack plus the library-owned chrome that belongs to
/// THAT page — its <see cref="ScaffoldNavBarContext"/> and (later) its nav bar host. Created
/// when the page enters a stack, disposed when it leaves; a stacked page that is not current
/// keeps its host alive, exactly as it keeps its own state.
/// </summary>
/// <remarks>
/// <para>
/// NOT an element: hosts live in a <see cref="Scaffold"/>-owned dictionary keyed by page, and
/// the element tree is left exactly as it was — pages stay direct logical children of the
/// scaffold (<c>page.Parent</c> is the scaffold) and the nav bar host stays a logical child of
/// the scaffold too. That is a deliberate reversal of this design's first sketch, which put a
/// host ELEMENT between the scaffold and the page so that per-page context bindings could
/// resolve it as an ancestor. Two findings killed it:
/// </para>
/// <para>
/// (1) <c>Page.OnParentSet</c> rejects any parent that is not an Application, Window, Page or
/// BaseShellItem — "Parent of a Page must also be a Page" — so a non-Page host simply cannot be
/// a page's logical parent. (2) The documented fallback (re-parenting the nav bar host to the
/// PAGE) puts library chrome inside the page's resource scope, where an app's page-level
/// implicit style silently restyles the bar. Both are pinned by <c>SpikeHostShapeTests</c>.
/// </para>
/// <para>
/// What made the tree change unnecessary is that per-page context resolution no longer needs an
/// ancestor of a MAUI-blessed type: <c>NavBarContextRelay</c> performs OUR OWN ancestry
/// walk and recognises both carriers — the bar host (bar content, a TitleView included) and the
/// page (page content) — in a single pass. Bindings therefore resolve the right page's context
/// from either side with the tree untouched.
/// </para>
/// </remarks>
internal sealed class ScaffoldPageHost : IDisposable
{
    /// <summary>The page this host owns the chrome for.</summary>
    public Page Page { get; }

    /// <summary>The navigation stack the page belongs to (fixed: a page never changes stack).</summary>
    public ScaffoldRoot Root { get; }

    /// <summary>The scaffold hosting the stack.</summary>
    public Scaffold Scaffold { get; }

    /// <summary>This page's nav bar state — the binding context of the bar mounted for it.</summary>
    public ScaffoldNavBarContext Context { get; }

    /// <summary>
    /// This page's nav bar realization, once the page has resolved a bar view. One per page —
    /// never shared, because two pages can be on screen at once during a transition, each
    /// showing its own bar. Null while the page resolves no bar view.
    /// </summary>
    public ScaffoldNavBarHost? NavBarHost { get; private set; }

    /// <summary>The template this page's bar was realized from, so a template swap rebuilds it.</summary>
    private DataTemplate? _barTemplate;

    /// <summary>This page's OWN bar view, realized from the resolved template. Never shared.</summary>
    private View? _barView;

    public ScaffoldPageHost(Scaffold scaffold, ScaffoldRoot root, Page page)
    {
        Scaffold = scaffold;
        Root = root;
        Page = page;
        Context = new ScaffoldNavBarContext(scaffold, page, root);
        Context.Refresh();
    }

    /// <summary>
    /// Whether the page's bar is presented: a bar view resolves for it AND the page wants one.
    /// </summary>
    public bool IsNavBarVisible
        => Scaffold.ResolveNavBarTemplate(Page) is not null && Scaffold.GetIsNavBarVisible(Page);

    /// <summary>
    /// This page's bar view, realized from the resolved template and cached. A template change
    /// (a runtime swap, XAML hot reload) rebuilds it; the template is never shared as an
    /// instance, which is the whole reason the attached property is a template.
    /// </summary>
    private View? ResolveBarView()
    {
        var template = Scaffold.ResolveNavBarTemplate(Page);

        if (template is null)
        {
            _barTemplate = null;
            _barView = null;

            return null;
        }

        if (!ReferenceEquals(_barTemplate, template) || _barView is null)
        {
            _barTemplate = template;
            _barView = template.CreateContent() as View
                       ?? throw new InvalidOperationException(
                           $"{nameof(Scaffold)}.NavBarTemplate must create a {nameof(View)}.");
        }

        return _barView;
    }

    /// <summary>
    /// Whether the page takes the bar's footprint as a top inset. Overlap mode still presents
    /// the bar — it just draws over content that lays out from the top edge.
    /// </summary>
    public bool WantsNavBarInset => IsNavBarVisible && !Scaffold.GetNavBarOverlapsContent(Page);

    /// <summary>
    /// Recomputes the stack-dependent context values for this page (back/close/drawer buttons)
    /// and re-resolves its bar. The presenters call it for the INCOMING page on every
    /// synchronization; a page on its way out keeps the state it had, because that is what it
    /// is still showing.
    /// </summary>
    public void Refresh() => Context.Refresh();

    /// <summary>
    /// This page's own realized bar view. Exposed for the platform layers that mount the view
    /// directly rather than through <see cref="EnsureNavBarHost"/>.
    /// </summary>
    public View? BarView => ResolveBarView();

    /// <summary>
    /// Creates or updates this page's bar host from the resolved bar view (page → area →
    /// scaffold → the built-in default, which is instantiated PER HOST), tearing it down if the
    /// page resolves no bar at all. Returns the host, or null when the page has no bar.
    /// </summary>
    public ScaffoldNavBarHost? EnsureNavBarHost()
    {

        if (ResolveBarView() is not { } barView)
        {
            DisposeNavBarHost();

            return null;
        }

        var host = NavBarHost ??= new ScaffoldNavBarHost(Scaffold) { Context = Context };
        host.SetBar(barView);
        host.UpdateSources(Page);

        return host;
    }

    /// <summary>
    /// The element tree reflects PRESENTED chrome: the bar host is a logical child of the
    /// scaffold while this page's bar is visible and detached while it is hidden (the platform
    /// view stays alive offscreen either way). A sibling of the page, never a child of it —
    /// library chrome must stay out of the page's resource scope.
    /// </summary>
    public void SetNavBarAttached(bool attached)
    {
        if (NavBarHost is not { } host)
        {
            return;
        }

        switch (attached)
        {
            case true when host.Parent is null:
                Scaffold.AddLogicalChild(host);

                break;

            case false when ReferenceEquals(host.Parent, Scaffold):
                Scaffold.RemoveLogicalChild(host);

                break;
        }
    }

    private void DisposeNavBarHost()
    {
        if (NavBarHost is not { } host)
        {
            return;
        }

        SetNavBarAttached(false);
        host.Dispose();
        NavBarHost = null;
    }

    public void Dispose()
    {
        DisposeNavBarHost();
        Context.Detach();
    }
}
