namespace Nalu;

internal class FixedRouteFactory : RouteFactory
{
    private Page? _page;

#pragma warning disable IDE0290
    public FixedRouteFactory(Page page)
#pragma warning restore IDE0290
    {
        _page = page;
    }

    public override Element GetOrCreate()
    {
        var page = _page;
        if (page is not null)
        {
            _page = null;
            return page;
        }

        throw new InvalidOperationException("Page has already been created.");
    }

    public override Element GetOrCreate(IServiceProvider services) => GetOrCreate();
}
