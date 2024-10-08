namespace Nalu;

using System.Collections.Concurrent;
using Foundation;
using Microsoft.Extensions.Logging;
using UIKit;

#pragma warning disable VSTHRD103, VSTHRD100, VSTHRD003, IDE0290, CA1848

/// <summary>
/// iOS processor for background HTTP requests.
/// </summary>
internal class MessageHandlerNSUrlSessionDownloadDelegate : NSUrlSessionDownloadDelegate
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="MessageHandlerNSUrlSessionDownloadDelegate"/>.
    /// </summary>
    public static MessageHandlerNSUrlSessionDownloadDelegate Current => _instance ??= new MessageHandlerNSUrlSessionDownloadDelegate();

    private static MessageHandlerNSUrlSessionDownloadDelegate? _instance;
    private NSUrlSession? _nsUrlSession;
    private ILogger? _logger;
    private ILogger Logger => _logger ??= GetLoggerFromApplicationServiceProvider() ?? CreateEmptyLogger();
    private readonly ConcurrentDictionary<string, RequestHandle> _pendingRequests = new();

    private NSUrlSession Session
    {
        get
        {
            if (_nsUrlSession == null)
            {
                var sessionName = $"{NSBundle.MainBundle.BundleIdentifier}.NSUrlBackgroundSessionHttpMessageHandler";
                var cfg = NSUrlSessionConfiguration.CreateBackgroundSessionConfiguration(sessionName);
                cfg.SessionSendsLaunchEvents = true;
                _nsUrlSession = NSUrlSession.FromConfiguration(cfg, this, new NSOperationQueue());
            }

            return _nsUrlSession!;
        }
    }

    private MessageHandlerNSUrlSessionDownloadDelegate()
    {
    }

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestUrl = new NSUrl(request.RequestUri?.ToString() ?? throw new ArgumentException("RequestUri cannot be null."));
        var requestIdentifier = Guid.NewGuid().ToString("N");
        var nativeHttpRequest = new NSMutableUrlRequest(requestUrl)
        {
            HttpMethod = request.Method.Method,
            Headers = GetPlatformHeaders(request),
        };

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

        var cancellationTokenRegistration = cancellationToken.Register(() =>
        {
            if (task.State is NSUrlSessionTaskState.Running or NSUrlSessionTaskState.Suspended)
            {
                task.Cancel();
            }
        });
        var requestHandle = new RequestHandle(requestIdentifier, contentPath, cancellationTokenRegistration);
        _pendingRequests[requestIdentifier] = requestHandle;

        task.TaskDescription = requestIdentifier;
        task.Resume();

        await Task.Yield();
        return await requestHandle.ResponseCompletionSource.Task.ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public override void DidBecomeInvalid(NSUrlSession session, NSError? error)
    {
        Logger.LogDebug("DidBecomeInvalid");
        _nsUrlSession = null;

        if (error != null)
        {
            Logger.LogError(new InvalidOperationException(error.ToString()), "Exception in DidBecomeInvalid");
        }
    }

    /// <inheritdoc/>
    public override void DidCompleteWithError(NSUrlSession session, NSUrlSessionTask task, NSError? error)
    {
        Logger.LogDebug("DidComplete {TaskDescription} with {State}", task.State, task.TaskDescription);

        if (task.TaskDescription is not { } requestIdentifier ||
            !_pendingRequests.TryGetValue(requestIdentifier, out var handle))
        {
            Logger.LogError("No handle found for {RequestName}", task.TaskDescription);
            return;
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
                        Logger.LogDebug("Task completed with canceled state");
                        handle.ResponseCompletionSource.TrySetCanceled();
                    }
                    else
                    {
                        var e = task.Error;
                        var msg = e?.ToString();
                        Logger.LogDebug("Task completed with error: {Error}", msg);
                        handle.ResponseCompletionSource.TrySetException(new HttpRequestException(msg));
                    }

                    CompleteAndRemoveHandle(handle);
                }

                break;
            default:
                Logger.LogError("Unknown task state {TaskState}", task.State);
                handle.ResponseCompletionSource.TrySetException(new HttpRequestException($"Unknown task state: {task.State}"));
                CompleteAndRemoveHandle(handle);
                break;
        }
    }

    /// <inheritdoc/>
    public override void DidFinishDownloading(NSUrlSession session, NSUrlSessionDownloadTask downloadTask, NSUrl location)
    {
        Logger.LogDebug("DidComplete {TaskDescription} with {Location}", downloadTask.TaskDescription, location);

        if (downloadTask.TaskDescription is not { } requestIdentifier ||
            !_pendingRequests.TryGetValue(requestIdentifier, out var handle))
        {
            Logger.LogError("No handle found for {RequestName}", downloadTask.TaskDescription);
            return;
        }

        if (downloadTask.Response is not NSHttpUrlResponse response)
        {
            Logger.LogError("Response is not NSHttpUrlResponse");
            handle.ResponseCompletionSource.TrySetException(new HttpRequestException("Response is not NSHttpUrlResponse"));
            CompleteAndRemoveHandle(handle);
            return;
        }

        var httpResponseMessage = new HttpResponseMessage(downloadTask.GetHttpStatusCode());
        var fileStream = new FileStream(location.Path!, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
        httpResponseMessage.Content = new StreamContent(fileStream);
        ApplyResponseHeaders(httpResponseMessage, response);
        handle.ResponseCompletionSource.TrySetResult(httpResponseMessage);
        CompleteAndRemoveHandle(handle);
    }

#pragma warning disable IDE0060, VSTHRD110
    public bool HandleEventsForBackgroundUrl(UIApplication application, string sessionIdentifier, Action completionHandler)
    {
        if (!ReferenceEquals(this, _nsUrlSession?.Delegate))
        {
            return false;
        }

        Logger.LogDebug("HandleEventsForBackgroundUrl");
        NotifyCompletion(completionHandler);
        return true;
    }

    private void CompleteAndRemoveHandle(RequestHandle handle)
    {
        handle.Complete();
        _pendingRequests.TryRemove(handle.Identifier, out _);
    }

    private async void NotifyCompletion(Action completionHandler)
    {
        var acknowledgeTasks = _pendingRequests.Values.Select(handle => handle.CompletedTask).ToArray();
        await Task.WhenAll(acknowledgeTasks).ConfigureAwait(false);

        // Give it a small amount of time to eventually enqueue other requests
        await Task.Delay(250).ConfigureAwait(false);

        Logger.LogDebug("HandleEventsForBackgroundUrl: Completed");
        completionHandler();
    }
#pragma warning restore IDE0060, VSTHRD110

    private void ApplyResponseHeaders(HttpResponseMessage httpResponseMessage, NSHttpUrlResponse response)
    {
        foreach (var header in response.AllHeaderFields)
        {
            var key = header.Key.ToString();
            var value = header.Value.ToString();

            var added =
                httpResponseMessage.Headers.TryAddWithoutValidation(key, value) ||
                httpResponseMessage.Content.Headers.TryAddWithoutValidation(key, value);

            if (!added)
            {
                Logger.LogWarning("Failed to add response header {HeaderKey}: {HeaderValue}", key, value);
            }
        }
    }

    private static string GetRequestBodyPath(string requestIdentifier)
        => Path.Combine(Path.GetTempPath(), $"{requestIdentifier}.nsrequest");

    private static NSDictionary GetPlatformHeaders(HttpRequestMessage request)
    {
        var requestHeaders = request.Headers
            .Select(header => (HeaderName: header.Key, HeaderValue: string.Join(';', header.Value)))
            .Where(header => !string.IsNullOrEmpty(header.HeaderValue))
            .ToDictionary();

        if (request.Content is { } content)
        {
            foreach (var contentHeader in content.Headers)
            {
                requestHeaders[contentHeader.Key] = string.Join(';', contentHeader.Value);
            }
        }

        var headers = requestHeaders
            .Select(header => (HeaderName: header.Key, HeaderValue: string.Join(';', header.Value)))
            .Where(header => !string.IsNullOrEmpty(header.HeaderValue))
            .ToArray();

        var nativeHeaders = NSDictionary.FromObjectsAndKeys(
            headers.Select(object (h) => h.HeaderValue).ToArray(),
            headers.Select(object (h) => h.HeaderName).ToArray());
        return nativeHeaders;
    }

    private static ILogger<MessageHandlerNSUrlSessionDownloadDelegate> CreateEmptyLogger() => LoggerFactory.Create(_ => { }).CreateLogger<MessageHandlerNSUrlSessionDownloadDelegate>();

    private static ILogger<MessageHandlerNSUrlSessionDownloadDelegate>? GetLoggerFromApplicationServiceProvider() => IPlatformApplication.Current?.Services.GetService<ILogger<MessageHandlerNSUrlSessionDownloadDelegate>>();

    private struct RequestHandle(
        string identifier,
        string? contentFile,
        CancellationTokenRegistration cancellationTokenRegistration)
    {
        private readonly TaskCompletionSource _completedCompletionSource = new();
        private bool _completed;
        public TaskCompletionSource<HttpResponseMessage> ResponseCompletionSource { get; } = new();
        public readonly Task CompletedTask => _completedCompletionSource.Task;
        public readonly string Identifier => identifier;

        public void Complete()
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            cancellationTokenRegistration.Dispose();
            _completedCompletionSource.SetResult();

            if (File.Exists(contentFile))
            {
                File.Delete(contentFile);
            }
        }
    }
}
