namespace Nalu;

using UIKit;

/// <summary>
/// Handler for <see cref="UIApplicationDelegate.HandleEventsForBackgroundUrl"/>.
/// </summary>
public interface IHandleEventsForBackgroundUrlHandler
{
    /// <inheritdoc cref="UIApplicationDelegate.HandleEventsForBackgroundUrl"/>
    bool HandleEventsForBackgroundUrl(
        UIApplication application,
        string sessionIdentifier,
        Action completionHandler);
}
