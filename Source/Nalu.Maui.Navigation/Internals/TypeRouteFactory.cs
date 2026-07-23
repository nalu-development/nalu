namespace Nalu;

internal class TypeRouteFactory(Type pageType) : RouteFactory
{
    private readonly Queue<Page> _pages = new();
    private WeakReference<Page>? _lastVended;

    public void Push(Page page) => _pages.Enqueue(page);

    public override Element GetOrCreate()
    {
        if (_pages.TryDequeue(out var page))
        {
            _lastVended = new WeakReference<Page>(page);

            return page;
        }

        // MAUI's stack rebuilding can pop a just-vended page and then re-request its route
        // (it mis-matches stacks containing duplicate adjacent route names, e.g. after
        // replacing a page with another instance of a type already on the stack).
        // Re-vend the page as long as it is not attached to a stack.
        if (_lastVended?.TryGetTarget(out var lastVended) == true && lastVended.Parent is null)
        {
            return lastVended;
        }

        throw new InvalidOperationException($"Unable to find page instance for specified route: {pageType.Name}.");
    }

    public override Element GetOrCreate(IServiceProvider services) => GetOrCreate();
}
