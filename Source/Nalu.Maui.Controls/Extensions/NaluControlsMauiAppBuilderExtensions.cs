// ReSharper disable once CheckNamespace

using Nalu;

namespace Microsoft.Maui;

/// <summary>
/// Provides a fluent API for configuring Nalu controls.
/// </summary>
public static class NaluControlsMauiAppBuilderExtensions
{
    /// <summary>
    /// Adds Nalu controls to the application.
    /// </summary>
    /// <param name="builder">Maui app builder.</param>
    public static MauiAppBuilder UseNaluControls(this MauiAppBuilder builder)
    {
        _ = new NaluXamlControlsInitializer();
        builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<InteractableCanvasView, InteractableCanvasViewHandler>());

        return builder;
    }
}
