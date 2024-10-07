namespace Nalu;

/// <summary>
/// Android foreground service constants.
/// </summary>
public static class AndroidForegroundService
{
    /// <summary>
    /// An intent action to start the foreground service.
    /// </summary>
    public const string StartAction = "START_FOREGROUND_SERVICE";

    /// <summary>
    /// An intent action to stop the foreground service.
    /// </summary>
    public const string StopAction = "STOP_FOREGROUND_SERVICE";

    /// <summary>
    /// The notification channel for foreground services.
    /// </summary>
    public const string NotificationChannel = "Service";
}
