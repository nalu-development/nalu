namespace Nalu;

internal sealed partial class NavigationServiceProvider : INavigationServiceProviderInternal
{
    private readonly Dictionary<Type, object> _services = [];
    private INavigationServiceProvider? _parent;

    public object? GetService(Type serviceType)
    {
        if (_services.TryGetValue(serviceType, out var service))
        {
            return service;
        }

        return _parent?.GetService(serviceType);
    }

    public void AddNavigationScoped<T>(T instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        _services.Add(typeof(T), instance);
    }

    public void SetParent(INavigationServiceProvider parent) => _parent = parent;

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
    }
}
