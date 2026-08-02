namespace Nalu;

/// <summary>
/// Adapts a <see cref="ScaffoldRoot"/> to the engine's section- and content-level contracts.
/// The Scaffold's two-level model synthesizes the content level: this class implements both,
/// acting as its own single content.
/// </summary>
internal sealed class ScaffoldRootProxy : IShellSectionProxy, IShellContentProxy
{
    private readonly NavigationService _navigationService;
    private readonly IShellContentProxy[] _contents;

    public ScaffoldRoot Root { get; }
    public ScaffoldAreaProxy Area { get; }
    public string SegmentName { get; }

    /// <summary>Route of the root page entry: <c>//{area}/{root}</c>.</summary>
    public string BaseRoute { get; }

    /// <summary>The resolved page type (a <see cref="Page"/> subclass).</summary>
    public Type PageType { get; }

    public ScaffoldRootProxy(ScaffoldRoot root, ScaffoldAreaProxy area, NavigationService navigationService)
    {
        Root = root;
        Area = area;
        _navigationService = navigationService;

        var declaredType = root.PageType
                           ?? throw new InvalidOperationException("ScaffoldRoot.PageType must be set.");

        PageType = NavigationHelper.GetPageType(declaredType, navigationService.Configuration);
        SegmentName = NavigationSegmentAttribute.GetSegmentName(PageType);
        BaseRoute = $"//{area.SegmentName}/{SegmentName}";
        _contents = [this];
    }

    public bool HasGuard => Page?.BindingContext is ILeavingGuard;

    public Page? Page => Root.NavigationStack.RootPage;

    public IShellContentProxy CurrentContent => this;

    public IReadOnlyList<IShellContentProxy> Contents => _contents;

    IShellItemProxy IShellSectionProxy.Parent => Area;

    IShellSectionProxy IShellContentProxy.Parent => this;

    public Page GetOrCreateContent() => Root.NavigationStack.RootPage ??= _navigationService.CreatePage(PageType, null);

    public void DestroyContent()
    {
        var stack = Root.NavigationStack;

        if (stack.RootPage is { } page)
        {
            PageNavigationContext.Dispose(page);
            stack.RootPage = null;
        }
    }

    public IEnumerable<NavigationStackPage> GetNavigationStack(IShellContentProxy? content = null)
    {
        if (Page is not { } rootPage)
        {
            yield break;
        }

        yield return new NavigationStackPage(BaseRoute, SegmentName, rootPage, false);

        foreach (var entry in Root.NavigationStack.PushedPages)
        {
            yield return entry;
        }
    }

    public void RemoveStackPages(int count = -1)
        // Trimming has no pop lifecycle: disposal of the removed pages is the engine's
        // responsibility (it tracks them in its dispose bag), matching the Shell adapter.
        => Root.NavigationStack.RemoveFromTop(count);

    /// <summary>
    /// Adopts the LIVE navigation stack of a same-segment predecessor (XAML hot reload
    /// replaces the whole structure with fresh instances): the pages migrate into this root's
    /// stack, so the presented content — and all preserved state — survives the replacement
    /// without any navigation.
    /// </summary>
    internal void AdoptStackFrom(ScaffoldRootProxy predecessor)
    {
        var previousStack = predecessor.Root.NavigationStack;
        var newStack = Root.NavigationStack;

        // RemoveFromTop returns entries top-first; re-push bottom-first.
        var pushed = previousStack.RemoveFromTop();
        var rootPage = previousStack.RootPage;
        previousStack.RootPage = null;

        newStack.RootPage = rootPage;

        for (var i = pushed.Count - 1; i >= 0; i--)
        {
            newStack.Push(pushed[i]);
        }
    }
}
