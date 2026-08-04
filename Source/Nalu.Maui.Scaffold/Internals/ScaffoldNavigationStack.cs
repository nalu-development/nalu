namespace Nalu;

/// <summary>
/// The live navigation stack of a <see cref="ScaffoldRoot"/>: the lazily created root page plus
/// the entries pushed onto it (modals included, always on top). Pure cross-platform state —
/// the Scaffold's navigation proxies mutate it freely during a navigation batch (multiple
/// pushes/pops per commit are normal in Nalu), then the platform presenter
/// (<see cref="IScaffoldPresenter"/>) synchronizes to it once. It never touches platform APIs.
/// </summary>
/// <remarks>
/// Every page entering the stack is parented as a logical child of the hosting
/// <see cref="Scaffold"/> (MAUI requires a page's parent to be a page), so pages participate in
/// the MAUI element tree: window resolution, visual-tree walks and tooling all work.
/// </remarks>
internal sealed class ScaffoldNavigationStack(ScaffoldRoot owner)
{
    private readonly List<NavigationStackPage> _pushedPages = [];
    private Page? _rootPage;

    /// <summary>
    /// Gets or sets the root page instance. Lifecycle is owned by the content-level proxy
    /// (created lazily from <see cref="ScaffoldRoot.PageType"/>, destroyable while not displayed).
    /// </summary>
    public Page? RootPage
    {
        get => _rootPage;
        set
        {
            if (ReferenceEquals(_rootPage, value))
            {
                return;
            }

            if (_rootPage is not null)
            {
                FindHostPage()?.RemoveLogicalChild(_rootPage);
                Scaffold.CleanupPageFlyoutContent(_rootPage);
            }

            _rootPage = value;

            if (value is not null)
            {
                FindHostPage()?.AddLogicalChild(value);
            }

            NotifyCurrentPageMayHaveChanged();
        }
    }

    /// <summary>Gets the pages pushed on top of the root page, bottom-first (modals always on top).</summary>
    public IReadOnlyList<NavigationStackPage> PushedPages => _pushedPages;

    /// <summary>Appends a newly pushed page.</summary>
    public void Push(NavigationStackPage entry)
    {
        _pushedPages.Add(entry);
        FindHostPage()?.AddLogicalChild(entry.Page);
        NotifyCurrentPageMayHaveChanged();
    }

    /// <summary>Removes and returns the top page.</summary>
    public NavigationStackPage Pop()
    {
        var entry = _pushedPages[^1];
        _pushedPages.RemoveAt(_pushedPages.Count - 1);
        FindHostPage()?.RemoveLogicalChild(entry.Page);

        // The page's drawer overrides leave the resolution stack with it; release the
        // attached content so the page model is not retained through it.
        Scaffold.CleanupPageFlyoutContent(entry.Page);

        NotifyCurrentPageMayHaveChanged();

        return entry;
    }

    /// <summary>
    /// The scaffold's observable <see cref="Scaffold.CurrentPage"/> recomputes from the
    /// PROXY state, so mutations on non-current stacks are naturally no-ops.
    /// </summary>
    private void NotifyCurrentPageMayHaveChanged() => (FindHostPage() as Scaffold)?.UpdateCurrentPage();

    private Page? FindHostPage()
    {
        Element? element = owner;

        while (element is not null and not Page)
        {
            element = element.Parent;
        }

        return element as Page;
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
