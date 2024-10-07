namespace Nalu;

using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Extensions for <see cref="MauiAppBuilder"/> to add Nalu connectivity services.
/// </summary>
public static class NaluConnectivityHostingExtensions
{
    /// <summary>
    /// Adds the Nalu background HTTP client to the application.
    /// </summary>
    /// <param name="builder">The app builder.</param>
    /// <param name="options">The options.</param>
    public static MauiAppBuilder UseNaluBackgroundHttpClient(this MauiAppBuilder builder, BackgroundClientHttpOptions? options = null)
    {
        options ??= new BackgroundClientHttpOptions();

        builder.UseLifecycleEventHandlers();
        builder.Services.TryAddSingleton<IAppEncryptionProvider, AppEncryptionProvider>();
        builder.Services.TryAddSingleton<ISecureStorage>(SecureStorage.Default);
        builder.Services
            .AddSingleton<BackgroundClientHttpOptions>(options)
            .AddSingleton<IBackgroundHttpRequestManager, BackgroundHttpRequestManager>()
#if ANDROID
            .AddSingleton<BackgroundHttpRequestPlatformProcessor>()
            .AddSingleton<IBackgroundHttpRequestPlatformProcessor>(sp => sp.GetRequiredService<BackgroundHttpRequestPlatformProcessor>())
#endif
#if IOS
            .AddSingleton<BackgroundHttpRequestPlatformProcessor>()
            .AddSingleton<IHandleEventsForBackgroundUrlHandler>(sp => sp.GetRequiredService<BackgroundHttpRequestPlatformProcessor>())
            .AddSingleton<IBackgroundHttpRequestPlatformProcessor>(sp => sp.GetRequiredService<BackgroundHttpRequestPlatformProcessor>())
#endif
            ;

        return builder;
    }
}
