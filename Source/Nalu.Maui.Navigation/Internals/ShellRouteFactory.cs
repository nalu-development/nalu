namespace Nalu;

internal class ShellRouteFactory
{
    private readonly Dictionary<Type, TypeRouteFactory> _factories = [];

    public RouteFactory GetRouteFactory(Page page)
    {
        var type = page.GetType();
        if (!_factories.TryGetValue(type, out var factory))
        {
            factory = new TypeRouteFactory();
            _factories[type] = factory;
        }

        factory.Push(page);
        return factory;
    }

    private class TypeRouteFactory : RouteFactory
    {
        private readonly Queue<Page> _pages = new();

        public void Push(Page page) => _pages.Enqueue(page);

        public override Element GetOrCreate() => _pages.TryDequeue(out var page) ? page : throw new InvalidOperationException("Page has already been created.");

        public override Element GetOrCreate(IServiceProvider services) => GetOrCreate();
    }
}
