using System.Diagnostics.CodeAnalysis;
using System.Net;
using Foundation;
using ObjCRuntime;
using UIKit;

namespace Nalu;

// ReSharper disable InconsistentNaming
/// <summary>
/// An <see cref="HttpMessageHandler" /> that uses a background <see cref="NSUrlSession" /> to send requests over the network.
/// </summary>
[SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "We want to follow the pattern of HttpClientHandler.")]
public class NSUrlBackgroundSessionHttpMessageHandler : HttpMessageHandler
{
    /// <summary>
    /// Holds the pending responses that have not been acknowledged yet.
    /// </summary>
    public static IReadOnlyDictionary<string, Task<HttpResponseMessage>> GetPendingResponses()
        => MessageHandler.GetPendingResponses();

    /// <summary>
    /// Gets or sets the <see cref="CookieContainer" /> used to store cookies.
    /// </summary>
    public CookieContainer? CookieContainer
    {
        get => _cookieContainer;
        set
        {
            EnsureModifiability();
            _cookieContainer = value;
        }
    }

    /// <summary>
    /// Gets or sets the default timeout for requests.
    /// </summary>
    /// <remarks>
    /// iOS IGNORES the per-request native timeout on background sessions (verified on-device
    /// with the chaos harness: a 3s value against a stalled server never fires), so this is
    /// enforced MANAGED-side: when it elapses the transfer is canceled and the request throws a
    /// <see cref="TaskCanceledException" /> wrapping a <see cref="TimeoutException" /> — the
    /// same convention as <see cref="HttpClient.Timeout" />. Because enforcement is managed, it
    /// only ticks while the app is alive: a transfer surviving a process kill is governed by
    /// the session's resource timeout instead.
    /// </remarks>
    public TimeSpan DefaultTimeout
    {
        get => _defaultTimeout;
        set
        {
            EnsureModifiability();
            _defaultTimeout = value;
        }
    }

    /// <summary>
    /// The name of the header that contains the identifier of the <see cref="NSUrlSessionTask" /> associated with the request.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When this header is set on a request, even if the app is terminated by the system, once the app is relaunched and the request completes,
    /// the request will be found in the <see cref="NSUrlBackgroundSessionHttpMessageHandler.GetPendingResponses" /> collection.
    /// </para>
    /// <para>
    /// An identifier maps to ONE transfer: sending a request while one with the same identifier
    /// is still in flight does not start a new transfer — it ATTACHES to the pending one and
    /// returns its response, exactly as if it had awaited the original call. The duplicate's
    /// <see cref="CancellationToken" /> only stops its own wait, never the shared transfer.
    /// Once the request completes (success or failure) the identifier is free again, so
    /// retrying after a failure starts a fresh transfer.
    /// </para>
    /// </remarks>
    public const string RequestIdentifierHeaderName = "X-NSUrlRequest-Identifier";

    /// <inheritdoc cref="HttpClientHandler.SupportsAutomaticDecompression" />
    // There's no way to turn off automatic decompression, so yes, we support it
    public bool SupportsAutomaticDecompression => true;

    /// <inheritdoc cref="HttpClientHandler.SupportsProxy" />
    // We don't support using custom proxies, but NSUrlSession will automatically use any proxies configured in the OS.
    public bool SupportsProxy => false;

    /// <inheritdoc cref="HttpClientHandler.SupportsRedirectConfiguration" />
    // We support the AllowAutoRedirect property, but we don't support changing the MaxAutomaticRedirections value,
    // so be safe here and say we don't support redirect configuration.
    public bool SupportsRedirectConfiguration => false;

    /// <inheritdoc cref="HttpClientHandler.UseProxy" />
    // NSUrlSession will automatically use any proxies configured in the OS (so always return true in the getter).
    // There doesn't seem to be a way to turn this off, so throw if someone attempts to disable this.
    public bool UseProxy
    {
        get => true;
        set
        {
            if (!value)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(value), value, "It's not possible to disable the use of system proxies.");
            }
        }
    }

    /// <inheritdoc cref="HttpClientHandler.AllowAutoRedirect" />
    // Background NSUrlSession will automatically follow redirects.
    public bool AllowAutoRedirect
    {
        get => true;
        set
        {
            if (!value)
            {
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(value), value, "It's not possible to disable auto redirects on background NSUrlSession.");
            }
        }
    }

    private bool _sentRequest;
    private CookieContainer? _cookieContainer;

    // Do not set a predefined timeout, as the background session will handle the timeout.
    private TimeSpan _defaultTimeout = Timeout.InfiniteTimeSpan;

    /// <summary>
    /// Initializes a new instance of the <see cref="NSUrlBackgroundSessionHttpMessageHandler" /> class.
    /// </summary>
    /// <exception cref="PlatformNotSupportedException">this handler is not supported in iOS simulators, use the default one instead.</exception>
    public NSUrlBackgroundSessionHttpMessageHandler()
    {
        if (DeviceInfo.DeviceType == DeviceType.Virtual)
        {
            throw new PlatformNotSupportedException("Background sessions are not supported in the simulator. Check DeviceInfo.DeviceType before creating this handler.");
        }
    }

    private void EnsureModifiability()
    {
        if (_sentRequest)
        {
            throw new InvalidOperationException(
                "This instance has already started one or more requests. " +
                "Properties can only be modified before sending the first request."
            );
        }
    }

    private static MessageHandlerNSUrlSessionDownloadDelegate MessageHandler { get; } = MessageHandlerNSUrlSessionDownloadDelegate.Current;

    /// <inheritdoc cref="UIApplicationDelegate.HandleEventsForBackgroundUrl" />
    public static void HandleEventsForBackgroundUrl(
        UIApplication application,
        string sessionIdentifier,
        Action completionHandler
    )
    {
        if (sessionIdentifier == MessageHandlerNSUrlSessionDownloadDelegate.SessionIdentifier)
        {
            MessageHandler.HandleEventsForBackgroundUrl(application, sessionIdentifier, completionHandler);
        }
    }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _sentRequest = true;

        return MessageHandler.SendAsync(request, _cookieContainer, DefaultTimeout, cancellationToken);
    }
}
