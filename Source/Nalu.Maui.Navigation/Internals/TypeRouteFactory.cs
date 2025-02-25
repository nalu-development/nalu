namespace Nalu;

internal class TypeRouteFactory(Type pageType) : RouteFactory
{
    private readonly Queue<Page> _pages = new();

    public void Push(Page page) => _pages.Enqueue(page);

    public override Element GetOrCreate()
    {
        if (_pages.TryDequeue(out var page))
        {
            return page;
        }

        throw new InvalidOperationException($"Unable to find page instance for specified route: {pageType.Name}.");
    }

    public override Element GetOrCreate(IServiceProvider services) => GetOrCreate();
}
