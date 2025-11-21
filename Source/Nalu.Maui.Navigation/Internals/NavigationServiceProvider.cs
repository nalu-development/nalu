namespace Nalu;

internal sealed class NavigationServiceProvider : INavigationServiceProviderInternal
{
    private readonly Dictionary<Type, object> _services = [];
    private INavigationServiceProvider? _parent;
    private Page? _page;

    public object? GetService(Type serviceType)
    {
        if (_services.TryGetValue(serviceType, out var service))
        {
            return service;
        }

        return _parent?.GetService(serviceType);
    }

    public Page ContextPage => _page ?? throw new InvalidOperationException("Context page is not set.");

    public void AddNavigationScoped<T>(T instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _services.Add(typeof(T), instance);
    }

    public void SetParent(INavigationServiceProvider parent) => _parent = parent;

    public void SetContextPage(Page page) => _page = page;

    public void Dispose()
    {
        foreach (var service in _services.Values)
        {
            if (service is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        _parent = null;
        _page = null;
    }
}
