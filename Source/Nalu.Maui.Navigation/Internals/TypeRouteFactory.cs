namespace Nalu;

internal class TypeRouteFactory : RouteFactory
{
    private readonly Queue<Page> _pages = new();

    public void Push(Page page) => _pages.Enqueue(page);

    public override Element GetOrCreate() => _pages.TryDequeue(out var page)
        ? page
        : throw new InvalidOperationException("Unable to find page instance for specified route.");

    public override Element GetOrCreate(IServiceProvider services) => GetOrCreate();
}
