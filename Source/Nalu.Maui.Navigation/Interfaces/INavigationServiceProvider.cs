namespace Nalu;

/// <summary>
/// Provides navigation-scoped services.
/// </summary>
/// <remarks>
/// Service instances added to this provider are visible to nested routes and disposed when the current route is left.
/// </remarks>
public interface INavigationServiceProvider : IServiceProvider, IDisposable
{
    /// <summary>
    /// Gets the <see cref="Page"/> instance which hosts this navigation service provider.
    /// </summary>
    Page ContextPage { get; }

    /// <summary>
    /// Registers a service instance visible to nested routes and disposed when the current route is left.
    /// </summary>
    /// <param name="instance">The service instance.</param>
    /// <typeparam name="T">The service type to register.</typeparam>
    void AddNavigationScoped<T>(T instance);
}
