using Microsoft.Extensions.DependencyInjection.Extensions;
using Nalu;

// ReSharper disable once CheckNamespace
namespace Microsoft.Maui;

/// <summary>
/// Provides a fluent API for configuring Nalu navigation.
/// </summary>
public static class NaluMauiAppBuilderExtensions
{
    /// <summary>
    /// Adds Nalu navigation to the application.
    /// </summary>
    /// <typeparam name="TApplication">Application type.</typeparam>
    /// <param name="builder">Maui app builder.</param>
    /// <param name="configure">Navigation configurator.</param>
    public static MauiAppBuilder UseNaluNavigation<TApplication>(this MauiAppBuilder builder, Action<NavigationConfigurator> configure)
        where TApplication : IApplication
    {
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddScoped<INavigationServiceProviderInternal, NavigationServiceProvider>();
        builder.Services.AddScoped<INavigationServiceProvider>(sp => sp.GetRequiredService<INavigationServiceProviderInternal>());

        // Navigation-state snapshot & restore: ALWAYS registered and inert unless
        // UseNaluNavigationRestore enabled it — shared/library pages inject it unconditionally.
        builder.Services.AddSingleton<NavigationRestoreService>();
        builder.Services.AddSingleton<INavigationRestore>(static provider => provider.GetRequiredService<NavigationRestoreService>());
        builder.Services.TryAddSingleton<IIntentSerializer>(static provider => new NavigationDefaultIntentSerializer(provider.GetService<NavigationRestoreOptions>()));
        builder.Services.TryAddSingleton<INavigationRestoreStore, NavigationRestoreFileStore>();

        var configurator = new NavigationConfigurator(builder.Services);
        configure(configurator);

        return builder;
    }

    /// <summary>
    /// Enables navigation-state snapshot &amp; restore: after an app restart the engine replays
    /// the last captured navigation (root selection, pushed stack, entering intents) once the
    /// configured initial page's first appearing completes — see <see cref="INavigationRestore"/>.
    /// Requires <see cref="UseNaluNavigation{TApplication}(MauiAppBuilder, Action{NavigationConfigurator})"/>
    /// (call order between the two does not matter). The library cannot see the app's build
    /// configuration: a DEBUG-only policy (the recommended developer-experience default) is
    /// expressed app-side via <see cref="NavigationRestoreOptions.Enabled"/> or an
    /// <c>#if DEBUG</c> guard around this call.
    /// </summary>
    /// <param name="builder">Maui app builder.</param>
    /// <param name="configure">Configures intents, expiry and serialization.</param>
    public static MauiAppBuilder UseNaluNavigationRestore(this MauiAppBuilder builder, Action<NavigationRestoreOptions>? configure = null)
    {
        // Idempotent accumulation: a second call configures the same options instance.
        var options = builder.Services
                             .FirstOrDefault(static d => d.ServiceType == typeof(NavigationRestoreOptions))
                             ?.ImplementationInstance as NavigationRestoreOptions;

        if (options is null)
        {
            options = new NavigationRestoreOptions();
            builder.Services.AddSingleton(options);

            // Fail fast (order-independently) when navigation itself was never configured:
            // without this the restore options would sit in DI silently unused.
            builder.Services.AddSingleton<IMauiInitializeService>(new NavigationRestoreStartupGuard());
        }

        configure?.Invoke(options);

        return builder;
    }

    private sealed class NavigationRestoreStartupGuard : IMauiInitializeService
    {
        public void Initialize(IServiceProvider services)
        {
            if (services.GetService<INavigationService>() is null)
            {
                throw new InvalidOperationException(
                    "UseNaluNavigationRestore requires Nalu navigation: call builder.UseNaluNavigation<TApplication>(...) as well."
                );
            }
        }
    }

    /// <summary>
    /// Configures a custom <see cref="Shell"/> handler that allows rendering a custom tab bar view via <see cref="NaluShell.TabBarViewProperty"/> when using <see cref="TabBar"/> or <see cref="FlyoutItem"/> with tabs.
    /// </summary>
    /// <example>
    /// <code>
    /// <![CDATA[
    /// <TabBar nalu:NaluShell.TabBarView="{nalu:NaluTabBar}"/>
    /// ]]>
    /// </code>
    /// </example>
    /// <remarks>
    /// This feature is only supported on iOS, Mac Catalyst and Android platforms.
    /// Nalu provides a built-in customizable implementation of a custom tab bar view via the <see cref="NaluTabBar"/> control.
    /// Any custom view will be bound to the corresponding <see cref="TabBar"/> or <see cref="FlyoutItem"/> to enable looping through tab items and handling tab selection.
    /// </remarks>
    public static MauiAppBuilder UseNaluTabBar(this MauiAppBuilder builder)
    {
#if IOS || MACCATALYST || ANDROID
        builder.ConfigureMauiHandlers(handlers => MauiHandlersCollectionExtensions.AddHandler<Shell, NaluShellRenderer>(handlers));
#endif
#if ANDROID && NET10_0_OR_GREATER
        Handlers.ScrollViewHandler.Mapper.Add("Nalu_ScrollSafeAreaRenderingFix",
                                                            (handler, _) =>
                                                            {
                                                                handler.PlatformView.SetClipToPadding(false);
                                                            });  
#endif
        return builder;
    }
}
