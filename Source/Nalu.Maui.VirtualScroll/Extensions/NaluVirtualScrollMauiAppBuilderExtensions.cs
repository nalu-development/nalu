using Nalu;

// ReSharper disable once CheckNamespace
namespace Microsoft.Maui;

/// <summary>
/// Provides a fluent API for configuring Nalu virtual scroll.
/// </summary>
public static class NaluVirtualScrollMauiAppBuilderExtensions
{
    /// <summary>
    /// Adds Nalu virtual scroll to the application.
    /// </summary>
    /// <param name="builder">Maui app builder.</param>
    public static MauiAppBuilder UseNaluVirtualScroll(this MauiAppBuilder builder)
    {
        _ = new NaluXamlVirtualScrollInitializer();

#if IOS || MACCATALYST
        builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<IVirtualScroll, VirtualScrollHandler>();
            }
        );
#endif

        return builder;
    }
}
