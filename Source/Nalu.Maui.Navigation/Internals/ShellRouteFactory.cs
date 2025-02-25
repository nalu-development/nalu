namespace Nalu;

internal class ShellRouteFactory
{
    private readonly Dictionary<Type, TypeRouteFactory> _factories = [];

    public TypeRouteFactory GetRouteFactory(Type pageType)
    {
        if (!_factories.TryGetValue(pageType, out var factory))
        {
            factory = new TypeRouteFactory(pageType);
            _factories[pageType] = factory;
        }

        return factory;
    }
}
