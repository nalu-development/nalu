namespace Nalu.Internals;

using Microsoft.Maui.Handlers;

/// <summary>
/// Provides extension methods for <see cref="IElementHandler"/> to access <see cref="IServiceProvider"/>.
/// </summary>
public static class NaluElementHandlerExtensions
{
    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> from the <see cref="IElementHandler"/>.
    /// </summary>
    /// <returns>Handler's service provider.</returns>
    /// <param name="handler">Element's handler.</param>
    /// <exception cref="InvalidOperationException"><see cref="MauiContext"/> and its services must be set.</exception>
    public static IServiceProvider GetServiceProvider(this IElementHandler handler)
    {
        var context = handler.MauiContext ??
                      throw new InvalidOperationException($"Unable to find the context. The {nameof(ElementHandler.MauiContext)} property should have been set by the host.");

        var services = context.Services ??
                       throw new InvalidOperationException($"Unable to find the service provider. The {nameof(ElementHandler.MauiContext)} property should have been set by the host.");

        return services;
    }

    /// <summary>
    /// Gets the service from the <see cref="IElementHandler"/>'s <see cref="MauiContext"/>.
    /// </summary>
    /// <param name="handler">Element's handler.</param>
    /// <typeparam name="T">Desired service type.</typeparam>
    /// <returns>Service type instance.</returns>
    /// <exception cref="InvalidOperationException"><see cref="MauiContext"/> or its services are null.</exception>
    public static T? GetService<T>(this IElementHandler handler)
    {
        var services = handler.GetServiceProvider();

        var service = services.GetService<T>();

        return service;
    }

    /// <summary>
    /// Gets the service from the <see cref="IElementHandler"/>'s <see cref="MauiContext"/>.
    /// </summary>
    /// <param name="handler">Element's handler.</param>
    /// <typeparam name="T">Desired service type.</typeparam>
    /// <returns>Service type instance.</returns>
    /// <exception cref="InvalidOperationException"><see cref="MauiContext"/> or its services are null or there is no registered service of type <typeparamref name="T"/>.</exception>
    public static T GetRequiredService<T>(this IElementHandler handler)
        where T : notnull
    {
        var services = handler.GetServiceProvider();

        var service = services.GetRequiredService<T>();

        return service;
    }
}
