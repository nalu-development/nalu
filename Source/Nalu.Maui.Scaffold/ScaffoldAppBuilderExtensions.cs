namespace Nalu;

/// <summary>Provides scaffold registration methods on <see cref="MauiAppBuilder"/>.</summary>
public static class ScaffoldAppBuilderExtensions
{
    /// <summary>
    /// Registers the Nalu <see cref="Scaffold"/> handlers and services.
    /// Requires <c>UseNaluNavigation</c> to be configured as well.
    /// </summary>
    /// <param name="builder">The MAUI application builder.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static MauiAppBuilder UseNaluScaffold(this MauiAppBuilder builder)
        // Handler and host-contract registrations arrive with P0/P1; the entry point is
        // established up-front so samples and docs can reference a stable name.
        => builder;
}
