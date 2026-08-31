namespace Nalu.Maui.TestApp;

using Foundation;
using UIKit;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    [Export("application:handleEventsForBackgroundURLSession:completionHandler:")]
    public virtual void HandleEventsForBackgroundUrl(UIApplication application, string sessionIdentifier, Action completionHandler)
    {
        // Lifecycle-harness instrumentation: proves iOS woke the app for background URL events.
        Tests.BackgroundHttpLostResults.NotifyBackgroundEvents();
        NSUrlBackgroundSessionHttpMessageHandler.HandleEventsForBackgroundUrl(application, sessionIdentifier, completionHandler);
    }
}
