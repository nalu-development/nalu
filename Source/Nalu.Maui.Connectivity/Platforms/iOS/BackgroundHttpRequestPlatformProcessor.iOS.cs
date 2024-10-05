namespace Nalu;

using Foundation;
using Microsoft.Extensions.Logging;
using Nalu.Internals;
using UIKit;

#pragma warning disable VSTHRD103, VSTHRD100

/// <summary>
/// iOS processor for background HTTP requests.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BackgroundHttpRequestPlatformProcessor"/> class.
/// </remarks>
/// <param name="manager">The manager.</param>
/// <param name="logger">The logger.</param>
public class BackgroundHttpRequestPlatformProcessor(IBackgroundHttpRequestManager manager, ILogger<BackgroundHttpRequestPlatformProcessor> logger)
    : NSUrlSessionDownloadDelegate, IBackgroundHttpRequestPlatformProcessor, IHandleEventsForBackgroundUrlHandler
{
    internal static string BackgroundPingTaskIdentifier => $"{NSBundle.MainBundle.BundleIdentifier}.nalu-bg-http-ping";

    private NSUrlSession? _nsUrlSession;

    private NSUrlSession Session
    {
        get
        {
            if (_nsUrlSession == null)
            {
                var sessionName = $"{NSBundle.MainBundle.BundleIdentifier}.NaluBackgroundHttpRequests";
                var cfg = NSUrlSessionConfiguration.CreateBackgroundSessionConfiguration(sessionName);
                cfg.SessionSendsLaunchEvents = true;
                _nsUrlSession = NSUrlSession.FromConfiguration(cfg, this, new NSOperationQueue());
            }

            return _nsUrlSession!;
        }
    }

    /// <inheritdoc/>
    public async Task StartPlatformRequestAsync(BackgroundHttpClient client, BackgroundHttpRequestMessage request, BackgroundHttpRequestHandle handle)
    {
        var requestUrl = new NSUrl(request.GetRequestUri(client).ToString());
        var nativeHttpRequest = new NSMutableUrlRequest(requestUrl)
        {
            HttpMethod = request.Method.Method,
            Headers = GetPlatformHeaders(request),
        };

        string? serializedBodyPath = null;
        if (request.Content is { } content)
        {
            if (handle.IsMultiPart)
            {
                serializedBodyPath = GetRequestBodyPath(handle.RequestName);
                await using var fileStream = File.Create(serializedBodyPath);
                await content.CopyToAsync(fileStream).ConfigureAwait(false);
            }
            else
            {
                await using var memoryStream = new MemoryStream();
                await content.CopyToAsync(memoryStream).ConfigureAwait(false);
                var body = memoryStream.ToArray();
                nativeHttpRequest.Body = NSData.FromArray(body);
            }
        }

        NSUrlSessionTask task;
        if (serializedBodyPath != null)
        {
            var fileUrl = NSUrl.CreateFileUrl(serializedBodyPath, null);
            task = Session.CreateUploadTask(nativeHttpRequest, fileUrl);
            logger.LogInformation("Created upload task for {RequestName} with body {BodyPath}", request.RequestName, serializedBodyPath);
        }
        else
        {
            task = Session.CreateDownloadTask(nativeHttpRequest);
            logger.LogInformation("Created download task for {RequestName}", request.RequestName);
        }

        task.TaskDescription = request.RequestName;
        task.Resume();
        handle.SetRunning();
    }

    /// <inheritdoc/>
    public override void DidBecomeInvalid(NSUrlSession session, NSError? error)
    {
        logger.LogDebug("DidBecomeInvalid");
        _nsUrlSession = null;

        if (error != null)
        {
            logger.LogError(new InvalidOperationException(error.ToString()), "Exception in DidBecomeInvalid");
        }
    }

    /// <inheritdoc/>
    public override void DidSendBodyData(NSUrlSession session, NSUrlSessionTask task, long bytesSent, long totalBytesSent, long totalBytesExpectedToSend)
    {
        logger.LogDebug("DidSendBodyData: {TaskDescription} - {TotalBytesSent} / {TotalBytesExpectedToSend}", task.TaskDescription, totalBytesSent, totalBytesExpectedToSend);

        if (task.TaskDescription is not { } requestName ||
            manager.GetHandle(requestName) is not { } handle)
        {
            logger.LogError("No handle found for {RequestName}", task.TaskDescription);
            return;
        }

        if (totalBytesExpectedToSend <= 0)
        {
            logger.LogWarning("Cannot compute progress: TotalBytesExpectedToSend is 0 or less");
            return;
        }

        handle.UpdateProgress(totalBytesSent / (double)totalBytesExpectedToSend * 0.5);
    }

    /// <inheritdoc/>
    public override void DidWriteData(NSUrlSession session, NSUrlSessionDownloadTask downloadTask, long bytesWritten, long totalBytesWritten, long totalBytesExpectedToWrite)
    {
        logger.LogDebug("DidSendBodyData: {TaskDescription} - {TotalBytesWritten} / {TotalBytesExpectedToWrite}", downloadTask.TaskDescription, totalBytesWritten, totalBytesExpectedToWrite);

        if (downloadTask.TaskDescription is not { } requestName ||
            manager.GetHandle(requestName) is not { } handle)
        {
            logger.LogError("No handle found for {RequestName}", downloadTask.TaskDescription);
            return;
        }

        if (totalBytesExpectedToWrite <= 0)
        {
            logger.LogWarning("Cannot compute progress: TotalBytesExpectedToWrite is 0 or less");
            return;
        }

        handle.UpdateProgress(0.5 + (totalBytesWritten / (double)totalBytesExpectedToWrite * 0.5));
    }

    /// <inheritdoc/>
    public override void DidCompleteWithError(NSUrlSession session, NSUrlSessionTask task, NSError? error)
    {
        logger.LogDebug("DidComplete{State}: {TaskDescription}", task.State, task.TaskDescription);

        if (task.TaskDescription is not { } requestName ||
            manager.GetHandle(requestName) is not { } handle)
        {
            logger.LogError("No handle found for {RequestName}", task.TaskDescription);
            return;
        }

        switch (task.State)
        {
            case NSUrlSessionTaskState.Running:
                logger.LogError("Task completed callback invoked with running state: {Error}", error?.ToString());
                break;
            case NSUrlSessionTaskState.Suspended:
                handle.SetPaused();
                break;
            case NSUrlSessionTaskState.Canceling:
                DeleteRequestBody(handle);
                handle.SetCanceled();
                break;
            case NSUrlSessionTaskState.Completed:
                DeleteRequestBody(handle);
                if (task.Error != null)
                {
                    if (task.IsCanceled())
                    {
                        logger.LogDebug("Task completed with canceled state");
                        handle.SetCanceled();
                    }
                    else
                    {
                        var e = task.Error;
                        var msg = e?.ToString();
                        logger.LogDebug("Task completed with error: {Error}", msg);
                        handle.SetError(new HttpRequestException(msg));
                    }
                }

                break;
            default:
                logger.LogError("Unknown task state {TaskState}", task.State);
                break;
        }
    }

    /// <inheritdoc/>
    public override void DidFinishDownloading(NSUrlSession session, NSUrlSessionDownloadTask downloadTask, NSUrl location)
    {
        logger.LogDebug("DidFinishDownloading {RequestName}: {Location}", downloadTask.TaskDescription, location);

        if (downloadTask.TaskDescription is not { } requestName ||
            manager.GetHandle(requestName) is not { } handle)
        {
            logger.LogError("No handle found for {RequestName}", downloadTask.TaskDescription);
            return;
        }

        if (downloadTask.Response is not NSHttpUrlResponse response)
        {
            logger.LogError("Response is not NSHttpUrlResponse");
            handle.SetError(new HttpRequestException("Response is not NSHttpUrlResponse"));
            return;
        }

        var httpResponseMessage = new HttpResponseMessage(downloadTask.GetHttpStatusCode());
        var fileStream = new FileStream(location.Path!, FileMode.Open, FileAccess.Read);
        httpResponseMessage.Content = new StreamContent(fileStream);
        ApplyResponseHeaders(httpResponseMessage, response);
        handle.SetResult(httpResponseMessage);
    }

    /// <inheritdoc />
    public bool HandleEventsForBackgroundUrl(UIApplication application, string sessionIdentifier, Action completionHandler)
    {
        if (!ReferenceEquals(this, _nsUrlSession?.Delegate))
        {
            return false;
        }

        logger.LogDebug("HandleEventsForBackgroundUrl");

        var completionTimeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
        var acknowledgedTasks = manager.GetHandles()
            .Where(handle => handle.State is BackgroundHttpRequestState.Running or BackgroundHttpRequestState.Paused)
            .Select(handle => handle.AcknowledgeTask);

#pragma warning disable VSTHRD110
        Task.WhenAny(completionTimeoutTask, Task.WhenAll(acknowledgedTasks))
            .ContinueWith(CompletionDelegate, null, TaskScheduler.Current);
#pragma warning restore VSTHRD110

        return true;

        void CompletionDelegate(Task task, object? state)
        {
            logger.LogDebug("HandleEventsForBackgroundUrl: Completed");
            completionHandler();
        }
    }

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
                logger.LogWarning("Failed to add response header {HeaderKey}: {HeaderValue}", key, value);
            }
        }
    }

    private static void DeleteRequestBody(BackgroundHttpRequestHandle handle)
    {
        if (!handle.IsMultiPart)
        {
            return;
        }

        var requestBodyPath = GetRequestBodyPath(handle.RequestName);
        if (File.Exists(requestBodyPath))
        {
            File.Delete(requestBodyPath);
        }
    }

    private static string GetRequestBodyPath(string requestName)
        => Path.Combine(Path.GetTempPath(), $"{requestName.ToBase64FilenameSafe()}.nsrequest");

    private static NSDictionary GetPlatformHeaders(BackgroundHttpRequestMessage request)
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
}
