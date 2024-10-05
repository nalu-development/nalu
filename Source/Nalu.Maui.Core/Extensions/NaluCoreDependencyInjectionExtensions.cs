namespace Nalu;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Maui.LifecycleEvents;

#pragma warning disable IDE0053

/// <summary>
/// Provides extension methods for <see cref="MauiAppBuilder"/>.
/// </summary>
public static class NaluCoreDependencyInjectionExtensions
{
    /// <summary>
    /// Adds the encryption provider to the app.
    /// </summary>
    /// <param name="builder">The app builder.</param>
    public static MauiAppBuilder UseAppEncryption(this MauiAppBuilder builder)
    {
        builder.Services.AddAppEncryption();
        return builder;
    }

    /// <summary>
    /// Adds the encryption provider to the app.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection AddAppEncryption(this IServiceCollection services)
    {
        services.TryAddSingleton<IAppEncryptionProvider, AppEncryptionProvider>();
        return services;
    }

    internal static MauiAppBuilder UseLifecycleEventHandlers(this MauiAppBuilder builder)
    {
        if (builder.Services.Any(s => s.ServiceType == typeof(LifecycleHandlers)))
        {
            return builder;
        }

        builder.ConfigureLifecycleEvents(events =>
        {
#if IOS
            events.AddiOS(lifecycle =>
            {
                lifecycle.ContinueUserActivity(LifecycleHandlers.ContinueUserActivity);
            });
#endif
        });

        builder.Services.AddSingleton<LifecycleHandlers>();
        return builder;
    }
}
