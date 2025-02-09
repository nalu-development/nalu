// ReSharper disable once CheckNamespace

using Nalu;

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

        var configurator = new NavigationConfigurator(builder.Services, typeof(TApplication));
        configure(configurator);

        return builder;
    }

    /// <summary>
    /// Adds Nalu navigation to the application using the default naming convention: MyPage -> MyPageModel.
    /// </summary>
    /// <remarks>
    /// Looks for pages and page models in the application assembly.
    /// </remarks>
    /// <typeparam name="TApplication">Application type.</typeparam>
    /// <param name="builder">Maui app builder.</param>
    public static MauiAppBuilder UseNaluNavigation<TApplication>(this MauiAppBuilder builder)
        where TApplication : IApplication
        => builder.UseNaluNavigation<TApplication>(configurator => configurator.AddPages());
}
