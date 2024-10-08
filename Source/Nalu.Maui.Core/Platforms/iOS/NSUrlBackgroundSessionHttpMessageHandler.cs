namespace Nalu;

using Foundation;
using UIKit;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that uses a background <see cref="NSUrlSession"/> to send requests over the network.
/// </summary>
public class NSUrlBackgroundSessionHttpMessageHandler : HttpMessageHandler
{
    private static MessageHandlerNSUrlSessionDownloadDelegate MessageHandler
        => MessageHandlerNSUrlSessionDownloadDelegate.Current;

    /// <inheritdoc cref="UIApplicationDelegate.HandleEventsForBackgroundUrl"/>
    public static void HandleEventsForBackgroundUrl(
        UIApplication application,
        string sessionIdentifier,
        Action completionHandler)
        => MessageHandler.HandleEventsForBackgroundUrl(application, sessionIdentifier, completionHandler);

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => MessageHandler.SendAsync(request, cancellationToken);
}
