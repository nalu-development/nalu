namespace Nalu;

/// <summary>Provides scaffold registration methods on <see cref="MauiAppBuilder"/>.</summary>
public static class ScaffoldAppBuilderExtensions
{
    /// <summary>
    /// Registers the Nalu <see cref="Scaffold"/> handler. Required for scaffold-hosted apps;
    /// requires <c>UseNaluNavigation</c> to be configured as well.
    /// </summary>
    /// <param name="builder">The MAUI application builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static MauiAppBuilder UseNaluScaffold(this MauiAppBuilder builder)
        => builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<Scaffold, ScaffoldHandler>());
}
