using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using CoreFoundation;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

namespace Nalu;

#pragma warning disable VSTHRD103, VSTHRD100, VSTHRD003, IDE0290, CA1848

/// <summary>
/// iOS processor for background HTTP requests.
/// </summary>
internal partial class MessageHandlerNSUrlSessionDownloadDelegate : NSUrlSessionDownloadDelegate
{
    private const string SetCookieHeaderKey = "Set-Cookie";
    private const string CookieHeaderKey = "Cookie";

    /// <summary>
    /// Gets the singleton instance of the <see cref="MessageHandlerNSUrlSessionDownloadDelegate" />.
    /// </summary>
    public static MessageHandlerNSUrlSessionDownloadDelegate Current => _instance ??= new MessageHandlerNSUrlSessionDownloadDelegate();

    public static string SessionIdentifier { get; } = $"{NSBundle.MainBundle.BundleIdentifier}.NSUrlBackgroundSessionHttpMessageHandler";

    private static readonly TimeSpan _eventProcessingWaitThreshold = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan _infiniteTimeout = Timeout.InfiniteTimeSpan;
    private static MessageHandlerNSUrlSessionDownloadDelegate? _instance;
    private readonly ILogger _emptyLogger = CreateEmptyLogger();
    private NSUrlSession? _nsUrlSession;
    private Action? _processingInBackgroundCompletionHandler;
    private long _lastCompletedTaskTimestamp = Stopwatch.GetTimestamp();
    private ILogger? _logger;

    private ILogger Logger
    {
        get
        {
            _logger ??= GetLoggerFromApplicationServiceProvider();

            return _logger ?? _emptyLogger;
        }
    }

    private readonly ConcurrentDictionary<string, NSUrlRequestHandle> _pendingRequests = new();
    private readonly ConcurrentDictionary<string, NSUrlRequestHandle> _processingInBackgroundHandles = [];

    private NSUrlSession Session
    {
        get
        {
            if (_nsUrlSession == null)
            {
                var config = NSUrlSessionConfiguration.CreateBackgroundSessionConfiguration(SessionIdentifier);
                config.SessionSendsLaunchEvents = true;

                // We want, by default, the timeout from HttpClient to have precedence over the one from NSUrlSession
                // Double.MaxValue does not work, so default to 24 hours
                config.TimeoutIntervalForRequest = 24 * 60 * 60;
                config.TimeoutIntervalForResource = 24 * 60 * 60;

                _nsUrlSession = NSUrlSession.FromConfiguration(config, this, new NSOperationQueue());
            }

            return _nsUrlSession!;
        }
    }

    private MessageHandlerNSUrlSessionDownloadDelegate()
    {
        _ = Session;
    }

    public IReadOnlyDictionary<string, Task<HttpResponseMessage>> GetPendingResponses()
        => _pendingRequests.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ResponseCompletionSource.Task);

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CookieContainer? cookieContainer, TimeSpan defaultTimeout, CancellationToken cancellationToken)
    {
        var requestUrl = new NSUrl(request.RequestUri?.ToString() ?? throw new ArgumentException("RequestUri cannot be null."));
        var requestIdentifier = TryGetRequestIdentifier(request, out var id) ? id : Guid.NewGuid().ToString("N");

        var nativeHttpRequest = new NSMutableUrlRequest(requestUrl)
                                {
                                    HttpMethod = request.Method.Method
                                };

        if (defaultTimeout != _infiniteTimeout)
        {
            nativeHttpRequest.TimeoutInterval = defaultTimeout.TotalSeconds;
        }

        string? contentPath = null;

        if (request.Content is { } content)
        {
            if (content is MultipartContent or StreamContent)
            {
                contentPath = GetRequestBodyPath(requestIdentifier);
                await using var fileStream = File.Create(contentPath);
                await content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await using var memoryStream = new MemoryStream();
                await content.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
                var body = memoryStream.ToArray();
                nativeHttpRequest.Body = NSData.FromArray(body);
            }
        }

        nativeHttpRequest.Headers = GetPlatformHeaders(request, cookieContainer);

        NSUrlSessionTask task;

        if (contentPath != null)
        {
            var fileUrl = NSUrl.CreateFileUrl(contentPath, null);
            task = Session.CreateUploadTask(nativeHttpRequest, fileUrl);
            Logger.LogDebug("Created upload task for {RequestName} with content stored in {BodyPath}", requestIdentifier, contentPath);
        }
        else
        {
            task = Session.CreateDownloadTask(nativeHttpRequest);
            Logger.LogDebug("Created download task for {RequestName}", requestIdentifier);
        }

        var weakTask = new WeakReference<NSUrlSessionTask>(task);

        var cancellationTokenRegistration = cancellationToken.Register(() =>
            {
                if (weakTask.TryGetTarget(out var t) && t?.State is NSUrlSessionTaskState.Running or NSUrlSessionTaskState.Suspended)
                {
                    t.Cancel();
                }
            }
        );

        var requestHandle = new NSUrlRequestHandle(requestIdentifier, cookieContainer, contentPath, cancellationTokenRegistration);
        _pendingRequests[requestIdentifier] = requestHandle;

        task.TaskDescription = requestIdentifier;
        task.Resume();

        await Task.Yield();

        return await requestHandle.ResponseCompletionSource.Task.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override void DidBecomeInvalid(NSUrlSession session, NSError? error)
    {
        Logger.LogDebug("DidBecomeInvalid");
        _nsUrlSession = null;

        if (error != null)
        {
            Logger.LogError(new InvalidOperationException(error.ToString()), "Exception in DidBecomeInvalid");
        }
    }

    /// <inheritdoc />
    public override void DidCompleteWithError(NSUrlSession session, NSUrlSessionTask task, NSError? error)
    {
        _lastCompletedTaskTimestamp = Stopwatch.GetTimestamp();

        Logger.LogDebug("DidCompleteWithError {TaskDescription} with {State}", task.TaskDescription, task.State);

        if (string.IsNullOrWhiteSpace(task.TaskDescription))
        {
            Logger.LogError("DidCompleteWithError TaskDescription is null or empty");

            return;
        }

        var requestIdentifier = task.TaskDescription!;

        if (!_pendingRequests.TryGetValue(requestIdentifier, out var handle))
        {
            handle = new NSUrlRequestHandle(requestIdentifier, null, null, default, true);
            _pendingRequests[requestIdentifier] = handle;
        }

        if (_processingInBackgroundCompletionHandler is not null)
        {
            var added = _processingInBackgroundHandles.TryAdd(requestIdentifier, handle);
            Logger.LogDebug("Tracking request {RequestIdentifier} for background processing: {Added}", requestIdentifier, added);
        }

        switch (task.State)
        {
            case NSUrlSessionTaskState.Running:
                Logger.LogError("Task {RequestIdentifier} completed callback invoked with running state: {Error}", requestIdentifier, error?.ToString());
                handle.ResponseCompletionSource.TrySetException(new InvalidOperationException("Task completed callback invoked with running state"));
                CompleteAndRemoveHandle(handle);

                break;
            case NSUrlSessionTaskState.Suspended:
                Logger.LogError("Task {RequestIdentifier} completed callback invoked with suspended state: {Error}", requestIdentifier, error?.ToString());
                handle.ResponseCompletionSource.TrySetException(new InvalidOperationException("Task completed callback invoked with suspended state"));
                CompleteAndRemoveHandle(handle);

                break;
            case NSUrlSessionTaskState.Canceling:
                Logger.LogDebug("Task {RequestIdentifier} completed with canceling state", requestIdentifier);
                handle.ResponseCompletionSource.TrySetCanceled();
                CompleteAndRemoveHandle(handle);

                break;
            case NSUrlSessionTaskState.Completed:
                if (task.Error != null)
                {
                    if (task.IsCanceled())
                    {
                        Logger.LogDebug("Task {RequestIdentifier} completed with canceled state", requestIdentifier);
                        handle.ResponseCompletionSource.TrySetCanceled();
                    }
                    else
                    {
                        var e = task.Error;
                        var msg = e?.ToString();
                        Logger.LogDebug("Task {RequestIdentifier} completed with error: {Error}", requestIdentifier, msg);
                        handle.ResponseCompletionSource.TrySetException(new HttpRequestException(msg));
                    }

                    CompleteAndRemoveHandle(handle);
                }
                else
                {
                    Logger.LogDebug("Task {RequestIdentifier} completed with success", requestIdentifier);
                }

                break;
            default:
                Logger.LogError("Task {RequestIdentifier} unknown task state {TaskState}", requestIdentifier, task.State);
                handle.ResponseCompletionSource.TrySetException(new HttpRequestException($"Unknown task state: {task.State}"));
                CompleteAndRemoveHandle(handle);

                break;
        }
    }

    /// <inheritdoc />
    public override void DidFinishDownloading(NSUrlSession session, NSUrlSessionDownloadTask task, NSUrl location)
    {
        _lastCompletedTaskTimestamp = Stopwatch.GetTimestamp();

        if (string.IsNullOrWhiteSpace(task.TaskDescription))
        {
            Logger.LogError("DidFinishDownloading TaskDescription is null or empty");

            return;
        }

        var requestIdentifier = task.TaskDescription!;
        Logger.LogDebug("DidFinishDownloading {RequestIdentifier}", requestIdentifier);

        if (!_pendingRequests.TryGetValue(requestIdentifier, out var handle))
        {
            handle = new NSUrlRequestHandle(requestIdentifier, null, null, default, true);
            _pendingRequests[requestIdentifier] = handle;
        }

        if (_processingInBackgroundCompletionHandler is not null)
        {
            var added = _processingInBackgroundHandles.TryAdd(requestIdentifier, handle);
            Logger.LogDebug("Tracking request {RequestIdentifier} for background processing: {Added}", requestIdentifier, added);
        }

        if (task.Response is not NSHttpUrlResponse response)
        {
            Logger.LogError("Response is not NSHttpUrlResponse");
            handle.ResponseCompletionSource.TrySetException(new HttpRequestException("Response is not NSHttpUrlResponse"));
            CompleteAndRemoveHandle(handle);

            return;
        }

        // https://developer.apple.com/documentation/foundation/urlsessiondownloaddelegate/1411575-urlsession/
        // Because the file is temporary, you must either open the file for reading or move it to a permanent location in your app’s sandbox container directory before returning from this delegate method.
        // We should be good with a temporary file path considering we're going to read it right away.
        var locationPath = location.Path;
        handle.ResponseContentFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".nsresponse");

        try
        {
            File.Move(locationPath!, handle.ResponseContentFile, true);
        }
        catch (Exception ex)
        {
            handle.ResponseCompletionSource.TrySetException(new HttpRequestException("Temporary response file is gone", ex));

            return;
        }
        finally
        {
            // This might have taken a while, so let's update the last completed task timestamp
            _lastCompletedTaskTimestamp = Stopwatch.GetTimestamp();
        }

        var httpResponseMessage = new HttpResponseMessage(task.GetHttpStatusCode())
                                  {
                                      RequestMessage = CreateHttpRequestMessage(task.CurrentRequest ?? task.OriginalRequest)
                                  };

        var fileStream = new FileStream(handle.ResponseContentFile, FileMode.Open, FileAccess.Read, FileShare.Read);
        httpResponseMessage.Content = new AcknowledgingStreamContent(this, handle, fileStream);
        ApplyResponseHeaders(httpResponseMessage, response, handle.CookieContainer);
        Logger.LogDebug("DidFinishDownloading set response for {RequestIdentifier}", requestIdentifier);
        handle.ResponseCompletionSource.TrySetResult(httpResponseMessage);

        if (handle.IsLostRequest)
        {
            MainThread.BeginInvokeOnMainThread(HandleLostMessage);
        }

        void HandleLostMessage()
        {
            var lostMessageHandler = GetLostMessageHandler();

            if (lostMessageHandler != null)
            {
                var responseHandle = new NSUrlBackgroundResponseHandle(requestIdentifier, handle.ResponseCompletionSource.Task);
                _ = lostMessageHandler.HandleLostMessageAsync(responseHandle);
            }
            else
            {
                CompleteAndRemoveHandle(handle);
            }
        }
    }

#pragma warning disable IDE0060, VSTHRD110
    public bool HandleEventsForBackgroundUrl(UIApplication application, string sessionIdentifier, Action completionHandler)
    {
        if (!ReferenceEquals(this, _nsUrlSession?.Delegate))
        {
            return false;
        }

        Logger.LogDebug("HandleEventsForBackgroundUrl");
        _processingInBackgroundCompletionHandler = completionHandler;
        _lastCompletedTaskTimestamp = Stopwatch.GetTimestamp();
        WaitEventsProcessingAndNotify();

        return true;
    }
#pragma warning restore IDE0060, VSTHRD110

    internal void CompleteAndRemoveHandle(NSUrlRequestHandle handle)
    {
        handle.Complete();
        _pendingRequests.TryRemove(handle.Identifier, out _);
    }

    private async void WaitEventsProcessingAndNotify()
    {
        if (_processingInBackgroundCompletionHandler is not { } completionHandler)
        {
            return;
        }

        var maxWaitTime = 6500;

        while (Stopwatch.GetElapsedTime(_lastCompletedTaskTimestamp) < _eventProcessingWaitThreshold)
        {
            await Task.Delay(200).ConfigureAwait(false);
            maxWaitTime -= 200;
        }

        var acknowledgeTasks = _processingInBackgroundHandles.Values.Select(h => h.CompletedTask).ToList();
        await Task.WhenAny(Task.WhenAll(acknowledgeTasks), Task.Delay(Math.Max(500, maxWaitTime))).ConfigureAwait(false);

        _processingInBackgroundHandles.Clear();
        _processingInBackgroundCompletionHandler = null;
        Logger.LogDebug("WaitEventsProcessingAndNotify Completed");

        DispatchQueue.MainQueue.DispatchAsync(completionHandler);
    }

    private HttpRequestMessage? CreateHttpRequestMessage(NSUrlRequest? taskRequest)
    {
        if (taskRequest is null)
        {
            return null;
        }

        var request = new HttpRequestMessage(new HttpMethod(taskRequest.HttpMethod), taskRequest.Url)
                      {
                          Content = taskRequest.Body is { } body ? new StreamContent(body.AsStream()) : null
                      };

        foreach (var header in taskRequest.Headers)
        {
            if (header.Value is null || header.Key is null)
            {
                continue;
            }

            var key = header.Key.ToString();

            if (key == SetCookieHeaderKey)
            {
                continue;
            }

            var value = header.Value.ToString();

            var added =
                request.Headers.TryAddWithoutValidation(key, value) ||
                (request.Content?.Headers.TryAddWithoutValidation(key, value) ?? false);

            if (!added)
            {
                Logger.LogWarning("Failed to add request header on response's request message {HeaderKey}: {HeaderValue}", key, value);
            }
        }

        return request;
    }

    private void ApplyResponseHeaders(HttpResponseMessage httpResponseMessage, NSHttpUrlResponse response, CookieContainer? cookieContainer)
    {
        foreach (var header in response.AllHeaderFields)
        {
            if (header.Value is null || header.Key is null)
            {
                continue;
            }

            var key = header.Key.ToString();

            if (key == SetCookieHeaderKey)
            {
                continue;
            }

            var value = header.Value.ToString();

            var added =
                httpResponseMessage.Headers.TryAddWithoutValidation(key, value) ||
                httpResponseMessage.Content.Headers.TryAddWithoutValidation(key, value);

            if (!added)
            {
                Logger.LogWarning("Failed to add response header {HeaderKey}: {HeaderValue}", key, value);
            }
        }

        if (Session.Configuration.HttpCookieStorage is { } cookieStorage && cookieContainer is not null)
        {
            var responseUrl = response.Url;
            var absoluteUri = new Uri(responseUrl.AbsoluteString!);
            var cookies = cookieStorage.CookiesForUrl(responseUrl);
            UpdateManagedCookieContainer(cookieContainer, absoluteUri, cookies);

            for (var index = 0; index < cookies.Length; index++)
            {
                httpResponseMessage.Headers.TryAddWithoutValidation(SetCookieHeaderKey, cookies[index].GetHeaderValue());
            }
        }
    }

    private static void UpdateManagedCookieContainer(CookieContainer cookieContainer, Uri absoluteUri, NSHttpCookie[] cookies)
    {
        if (cookies.Length > 0)
        {
            lock (cookieContainer)
            {
                // As per docs: The contents of an HTTP set-cookie header as returned by an HTTP server, with Cookie instances delimited by commas.
                var cookiesContents = Array.ConvertAll(cookies, static cookie => cookie.GetHeaderValue());
                cookieContainer.SetCookies(absoluteUri, string.Join(',', cookiesContents));
            }
        }
    }

    private static INSUrlBackgroundSessionLostMessageHandler? GetLostMessageHandler() =>
        IPlatformApplication.Current?
                            .Services
                            .GetService<INSUrlBackgroundSessionLostMessageHandler>();

    private static string GetRequestBodyPath(string requestIdentifier)
        => Path.Combine(Path.GetTempPath(), $"{requestIdentifier}.nsrequest");

    private NSMutableDictionary GetPlatformHeaders(HttpRequestMessage request, CookieContainer? cookieContainer)
    {
        var headers = new Dictionary<string, string>();
        AddManagedHeaders(headers, request.Headers);

        if (request.Content is { } content)
        {
            AddManagedHeaders(headers, content.Headers);
        }

        var enumeratedHeaders = headers.ToArray();

        var nativeHeaders = NSMutableDictionary.FromObjectsAndKeys(
            enumeratedHeaders.Select(object (h) => h.Value).ToArray(),
            enumeratedHeaders.Select(object (h) => h.Key).ToArray()
        );

        // set header cookies if needed from the managed cookie container if we do use Cookies
        if (Session.Configuration.HttpCookieStorage is not null && cookieContainer is not null)
        {
            // As per docs: An HTTP cookie header, with strings representing Cookie instances delimited by semicolons.
            var cookies = cookieContainer.GetCookieHeader(request.RequestUri!);

            if (!string.IsNullOrEmpty(cookies))
            {
#pragma warning disable CS0618
                var cookiePtr = NSString.CreateNative(CookieHeaderKey);
                var cookiesPtr = NSString.CreateNative(cookies);
#pragma warning restore CS0618
                nativeHeaders.LowlevelSetObject(cookiesPtr, cookiePtr);
                NSString.ReleaseNative(cookiePtr);
                NSString.ReleaseNative(cookiesPtr);
            }
        }

        return nativeHeaders;
    }

    private static void AddManagedHeaders(Dictionary<string, string> headers, HttpHeaders managedHeaders)
    {
        var enumeratedManagedHeaders = managedHeaders.ToString().Split('\n');
        var regex = HeaderValueRegex();

        foreach (var header in enumeratedManagedHeaders)
        {
            var match = regex.Match(header);

            if (match.Success)
            {
                var key = match.Groups[1].Value;
                var value = match.Groups[2].Value;
                headers[key] = value;
            }
        }
    }

    private static bool TryGetRequestIdentifier(HttpRequestMessage request, [NotNullWhen(true)] out string? requestIdentifier)
    {
        if (request.Headers.TryGetValues(NSUrlBackgroundSessionHttpMessageHandler.RequestIdentifierHeaderName, out var values))
        {
            var id = values.FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(id))
            {
                requestIdentifier = id;

                return true;
            }
        }

        requestIdentifier = null;

        return false;
    }

    private static ILogger<MessageHandlerNSUrlSessionDownloadDelegate> CreateEmptyLogger()
        => LoggerFactory.Create(_ => { }).CreateLogger<MessageHandlerNSUrlSessionDownloadDelegate>();

    private static ILogger<MessageHandlerNSUrlSessionDownloadDelegate>? GetLoggerFromApplicationServiceProvider()
        => IPlatformApplication.Current?.Services?.GetService<ILogger<MessageHandlerNSUrlSessionDownloadDelegate>>();

    [GeneratedRegex("(.+?): (.+)")]
    private static partial Regex HeaderValueRegex();
}
