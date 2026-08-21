namespace Nalu;

/// <summary>
/// The live navigation stack of a <see cref="ScaffoldRoot"/>: the lazily created root page plus
/// the entries pushed onto it (modals included, always on top). Pure cross-platform STATE —
/// the Scaffold's navigation proxies mutate it freely during a navigation batch (multiple
/// pushes/pops per commit are normal in Nalu), then the platform presenter
/// (<see cref="IScaffoldPresenter"/>) synchronizes to it once. It never touches platform APIs.
/// </summary>
/// <remarks>
/// It owns the stack and NOTHING else. Element-tree parenting and per-page platform state are
/// the <see cref="Scaffold"/>'s business: this type only reports that its membership changed,
/// and the scaffold reconciles. That separation is deliberate — mutating the tree from here got
/// the timing wrong in both directions. A pop used to unparent the departing page BEFORE the
/// presenter animated it away, so a page still on screen lost its binding context, its resource
/// resolution and its window; a root switch clears <see cref="RootPage"/> AFTER the presenter
/// has synchronized, so anything cleaned up at the end of a synchronization missed it entirely.
/// Membership is a fact the stack knows; when it is safe to tear a page down is not.
/// </remarks>
internal sealed class ScaffoldNavigationStack(ScaffoldRoot owner)
{
    private readonly List<NavigationStackPage> _pushedPages = [];

    /// <summary>
    /// Gets or sets the root page instance. Lifecycle is owned by the content-level proxy
    /// (created lazily from <see cref="ScaffoldRoot.PageType"/>, destroyable while not displayed).
    /// </summary>
    public Page? RootPage
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            field = value;
            NotifyStackChanged();
        }
    }

    /// <summary>Gets the pages pushed on top of the root page, bottom-first (modals always on top).</summary>
    public IReadOnlyList<NavigationStackPage> PushedPages => _pushedPages;

    /// <summary>Appends a newly pushed page.</summary>
    public void Push(NavigationStackPage entry)
    {
        _pushedPages.Add(entry);
        NotifyStackChanged();
    }

    /// <summary>Removes and returns the top page.</summary>
    public NavigationStackPage Pop()
    {
        var entry = _pushedPages[^1];
        _pushedPages.RemoveAt(_pushedPages.Count - 1);
        NotifyStackChanged();

        return entry;
    }

    /// <summary>
    /// Membership changed: the scaffold recomputes <see cref="Scaffold.CurrentPage"/> (which
    /// derives from the PROXY state, so mutations on non-current stacks are naturally no-ops)
    /// and reconciles page hosts and parenting against the new membership.
    /// </summary>
    private void NotifyStackChanged() => FindScaffold()?.OnNavigationStackChanged();

    private Scaffold? FindScaffold()
    {
        Element? element = owner;

        while (element is not null and not Page)
        {
            element = element.Parent;
        }

        return element as Scaffold;
    }

    /// <summary>
    /// Removes pushed pages from the top without pop semantics (stack trimming during absolute
    /// navigation). A negative count removes all pushed pages — mirrors
    /// <see cref="IShellSectionProxy.RemoveStackPages"/>. Returns the removed entries so the
    /// caller can run their disposal lifecycle.
    /// </summary>
    public IReadOnlyList<NavigationStackPage> RemoveFromTop(int count = -1)
    {
        if (count < 0 || count > _pushedPages.Count)
        {
            count = _pushedPages.Count;
        }

        var removed = new NavigationStackPage[count];

        for (var i = 0; i < count; i++)
        {
            removed[i] = Pop();
        }

        return removed;
    }
}
