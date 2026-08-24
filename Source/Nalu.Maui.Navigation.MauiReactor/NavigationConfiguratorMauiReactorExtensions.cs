namespace Nalu;

/// <summary>
/// MauiReactor integration for Nalu navigation.
/// </summary>
public static class NavigationConfiguratorMauiReactorExtensions
{
    /// <summary>
    /// Enables navigating to MauiReactor components as pages: register each component with
    /// <see cref="NavigationConfigurator.AddPage{TPage}()" /> and navigate with
    /// <c>Navigation.Relative().Push&lt;MyComponent&gt;()</c> (or use the component as a root
    /// page type). The component is the navigation lifecycle target — implement
    /// <see cref="IEnteringAware" />, <see cref="ILeavingGuard" />, typed intent interfaces…
    /// directly on the component class. Components are resolved from the page's own navigation
    /// scope, so constructor injection (e.g. <see cref="INavigationService" />) works.
    /// </summary>
    /// <remarks>
    /// Also call MauiReactor's own <c>builder.UseMauiReactor()</c> on the app builder so
    /// <c>Component.Services</c> and MauiReactor's runtime are initialized.
    /// </remarks>
    /// <param name="configurator">The Nalu navigation configurator.</param>
    public static NavigationConfigurator UseMauiReactorComponents(this NavigationConfigurator configurator)
        => configurator.UseComponentPageFactory<MauiReactorComponentPageFactory>();
}
