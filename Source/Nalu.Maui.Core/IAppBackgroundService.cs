namespace Nalu;

/// <summary>
/// An interface for a background service which can be started and stopped multiple times.
/// </summary>
public interface IAppBackgroundService
{
    /// <summary>
    /// Gets the title of the user notification.
    /// </summary>
    string UserTitle { get; }

    /// <summary>
    /// Gets the description of the user notification.
    /// </summary>
    string UserDescription { get; }

    /// <summary>
    /// Starts the background service.
    /// </summary>
    /// <param name="completionHandler">Signals that the service has completed its job and can be stopped.</param>
    Task StartAsync(Action completionHandler);

    /// <summary>
    /// Stops the background service.
    /// </summary>
    Task StopAsync();
}
