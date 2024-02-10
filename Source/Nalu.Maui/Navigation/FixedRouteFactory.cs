namespace Nalu;

internal class FixedRouteFactory : RouteFactory
{
    private readonly WeakReference<Page> _weakPage;

#pragma warning disable IDE0290
    public FixedRouteFactory(Page page)
#pragma warning restore IDE0290
    {
        _weakPage = new WeakReference<Page>(page);
    }

    public override Element GetOrCreate() => _weakPage.TryGetTarget(out var page) ? page : throw new InvalidOperationException("Page has been collected");

    public override Element GetOrCreate(IServiceProvider services) => GetOrCreate();
}
