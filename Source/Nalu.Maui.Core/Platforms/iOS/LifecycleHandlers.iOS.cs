namespace Nalu;

using Foundation;
using UIKit;

/// <summary>
/// Provides iOS lifecycle callbacks.
/// </summary>
public sealed partial class LifecycleHandlers
{
    /// <inheritdoc cref="UIApplicationDelegate.ContinueUserActivity"/>
    public static bool ContinueUserActivity(
        UIApplication application,
        NSUserActivity userActivity,
        UIApplicationRestorationHandler completionHandler)
    {
        var handlers = GetLifecycleHandlers<IContinueUserActivityHandler>();
        foreach (var handler in handlers)
        {
            if (handler.ContinueUserActivity(application, userActivity, completionHandler))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc cref="UIApplicationDelegate.HandleEventsForBackgroundUrl"/>
    public static void HandleEventsForBackgroundUrl(
        UIApplication application,
        string sessionIdentifier,
        Action completionHandler)
    {
        var handlers = GetLifecycleHandlers<IHandleEventsForBackgroundUrlHandler>();
        foreach (var handler in handlers)
        {
            if (handler.HandleEventsForBackgroundUrl(application, sessionIdentifier, completionHandler))
            {
                return;
            }
        }
    }
}
