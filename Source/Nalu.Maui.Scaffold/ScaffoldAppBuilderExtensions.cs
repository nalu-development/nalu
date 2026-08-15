namespace Nalu;

/// <summary>Provides scaffold registration methods on <see cref="MauiAppBuilder"/>.</summary>
public static class ScaffoldAppBuilderExtensions
{
    /// <summary>
    /// Registers the Nalu <see cref="Scaffold"/> handler and services. Required for
    /// scaffold-hosted apps; requires <c>UseNaluNavigation</c> to be configured as well.
    /// Safe to call on every platform: off iOS/Android the registered services
    /// (<see cref="IOverlayService"/>, <see cref="IScaffoldFlyoutController"/>) degrade to
    /// graceful no-ops — hosting an actual <see cref="Scaffold"/> still throws.
    /// </summary>
    /// <param name="builder">The MAUI application builder.</param>
    /// <param name="configure">
    /// Optional overlay registration for <see cref="IOverlayService"/> — usually the
    /// source-generated <c>AddOverlays()</c>, or explicit
    /// <see cref="IScaffoldConfigurator.AddOverlay{TModel,TView}()"/> calls.
    /// </param>
    /// <returns>The same builder, for chaining.</returns>
    public static MauiAppBuilder UseNaluScaffold(this MauiAppBuilder builder, Action<IScaffoldConfigurator>? configure = null)
    {
#if IOS
        // Upstream MAUI iOS bug: programmatic scrolls clamp without AdjustedContentInset and
        // under-shoot on inset-consuming scroll views (every scaffold-hosted page). See
        // ScaffoldScrollToFix; remove once fixed in MAUI.
        ScaffoldScrollToFix.Apply();
#endif

#if IOS || ANDROID
        // Keyboard-aware overlays own the soft-keyboard geometry (see ScaffoldKeyboardSupport):
        // iOS drops MAUI's KeyboardAutoManagerScroll, Android goes edge-to-edge and receives the
        // IME as window insets. Nalu.Maui.Core's SoftKeyboardManager is NOT supported alongside
        // (analyzer NALU0104).
        ScaffoldKeyboardSupport.Configure(builder);
#endif

        // Page-scope drawer control: page models open/close the ambient scaffold's flyouts
        // without a scaffold reference.
        builder.Services.AddScoped<IScaffoldFlyoutController, ScaffoldFlyoutController>();

        // Model-first overlays: the registry is built ONCE here (trim-safe closures, no
        // reflection over registrations); the service resolves the ambient scaffold per call.
        var overlayRegistry = new ScaffoldOverlayRegistry();
        configure?.Invoke(overlayRegistry);
        builder.Services.AddSingleton(overlayRegistry);
        builder.Services.AddScoped<IOverlayService, ScaffoldOverlayService>();

        return builder.ConfigureMauiHandlers(handlers => handlers.AddHandler<Scaffold, ScaffoldHandler>());
    }
}
