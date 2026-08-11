// ReSharper disable once CheckNamespace

using Nalu;

namespace Microsoft.Maui;

/// <summary>
/// Provides a fluent API for configuring Nalu layouts.
/// </summary>
public static class NaluLayoutsMauiAppBuilderExtensions
{
    /// <summary>
    /// Adds Nalu layouts to the application.
    /// </summary>
    /// <param name="builder">Maui app builder.</param>
    public static MauiAppBuilder UseNaluLayouts(this MauiAppBuilder builder)
    {
        _ = new NaluXamlLayoutsInitializer();

        builder.ConfigureMauiHandlers(handlers =>
            {
                handlers.AddHandler<IViewBox, ViewBoxHandler>();
                handlers.AddHandler<SlideBox, SlideBoxHandler>();
#if IOS || MACCATALYST || ANDROID || WINDOWS
                handlers.AddHandler<ScrollBox, ScrollBoxHandler>();
#endif
            }
        );

        return builder;
    }
}
