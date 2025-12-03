using Microsoft.Maui.Handlers;
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

#if IOS || MACCATALYST || ANDROID
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
    /// Nalu provides a built-in customizable implementation of a custom tab bar view via the <see cref="NaluTabBar"/> control.
    /// Any custom view will be bound to the corresponding <see cref="TabBar"/> or <see cref="FlyoutItem"/> to enable looping through tab items and handling tab selection.
    /// </remarks>
    public static MauiAppBuilder UseNaluTabBar(this MauiAppBuilder builder)
    {
        builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<Shell, NaluShellRenderer>());
#if ANDROID && NET10_0_OR_GREATER
        ScrollViewHandler.Mapper.Add("Nalu_ScrollSafeAreaRenderingFix",
                                     (handler, _) =>
                                     {
                                         handler.PlatformView.SetClipToPadding(false);
                                     });  
#endif
        return builder;
    }
#endif
}
