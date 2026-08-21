using System.ComponentModel;

namespace Nalu.Internals;

/// <summary>
/// The binding source behind <see cref="NavBarBindingExtension"/>, <see cref="NavBarBindings"/>
/// and the scroll-value extensions: a relay whose <see cref="Context"/> is resolved by OUR OWN
/// ancestry walk, so a binding declared anywhere reaches the context of the page it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// MAUI's <c>RelativeBindingSource(FindAncestor, T)</c> cannot express this: the two carriers of
/// a page's context sit on different branches — bar content climbs to the
/// <see cref="ScaffoldNavBarHost"/>, page content climbs to the <see cref="Page"/> — and neither
/// is an ancestor of the other. <c>RelativeBindingSource</c> is sealed and <c>BindingBase</c>
/// cannot be derived outside MAUI, so the walk is reimplemented here over the public
/// <see cref="Element.ParentChanged"/> event, which is what MAUI's own resolver subscribes to.
/// </para>
/// <para>
/// Being a plain source object (and NOT a <c>BindableObject</c>, which would demand a dispatcher)
/// is what preserves full reflection binding paths: <c>Context.CurrentPage.BindingContext.X</c>
/// still works, where a typed-getter map over a fixed property set could not express it.
/// </para>
/// <para>
/// Retention: every resolution unsubscribes the previous chain before walking again, so a
/// subscription taken on the app-lifetime <see cref="Scaffold"/> during an unresolved walk is
/// dropped the moment the target is re-parented or detached. The relay is referenced by the
/// binding it serves and references the target back — one cycle, collected together.
/// </para>
/// </remarks>
internal sealed class NavBarContextRelay : INotifyPropertyChanged
{
    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private readonly List<Element> _chain = [];
    private Element? _target;
    private ScaffoldNavBarHost? _observedBarHost;
    private Scaffold? _observedScaffold;
    private Page? _observedScaffoldPage;

    /// <summary>The resolved context, or null while the target is not yet in a scaffold's tree.</summary>
    public ScaffoldNavBarContext? Context
    {
        get;
        private set
        {
            if (!ReferenceEquals(field, value))
            {
                field = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Context)));
            }
        }
    }

    /// <summary>Starts resolving for the given binding target (idempotent).</summary>
    public void Attach(Element target)
    {
        if (ReferenceEquals(_target, target))
        {
            return;
        }

        _target = target;
        Resolve();
    }

    /// <summary>
    /// Walks up from the target, recognising both carriers in ONE pass: a
    /// <see cref="ScaffoldNavBarHost"/> answers for bar content (a hosted
    /// <see cref="Scaffold.TitleViewProperty"/> view included — it is parented into the bar), the
    /// nearest <see cref="Page"/> answers for page content. The page's context is looked up on
    /// the <see cref="Scaffold"/> that owns it, so a page with no host (not in a stack) falls
    /// back to whatever the scaffold is currently presenting.
    /// </summary>
    private void Resolve()
    {
        Unsubscribe();

        Page? page = null;

        for (var current = _target; current is not null; current = current.Parent)
        {
            _chain.Add(current);
            current.ParentChanged += OnAncestryChanged;

            switch (current)
            {
                case ScaffoldNavBarHost barHost:
                    // A bar host can be re-pointed at another page's context without any
                    // ancestry change (a bar view reused across pages): follow that too.
                    _observedBarHost = barHost;
                    barHost.PropertyChanged += OnBarHostPropertyChanged;
                    Context = barHost.Context;

                    return;

                // The scaffold IS a page: it must never be mistaken for a hosted one.
                case Scaffold scaffold:
                    // Chrome outside any page (tab bar, scaffold-level overlays) falls back to
                    // the CURRENT page's context — and must keep following it across
                    // navigations, which arrive as PropertyChanged(NavBarContext), not as an
                    // ancestry change.
                    _observedScaffold = scaffold;
                    _observedScaffoldPage = page;
                    scaffold.PropertyChanged += OnScaffoldPropertyChanged;
                    Context = ResolveScaffoldContext(scaffold, page);

                    return;

                case Page hostedPage:
                    page ??= hostedPage;

                    break;
            }
        }

        // Not in a scaffold's tree (yet): re-resolves when the ancestry changes.
        Context = null;
    }

    private static ScaffoldNavBarContext ResolveScaffoldContext(Scaffold scaffold, Page? page)
        => (page is not null ? scaffold.GetPageHost(page)?.Context : null) ?? scaffold.NavBarContext;

    private void OnAncestryChanged(object? sender, EventArgs e) => Resolve();

    private void OnScaffoldPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Scaffold.NavBarContext) && sender is Scaffold scaffold)
        {
            Context = ResolveScaffoldContext(scaffold, _observedScaffoldPage);
        }
    }

    private void OnBarHostPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScaffoldNavBarHost.Context) && sender is ScaffoldNavBarHost barHost)
        {
            Context = barHost.Context;
        }
    }

    private void Unsubscribe()
    {
        foreach (var element in _chain)
        {
            element.ParentChanged -= OnAncestryChanged;
        }

        _chain.Clear();

        if (_observedBarHost is not null)
        {
            _observedBarHost.PropertyChanged -= OnBarHostPropertyChanged;
            _observedBarHost = null;
        }

        if (_observedScaffold is not null)
        {
            _observedScaffold.PropertyChanged -= OnScaffoldPropertyChanged;
            _observedScaffold = null;
            _observedScaffoldPage = null;
        }
    }
}
