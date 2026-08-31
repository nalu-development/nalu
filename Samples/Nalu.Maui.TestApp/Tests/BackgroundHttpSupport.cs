#if IOS && !MACCATALYST
using System.Net;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// One shared background-session <see cref="HttpClient" /> for every background-HTTP harness
/// page: a real device goes through the public <see cref="NSUrlBackgroundSessionHttpMessageHandler" />,
/// the SIMULATOR through the internal delegate directly (same pipeline, minus the ctor guard),
/// falling back to a default client when the simulator OS refuses background configurations.
/// </summary>
internal static class BackgroundHttpClientFactory
{
    private sealed class DirectBackgroundHandler : HttpMessageHandler
    {
        public CookieContainer Cookies { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => MessageHandlerNSUrlSessionDownloadDelegate.Current.SendAsync(request, Cookies, Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private static readonly Lazy<(HttpClient Client, string Mode)> _client = new(() =>
        {
            if (DeviceInfo.DeviceType != DeviceType.Virtual)
            {
                var handler = new NSUrlBackgroundSessionHttpMessageHandler { CookieContainer = new CookieContainer() };

                return (new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }, "device");
            }

            try
            {
                var handler = new DirectBackgroundHandler();
                _ = MessageHandlerNSUrlSessionDownloadDelegate.Current;

                return (new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }, "simulator-bg");
            }
            catch (Exception)
            {
                return (new HttpClient { Timeout = Timeout.InfiniteTimeSpan }, "simulator-default");
            }
        }
    );

    public static HttpClient Client => _client.Value.Client;

    /// <summary>"device", "simulator-bg" (background pipeline on the simulator) or "simulator-default".</summary>
    public static string Mode => _client.Value.Mode;
}

/// <summary>
/// Process-wide record of LOST background responses (requests whose owning process died) and of
/// <c>handleEventsForBackgroundURLSession</c> invocations — the observable outcome of the
/// kill/relaunch and backgrounding lifecycle scenarios. Fresh statics after a relaunch by
/// construction, which is exactly what those scenarios need.
/// </summary>
public static class BackgroundHttpLostResults
{
    private static readonly Lock _lock = new();
    private static readonly List<string> _lines = [];

    public static event Action? Changed;

    public static int LostOk { get; private set; }
    public static int LostErr { get; private set; }
    public static long LostBytes { get; private set; }
    public static int BackgroundEvents { get; private set; }

    public static IReadOnlyList<string> Lines
    {
        get
        {
            lock (_lock)
            {
                return [.. _lines];
            }
        }
    }

    public static string Summary
    {
        get
        {
            lock (_lock)
            {
                return FormattableString.Invariant($"lost={LostOk + LostErr} ok={LostOk} err={LostErr}");
            }
        }
    }

    public static void RecordOk(string requestIdentifier, int statusCode, long contentLength)
    {
        lock (_lock)
        {
            LostOk++;
            LostBytes += contentLength;
            _lines.Add(FormattableString.Invariant($"LOST {requestIdentifier} OK {statusCode} len={contentLength}"));
        }

        Changed?.Invoke();
    }

    public static void RecordError(string requestIdentifier, string message)
    {
        lock (_lock)
        {
            LostErr++;
            _lines.Add(FormattableString.Invariant($"LOST {requestIdentifier} ERR {message}"));
        }

        Changed?.Invoke();
    }

    public static void NotifyBackgroundEvents()
    {
        lock (_lock)
        {
            BackgroundEvents++;
        }

        Changed?.Invoke();
    }

    public static void Reset()
    {
        lock (_lock)
        {
            LostOk = 0;
            LostErr = 0;
            LostBytes = 0;
            BackgroundEvents = 0;
            _lines.Clear();
        }

        Changed?.Invoke();
    }
}
#endif
