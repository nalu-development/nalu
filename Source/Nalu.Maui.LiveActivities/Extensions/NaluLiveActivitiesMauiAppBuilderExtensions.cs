using Microsoft.Extensions.DependencyInjection.Extensions;
using Nalu;

// ReSharper disable once CheckNamespace
namespace Microsoft.Maui;

/// <summary>
/// Provides a fluent API for configuring Nalu live activities.
/// </summary>
public static class NaluLiveActivitiesMauiAppBuilderExtensions
{
    /// <summary>
    /// Adds Nalu live activities to the application: registers <see cref="ILiveActivityManager"/>
    /// rendering the shared content model as an iOS Live Activity or an Android (promoted)
    /// ongoing notification. Everywhere else the manager reports
    /// <see cref="LiveActivitySupport.Unavailable"/> and calls are inert.
    /// </summary>
    /// <param name="builder">Maui app builder.</param>
    /// <param name="configure">Optional configuration; see <see cref="LiveActivityOptions"/>.</param>
    public static MauiAppBuilder UseNaluLiveActivities(this MauiAppBuilder builder, Action<LiveActivityOptions>? configure = null)
    {
        var options = new LiveActivityOptions();
        configure?.Invoke(options);
        builder.Services.TryAddSingleton(options);

#if ANDROID
        builder.Services.TryAddSingleton<ILiveActivityManager, AndroidLiveActivityManager>();
#elif IOS && !MACCATALYST
        builder.Services.TryAddSingleton<ILiveActivityManager, AppleLiveActivityManager>();
#else
        builder.Services.TryAddSingleton<ILiveActivityManager, UnsupportedLiveActivityManager>();
#endif

        return builder;
    }
}
