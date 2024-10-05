namespace Nalu;

using Foundation;
using Microsoft.Maui.LifecycleEvents;
using UIKit;

/// <summary>
/// Handler for <see cref="iOSLifecycle.ContinueUserActivity"/> lifecycle event.
/// </summary>
public interface IContinueUserActivityHandler
{
    /// <inheritdoc cref="iOSLifecycle.ContinueUserActivity"/>
    bool ContinueUserActivity(
        UIApplication application,
        NSUserActivity userActivity,
        UIApplicationRestorationHandler completionHandler);
}
