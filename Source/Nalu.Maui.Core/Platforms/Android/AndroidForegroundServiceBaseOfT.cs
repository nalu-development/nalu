namespace Nalu;

using AndroidX.Core.App;

#pragma warning disable VSTHRD100, SA1649

/// <summary>
/// A convenient base class to implement Android foreground services.
/// </summary>
/// <remarks>
/// This class provides a base implementation for Android foreground services, and it will be moved to a separate package in the future.
/// Do NOT use this class directly, this is for internal use only.
/// </remarks>
/// <typeparam name="TBackgroundService">The type of background service.</typeparam>
public abstract class AndroidForegroundServiceBase<TBackgroundService> : AndroidForegroundServiceBase
    where TBackgroundService : IAppBackgroundService
{
    private TBackgroundService? _backgroundService;

    /// <summary>
    /// Gets the background service instance.
    /// </summary>
    protected TBackgroundService BackgroundService => _backgroundService ??= ServiceProvider.GetRequiredService<TBackgroundService>();

    /// <inheritdoc />
    protected override NotificationCompat.Builder CreateNotificationBuilder()
    {
        var service = BackgroundService;
        var build = base.CreateNotificationBuilder();
        build
            .SetContentTitle(service.UserTitle)
            .SetContentText(service.UserDescription);

        return build;
    }

    /// <inheritdoc />
    protected override Task StartAsync() => BackgroundService.StartAsync(CompleteHandler);

    /// <inheritdoc />
    protected override Task StopAsync() => BackgroundService.StopAsync();

    private void CompleteHandler() => AndroidApp.StopForegroundService(GetType());
}
