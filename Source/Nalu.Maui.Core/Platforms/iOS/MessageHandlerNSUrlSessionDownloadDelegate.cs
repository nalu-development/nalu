using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
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
// ReSharper disable once InconsistentNaming
internal partial class MessageHandlerNSUrlSessionDownloadDelegate : NSUrlSessionDownloadDelegate
{
    // ReSharper disable once InconsistentNaming
    private const string SetCookieHeaderKey = "Set-Cookie";
    // ReSharper disable once InconsistentNaming
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
    private volatile Action? _processingInBackgroundCompletionHandler;
    private long _lastCompletedTaskTimestamp = Stopwatch.GetTimestamp();

    private ILogger Logger
    {
        get
        {
            field ??= GetLoggerFromApplicationServiceProvider();
            return field ?? _emptyLogger;
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

                // Disable iOS automatic cookie handling - we manage cookies per-request via CookieContainer
                config.HttpCookieStorage = null;
                config.HttpCookieAcceptPolicy = NSHttpCookieAcceptPolicy.Never;

                // We want, by default, the timeout from HttpClient to have precedence over the one from NSUrlSession
                // Double.MaxValue does not work, so default to 24 hours
                config.TimeoutIntervalForRequest = 24 * 60 * 60;
                config.TimeoutIntervalForResource = 24 * 60 * 60;

                // The delegate callback queue MUST be serial. NSOperationQueue defaults to a
                // concurrent queue, which lets DidFinishDownloading/DidCompleteWithError (and
                // callbacks for different tasks) run simultaneously, racing the shared handler
                // state. A serial queue restores Apple's documented callback ordering guarantees.
                var delegateQueue = new NSOperationQueue { MaxConcurrentOperationCount = 1 };

                _nsUrlSession = NSUrlSession.FromConfiguration(config, this, delegateQueue);
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
        ArgumentNullException.ThrowIfNull(request.RequestUri);
        // Build NSUrl from Uri components to preserve proper encoding of path, query items, user info, etc.
        var requestUrl = BuildNativeUrl(request.RequestUri);
        var requestIdentifier = TryGetRequestIdentifier(request, out var id) ? id : Guid.NewGuid().ToString("N");

        Logger.LogDebug("SendAsync {RequestName} for [{Method}] {Url}", requestIdentifier, request.Method.Method, request.RequestUri);

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
                Logger.LogDebug("MultipartContent or StreamContent for request {RequestName}", requestIdentifier);
            }
            else
            {
                await using var memoryStream = new MemoryStream();
                await content.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
                var body = memoryStream.ToArray();
                nativeHttpRequest.Body = NSData.FromArray(body);
                Logger.LogDebug("BufferedContent for request {RequestName} with size {Size}", requestIdentifier, body.Length);
            }
        }
        else
        {
            Logger.LogDebug("No content for request {RequestName}", requestIdentifier);
        }

        nativeHttpRequest.Headers = GetPlatformHeaders(request, cookieContainer);

        NSUrlSessionTask task;

        if (contentPath != null)
        {
            // Download tasks only: their responses land in DidFinishDownloading, which this
            // delegate handles. Memory-map the body file so large payloads stay off the heap.
            var fileUrl = NSUrl.CreateFileUrl(contentPath, null);
            // NSDataReadingOptions.Mapped == NSDataReadingMappedIfSafe
            nativeHttpRequest.Body = NSData.FromUrl(fileUrl, NSDataReadingOptions.Mapped, out var bodyError)
                                     ?? throw new HttpRequestException($"Failed to read request body file '{contentPath}': {FormatNSError(bodyError)}");
            task = Session.CreateDownloadTask(nativeHttpRequest);
            Logger.LogDebug("Created download task for {RequestName} with content stored in {BodyPath}", requestIdentifier, contentPath);
        }
        else
        {
            task = Session.CreateDownloadTask(nativeHttpRequest);
            Logger.LogDebug("Created download task for {RequestName}", requestIdentifier);
        }

        var requestHandle = new NSUrlRequestHandle(requestIdentifier, cookieContainer, contentPath);
        requestHandle.CancellationTokenRegistration = cancellationToken.Register(() =>
            {
                Logger.LogDebug("Cancellation requested for {RequestName} task", requestIdentifier);

                try
                {
                    task.Cancel();
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Failed to cancel native task {RequestName}", requestIdentifier);
                }
                finally
                {
                    requestHandle.ResponseCompletionSource.TrySetCanceled(cancellationToken);
                    requestHandle.Complete();
                }
            }
        );

        _pendingRequests[requestIdentifier] = requestHandle;

        cancellationToken.ThrowIfCancellationRequested();
        
        task.TaskDescription = requestIdentifier;
        task.Resume();
        
        Logger.LogDebug("Task resumed for request {RequestName}", requestIdentifier);

        await Task.Yield();

        try
        {
            return await requestHandle.ResponseCompletionSource.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The native completion callback may never arrive for a canceled background task,
            // so proactively release the handle and its temporary files to avoid leaking them.
            CompleteAndRemoveHandle(requestHandle);

            throw;
        }
    }

    /// <inheritdoc />
    public override void DidBecomeInvalid(NSUrlSession session, NSError? error)
    {
        try
        {
            Logger.LogDebug("DidBecomeInvalid");
            _nsUrlSession = null;

            if (error != null)
            {
                Logger.LogError(new InvalidOperationException(FormatNSError(error)), "Exception in DidBecomeInvalid");
            }
        }
        catch (Exception)
        {
            // A managed exception escaping an ObjC-registrar callback aborts the process (SIGABRT).
            _nsUrlSession = null;
        }
    }

    /// <inheritdoc />
    public override void DidCompleteWithError(NSUrlSession session, NSUrlSessionTask task, NSError? error)
    {
        Volatile.Write(ref _lastCompletedTaskTimestamp, Stopwatch.GetTimestamp());

        // A managed exception escaping an ObjC-registrar callback aborts the process (SIGABRT),
        // orphaning every queued background-session event — observed in the field on background
        // launches where session callbacks arrive before the DI container is built.
        try
        {
            ProcessTaskCompletion(task, error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Unhandled error in DidCompleteWithError for {TaskDescription}", task.TaskDescription);
            FaultPendingRequest(task.TaskDescription, ex);
        }
    }

    private void ProcessTaskCompletion(NSUrlSessionTask task, NSError? error)
    {
        // Runs synchronously on the delegate queue: unlike DidFinishDownloading there is no
        // temp file at stake and the body is quick — handle bookkeeping plus TrySet*
        // completions, whose continuations run asynchronously by construction
        // (TaskCreationOptions.RunContinuationsAsynchronously) — so deferring buys nothing.
        Logger.LogDebug("DidCompleteWithError {TaskDescription} with {State}", task.TaskDescription, task.State);

        if (string.IsNullOrWhiteSpace(task.TaskDescription))
        {
            Logger.LogError("DidCompleteWithError TaskDescription is null or empty");

            return;
        }

        var requestIdentifier = task.TaskDescription!;

        // A successful completion is finalized by DidFinishDownloading and the subsequent
        // disposal of the response content stream (see AcknowledgingStreamContent). There is
        // nothing to do here, and we must NOT fabricate and re-insert a handle: it would never
        // be completed, leaking in _pendingRequests and stalling background-processing waits.
        if (task is { State: NSUrlSessionTaskState.Completed, Error: null })
        {
            if (!_pendingRequests.ContainsKey(requestIdentifier))
            {
                Logger.LogDebug("Task {RequestIdentifier} completed successfully with no pending handle", requestIdentifier);
            }

            return;
        }

        // Abnormal completion (error/cancel/unexpected state): ensure we have a handle to fault.
        // GetOrAdd: processing runs in parallel, a get-then-set could produce two competing
        // lost handles for callbacks of the same request.
        var handle = _pendingRequests.GetOrAdd(requestIdentifier, static id => new NSUrlRequestHandle(id, null, null, true));

        if (_processingInBackgroundCompletionHandler is not null)
        {
            var added = _processingInBackgroundHandles.TryAdd(requestIdentifier, handle);
            Logger.LogDebug("Tracking request {RequestIdentifier} for background processing: {Added}", requestIdentifier, added);
        }

        switch (task.State)
        {
            case NSUrlSessionTaskState.Running:
                Logger.LogError("Task {RequestIdentifier} completed callback invoked with running state: {Error}", requestIdentifier, FormatNSError(error));
                handle.ResponseCompletionSource.TrySetException(new InvalidOperationException("Task completed callback invoked with running state"));
                CompleteAndRemoveHandle(handle);

                break;
            case NSUrlSessionTaskState.Suspended:
                Logger.LogError("Task {RequestIdentifier} completed callback invoked with suspended state: {Error}", requestIdentifier, FormatNSError(error));
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
                        // NSError.ToString() is just LocalizedDescription (e.g. a bare "unknown error"
                        // for NSURLErrorDomain -1): surface domain, code and the underlying error chain.
                        var msg = FormatNSError(task.Error);
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

    private static string ToSafeUnixFileName(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Guid.NewGuid().ToString("N");
        }

        // Normalize Unicode to decompose accents, etc.
        var normalized = input.Normalize(NormalizationForm.FormD);

        // Remove invalid characters (anything that's not safe for Unix file names)
        // Safe characters: a-zA-Z0-9._-
        var cleaned = InvalidUnixFileNameChars().Replace(normalized, "_");

        // Trim underscores or dots from start and end
        cleaned = cleaned.Trim('_', '.');

        // Ensure it's not empty
        if (string.IsNullOrEmpty(cleaned))
        {
            return Guid.NewGuid().ToString("N");
        }

        return cleaned;
    }

    /// <inheritdoc />
    public override void DidFinishDownloading(NSUrlSession session, NSUrlSessionDownloadTask task, NSUrl location)
    {
        Volatile.Write(ref _lastCompletedTaskTimestamp, Stopwatch.GetTimestamp());

        try
        {
            // SECURE FIRST, PROCESS LATER. The system guarantees the downloaded file only while
            // this callback executes, and the delegate queue is SERIAL: every millisecond spent
            // here delays the callbacks queued behind this one, leaving THEIR temp files exposed
            // to nsurlsessiond cleanup — observed in the field as "Downloaded file was removed by
            // the system" after bursts of simultaneous completions. The callback therefore only
            // renames the file into app-owned storage (microseconds) and defers everything else.
            var stagedFilePath = SecureDownloadedFile(location, out var stagingFailureReason);
            var originalSourcePath = location.Path;

            ProcessCallback(task.TaskDescription, () => ProcessFinishedDownload(task, stagedFilePath, originalSourcePath, stagingFailureReason));
        }
        catch (Exception ex)
        {
            // A managed exception escaping an ObjC-registrar callback aborts the process (SIGABRT).
            Logger.LogError(ex, "Unhandled error in DidFinishDownloading for {TaskDescription}", task.TaskDescription);
            FaultPendingRequest(task.TaskDescription, ex);
        }
    }

    private void ProcessFinishedDownload(NSUrlSessionDownloadTask task, string? stagedFilePath, string? originalSourcePath, string? stagingFailureReason)
    {
        if (string.IsNullOrWhiteSpace(task.TaskDescription))
        {
            Logger.LogError("DidFinishDownloading TaskDescription is null or empty");
            DeleteStagedFile(stagedFilePath);

            return;
        }

        var requestIdentifier = task.TaskDescription!;
        Logger.LogDebug("DidFinishDownloading {RequestIdentifier}", requestIdentifier);

        // GetOrAdd: processing runs in parallel, a get-then-set could produce two competing
        // lost handles for callbacks of the same request.
        var handle = _pendingRequests.GetOrAdd(requestIdentifier, static id => new NSUrlRequestHandle(id, null, null, true));

        if (handle.ResponseCompletionSource.Task.IsCompleted)
        {
            Logger.LogDebug("Response task for {RequestIdentifier} already completed, ignoring", requestIdentifier);
            DeleteStagedFile(stagedFilePath);

            return;
        }

        if (_processingInBackgroundCompletionHandler is not null)
        {
            var added = _processingInBackgroundHandles.TryAdd(requestIdentifier, handle);
            Logger.LogDebug("Tracking request {RequestIdentifier} for background processing: {Added}", requestIdentifier, added);
        }

        if (task.Response is not NSHttpUrlResponse response)
        {
            Logger.LogError("Response is not NSHttpUrlResponse");
            DeleteStagedFile(stagedFilePath);
            handle.ResponseCompletionSource.TrySetException(new HttpRequestException("Response is not NSHttpUrlResponse"));
            CompleteAndRemoveHandle(handle);

            return;
        }

        // https://developer.apple.com/documentation/foundation/urlsessiondownloaddelegate/1411575-urlsession/
        // Because the file is temporary, you must either open the file for reading or move it to a permanent location in your app's sandbox container directory before returning from this delegate method.
        // The file was already secured into app-owned storage by DidFinishDownloading; here it
        // is renamed to the request's deterministic response path and opened for reading.
        handle.ResponseContentFile = Path.Combine(Path.GetTempPath(), ToSafeUnixFileName(requestIdentifier) + ".nsresponse");

        FileStream fileStream;

        var targetResponseContentFilePath = handle.ResponseContentFile;

        try
        {
            // Use NSFileManager to move the file - this handles iOS sandbox and symbolic link quirks
            // that can cause .NET's File.Move to fail (e.g., /.nofollow/ prefix in paths)
            var fileManager = NSFileManager.DefaultManager;
            var destinationUrl = NSUrl.FromFilename(targetResponseContentFilePath);

            if (stagedFilePath is not null)
            {
                // Remove existing file if present (NSFileManager.Move doesn't overwrite)
                if (fileManager.FileExists(targetResponseContentFilePath) && !fileManager.Remove(targetResponseContentFilePath, out var removeError))
                {
                    // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
                    var removeErrorMessage = removeError?.LocalizedDescription ?? "Unknown error";
                    throw new IOException($"NSFileManager.Remove failed: {removeErrorMessage}. Path: {targetResponseContentFilePath}");
                }

                if (!fileManager.Move(NSUrl.FromFilename(stagedFilePath), destinationUrl, out var moveError))
                {
                    // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
                    var errorMessage = moveError?.LocalizedDescription ?? "Unknown error";
                    throw new IOException($"NSFileManager.Move failed: {errorMessage}. Source: {stagedFilePath}, Destination: {targetResponseContentFilePath}");
                }
            }
            else
            {
                // SecureDownloadedFile could not claim the file: surface what NSFileManager
                // actually reported instead of guessing. ENOENT means the system reclaimed it
                // (cache purge, or re-delivery of an event whose file is long gone); EPERM
                // points at data protection on a locked device.
                throw new IOException($"Failed to secure downloaded file: {stagingFailureReason ?? "unknown reason"}. Source: {originalSourcePath}");
            }

            fileStream = new FileStream(targetResponseContentFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to process downloaded file for {RequestIdentifier} at {Url}. Source: {Source}, Destination: {Destination}, DestExists: {DestExists}",
                requestIdentifier,
                (task.CurrentRequest ?? task.OriginalRequest)?.Url.ToString() ?? "Missing URL",
                originalSourcePath,
                targetResponseContentFilePath,
                File.Exists(targetResponseContentFilePath));

            DeleteStagedFile(stagedFilePath);
            handle.ResponseCompletionSource.TrySetException(new HttpRequestException("Failed to process downloaded file", ex));
            CompleteAndRemoveHandle(handle);

            return;
        }

        var httpResponseMessage = new HttpResponseMessage(task.GetHttpStatusCode())
                                  {
                                      RequestMessage = CreateHttpRequestMessage(task.CurrentRequest ?? task.OriginalRequest)
                                  };
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
        Volatile.Write(ref _lastCompletedTaskTimestamp, Stopwatch.GetTimestamp());
        WaitEventsProcessingAndNotify();

        return true;
    }
#pragma warning restore IDE0060, VSTHRD110

    internal void CompleteAndRemoveHandle(NSUrlRequestHandle handle)
    {
        handle.Complete();
        _pendingRequests.TryRemove(handle.Identifier, out _);
    }

    /// <summary>
    /// Faults the pending request for <paramref name="requestIdentifier" />, if any, so a
    /// contained callback failure surfaces to the awaiting caller instead of leaking the handle.
    /// </summary>
    private void FaultPendingRequest(string? requestIdentifier, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(requestIdentifier) || !_pendingRequests.TryGetValue(requestIdentifier, out var handle))
        {
            return;
        }

        handle.ResponseCompletionSource.TrySetException(new HttpRequestException("Failed to process session callback", exception));
        CompleteAndRemoveHandle(handle);
    }

    /// <summary>
    /// Runs download processing OFF the serial delegate queue, fire-and-forget. Keeping
    /// <see cref="DidFinishDownloading" /> near-instant is what lets burst completions all
    /// secure their temp files before the system reclaims them. Once a download is secured,
    /// tasks are fully independent — state lives on each request's own handle, the shared maps
    /// are concurrent, and completion races are settled by TrySet* — so processing runs in
    /// parallel with no ordering requirements.
    /// </summary>
    private void ProcessCallback(string? requestIdentifier, Action action)
        => _ = Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Unhandled error while processing a session callback for {RequestIdentifier}", requestIdentifier);
                    FaultPendingRequest(requestIdentifier, ex);
                }
                finally
                {
                    Volatile.Write(ref _lastCompletedTaskTimestamp, Stopwatch.GetTimestamp());
                }
            });

    /// <summary>
    /// Renames the system-provided download into app-owned storage. MUST be the first thing
    /// <see cref="DidFinishDownloading" /> does, synchronously: the file is guaranteed only
    /// while that callback executes, and a bare rename is what keeps the serial delegate queue
    /// draining fast enough during completion bursts. Returns null when the move failed,
    /// with <paramref name="failureReason" /> carrying the actual NSFileManager error.
    /// </summary>
    private string? SecureDownloadedFile(NSUrl location, out string? failureReason)
    {
        if (string.IsNullOrEmpty(location.Path))
        {
            failureReason = "the system provided no file path";

            return null;
        }

        var stagedFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.nsdownload");
        var stagedUrl = NSUrl.FromFilename(stagedFilePath);
        var fileManager = NSFileManager.DefaultManager;

        if (fileManager.Move(location, stagedUrl, out var moveError))
        {
            failureReason = null;

            return stagedFilePath;
        }

        // One retry after ensuring the temp directory exists (iOS may purge it wholesale).
        var tempDir = Path.GetTempPath();
        var isDirectory = false;

        if ((!fileManager.FileExists(tempDir, ref isDirectory) || !isDirectory)
            && fileManager.CreateDirectory(tempDir, true, (NSDictionary?)null, out _)
            && fileManager.Move(location, stagedUrl, out moveError))
        {
            failureReason = null;

            return stagedFilePath;
        }

        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        failureReason = $"{FormatNSError(moveError)} (source file exists: {fileManager.FileExists(location.Path!)})";
        Logger.LogError("Failed to stage downloaded file {Source}: {Reason}", location.Path, failureReason);

        return null;
    }

    private void DeleteStagedFile(string? stagedFilePath)
    {
        try
        {
            if (stagedFilePath is not null && File.Exists(stagedFilePath))
            {
                File.Delete(stagedFilePath);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to delete staged download file {Path}", stagedFilePath);
        }
    }

    // ReSharper disable once AsyncVoidMethod
    private async void WaitEventsProcessingAndNotify()
    {
        if (_processingInBackgroundCompletionHandler is not { } completionHandler)
        {
            return;
        }

        try
        {
            var maxWaitTime = 6500;

            while (Stopwatch.GetElapsedTime(Volatile.Read(ref _lastCompletedTaskTimestamp)) < _eventProcessingWaitThreshold)
            {
                await Task.Delay(200).ConfigureAwait(false);
                maxWaitTime -= 200;
            }

            var acknowledgeTasks = _processingInBackgroundHandles.Values.Select(h => h.CompletedTask).ToList();
            await Task.WhenAny(Task.WhenAll(acknowledgeTasks), Task.Delay(Math.Max(500, maxWaitTime))).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while waiting for background events processing");
        }
        finally
        {
            _processingInBackgroundHandles.Clear();
            _processingInBackgroundCompletionHandler = null;
            Logger.LogDebug("WaitEventsProcessingAndNotify Completed");

            // The system-provided completion handler MUST always be invoked, otherwise iOS may
            // throttle or stop delivering background URL session events to the app.
            DispatchQueue.MainQueue.DispatchAsync(completionHandler);
        }
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
        List<string>? setCookieValues = null;

        foreach (var header in response.AllHeaderFields)
        {
            if (header.Value is null || header.Key is null)
            {
                continue;
            }

            var key = header.Key.ToString();
            var value = header.Value.ToString();

            // Collect Set-Cookie headers for cookie container processing
            if (string.Equals(key, SetCookieHeaderKey, StringComparison.OrdinalIgnoreCase))
            {
                if (cookieContainer is not null)
                {
                    setCookieValues ??= [];
                    setCookieValues.Add(value);
                }

                httpResponseMessage.Headers.TryAddWithoutValidation(SetCookieHeaderKey, value);

                continue;
            }

            var added =
                httpResponseMessage.Headers.TryAddWithoutValidation(key, value) ||
                httpResponseMessage.Content.Headers.TryAddWithoutValidation(key, value);

            if (!added)
            {
                Logger.LogWarning("Failed to add response header {HeaderKey}: {HeaderValue}", key, value);
            }
        }

        // Update managed cookie container from Set-Cookie headers
        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        if (setCookieValues is { Count: > 0 } && cookieContainer is not null && response.Url?.AbsoluteString is { } absoluteUrl)
        {
            var absoluteUri = new Uri(absoluteUrl);

            lock (cookieContainer)
            {
                // CookieContainer.SetCookies expects comma-delimited Set-Cookie values
                cookieContainer.SetCookies(absoluteUri, string.Join(',', setCookieValues));
            }
        }
    }

    private static INSUrlBackgroundSessionLostMessageHandler? GetLostMessageHandler()
    {
        try
        {
            // IPlatformApplication.Current is assigned in the app-delegate constructor, but
            // Services only after CreateMauiApp() returns: background launches deliver session
            // events inside exactly that window, so Services can be null here.
            return IPlatformApplication.Current?.Services?.GetService<INSUrlBackgroundSessionLostMessageHandler>();
        }
        catch (Exception)
        {
            // e.g. ObjectDisposedException while the container is being torn down.
            return null;
        }
    }

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

        // Set header cookies if needed from the managed cookie container
        if (cookieContainer is not null)
        {
            lock (cookieContainer)
            {
                // As per docs: An HTTP cookie header, with strings representing Cookie instances delimited by semicolons.
                var cookies = cookieContainer.GetCookieHeader(request.RequestUri!);

                if (!string.IsNullOrEmpty(cookies))
                {
                    nativeHeaders[CookieHeaderKey] = new NSString(cookies);
                }
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

    private static readonly NSString _failingUrlErrorKey = new("NSErrorFailingURLStringKey");

    /// <summary>
    /// Formats an <see cref="NSError"/> with its domain, code, failing URL and underlying error
    /// chain: <c>NSError.ToString()</c> only returns <c>LocalizedDescription</c>, which for
    /// e.g. <c>NSURLErrorDomain</c> code -1 is a useless bare "unknown error".
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private static string FormatNSError(NSError? error)
    {
        if (error is null)
        {
            return "unknown error";
        }

        var builder = new StringBuilder();
        AppendNSError(builder, error, 0);

        return builder.ToString();
    }

    // ReSharper disable once InconsistentNaming
    private static void AppendNSError(StringBuilder builder, NSError error, int depth)
    {
        builder.Append(error.LocalizedDescription)
               .Append(" [")
               .Append(error.Domain)
               .Append(' ')
               .Append(error.Code)
               .Append(']');

        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        if (error.UserInfo?[_failingUrlErrorKey] is { } failingUrl)
        {
            builder.Append(" Url=").Append(failingUrl);
        }

        if (depth < 3 && error.UserInfo?[NSError.UnderlyingErrorKey] is NSError underlyingError)
        {
            builder.Append(" <- ");
            AppendNSError(builder, underlyingError, depth + 1);
        }
    }

    private static ILogger<MessageHandlerNSUrlSessionDownloadDelegate> CreateEmptyLogger()
        => LoggerFactory.Create(_ => { }).CreateLogger<MessageHandlerNSUrlSessionDownloadDelegate>();

    private static ILogger<MessageHandlerNSUrlSessionDownloadDelegate>? GetLoggerFromApplicationServiceProvider()
    {
        try
        {
            // IPlatformApplication.Current is assigned in the app-delegate constructor, but
            // Services only after CreateMauiApp() returns: on a background launch session
            // callbacks can arrive inside that window (observed in the field as a SIGABRT
            // from an ArgumentNullException crossing the ObjC-registrar boundary).
            return IPlatformApplication.Current?.Services?.GetService<ILogger<MessageHandlerNSUrlSessionDownloadDelegate>>();
        }
        catch (Exception)
        {
            // e.g. ObjectDisposedException while the container is being torn down.
            return null;
        }
    }

    // Build an NSUrl from a managed Uri using NSUrlComponents and HttpUtility.ParseQueryString for accurate query parsing.
    private static NSUrl BuildNativeUrl(Uri uri)
    {
        var components = new NSUrlComponents
        {
            Scheme = uri.Scheme,
            Host = uri.Host,
            Path = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath
        };

        if (uri is { IsDefaultPort: false, Port: > 0 })
        {
            components.Port = NSNumber.FromInt32(uri.Port);
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            // Uri.UserInfo is already decoded by the Uri class, so use it directly
            var userInfoParts = uri.UserInfo.Split(':');
            if (userInfoParts.Length > 0)
            {
                components.User = userInfoParts[0];
            }
            if (userInfoParts.Length > 1)
            {
                components.Password = userInfoParts[1];
            }
        }

        if (!string.IsNullOrEmpty(uri.Query))
        {
            // Use PercentEncodedQuery to preserve the exact encoding from the original URI.
            // This avoids decoding/re-encoding which can change semantics (e.g., + vs %20 for spaces).
            components.PercentEncodedQuery = uri.Query.TrimStart('?');
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            components.Fragment = uri.Fragment.TrimStart('#');
        }

        return components.Url ?? throw new InvalidOperationException("Failed to build valid NSUrl from Uri components.");
    }

    [GeneratedRegex("(.+?): (.+)")]
    private static partial Regex HeaderValueRegex();

    [GeneratedRegex(@"[^a-zA-Z0-9_\.\-]+")]
    private static partial Regex InvalidUnixFileNameChars();
}
