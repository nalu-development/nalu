namespace Nalu;

using Android.Content.PM;
using AndroidX.Core.App;
using global::Android.App;
using global::Android.Content;
using global::Android.OS;
using Microsoft.Extensions.Logging;

#pragma warning disable VSTHRD100

/// <summary>
/// A convenient base class to implement Android foreground services.
/// </summary>
/// <remarks>
/// This class provides a base implementation for Android foreground services, and it will be moved to a separate package in the future.
/// Do NOT use this class directly, this is for internal use only.
/// </remarks>
public abstract class AndroidForegroundServiceBase : Service
{
    private ILogger? _logger;
    private Notification? _notification;

    /// <summary>
    /// Gets the notification channel identifier for this service instance.
    /// </summary>
    protected virtual int NotificationId { get; } = AndroidApp.NewNotificationId();

    /// <summary>
    /// Gets the notification manager for this service.
    /// </summary>
    protected NotificationManagerCompat? NotificationManager { get; private set; }

#pragma warning disable CA1822
    /// <summary>
    /// Gets the application service provider.
    /// </summary>
    protected IServiceProvider ServiceProvider => IPlatformApplication.Current!.Services;
#pragma warning restore CA1822

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger => _logger ??= ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(GetType());

    /// <summary>
    /// Gets the foreground service type.
    /// </summary>
    protected abstract ForegroundService ForegroundService { get; }

    /// <inheritdoc />
    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        Logger.LogDebug("OnStartCommand / Action: {Action} / Notification ID: {NotificationId}", intent?.Action, NotificationId);

        if (intent?.Action == AndroidForegroundService.StartAction)
        {
            StartForegroundService();
        }
        else if (intent?.Action == AndroidForegroundService.StopAction)
        {
            StopForegroundService();
        }

        return StartCommandResult.Sticky;
    }

    protected virtual void EnsureNotificationChannel()
    {
        var notificationChannel = AndroidForegroundService.NotificationChannel;
        if (NotificationManager!.GetNotificationChannel(notificationChannel) != null)
        {
            return;
        }

        var channel = new NotificationChannel(
            notificationChannel,
            notificationChannel,
            NotificationImportance.Default);

        channel.SetShowBadge(false);

        NotificationManager.CreateNotificationChannel(channel);
    }

    protected virtual NotificationCompat.Builder CreateNotificationBuilder()
    {
        var build = new NotificationCompat.Builder(AndroidApp.GetApplicationContext(), AndroidForegroundService.NotificationChannel)
            .SetSmallIcon(GetNotificationIconResourceId())
            .SetOngoing(true)
            .SetSilent(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetOnlyAlertOnce(true);

        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            build.SetForegroundServiceBehavior((int)NotificationForegroundService.Immediate);
        }

        return build;
    }

    protected virtual int GetNotificationIconResourceId()
    {
        var id = AndroidApp.GetResourceDrawableIdByName("notification");
        if (id > 0)
        {
            return id;
        }

        id = AndroidApp.GetApplicationContext().ApplicationInfo?.Icon ?? 0;
        if (id > 0)
        {
            return id;
        }

        throw new InvalidOperationException("A 'notification' icon drawable resource or application icon is required.");
    }

    /// <summary>
    /// Actually starts the background service.
    /// </summary>
    protected abstract Task StartAsync();

    /// <summary>
    /// Actually stops the background service.
    /// </summary>
    protected abstract Task StopAsync();

    private async void StartForegroundService()
    {
        NotificationManager = NotificationManagerCompat.From(ApplicationContext!);
        EnsureNotificationChannel();

        Logger.LogDebug("Starting notification: {NotificationId}", NotificationId);
        _notification = CreateNotificationBuilder().Build();
        _notification.Flags |= NotificationFlags.ForegroundService;

        ServiceCompat.StartForeground(this, NotificationId, _notification, (int)ForegroundService);
        Logger.LogDebug("Started foreground Service");

        try
        {
            await StartAsync().ConfigureAwait(true);
            Logger.LogDebug("Started background Service");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred while starting the background service.");
            StopForegroundService();
        }
    }

    private async void StopForegroundService()
    {
        Logger.LogDebug("Stopping background service");

        try
        {
            await StopAsync().ConfigureAwait(true);
            Logger.LogDebug("Stopped background service");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "An error occurred while stopping the background service.");
        }

        Logger.LogDebug("Stopping foreground service");
        ServiceCompat.StopForeground(this, ServiceCompat.StopForegroundRemove);
        StopSelf();

        Logger.LogDebug("Foreground service stopped successfully");
    }
}
