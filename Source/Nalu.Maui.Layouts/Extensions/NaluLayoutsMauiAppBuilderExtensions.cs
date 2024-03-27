// ReSharper disable once CheckNamespace
namespace Microsoft.Maui;

using Nalu;

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
        _ = new XamlInitializer();
        return builder;
    }
}
