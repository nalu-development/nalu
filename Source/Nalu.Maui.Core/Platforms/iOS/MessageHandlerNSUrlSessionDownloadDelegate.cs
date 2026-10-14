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
    // Lazy: a racy `??=` could construct twice — rerunning the orphan sweep against a live
    // session's files and creating a second session with the same background identifier.
    public static MessageHandlerNSUrlSessionDownloadDelegate Current => _instance.Value;

    public static string SessionIdentifier { get; } = $"{NSBundle.MainBundle.BundleIdentifier}.NSUrlBackgroundSessionHttpMessageHandler";

    // Background wake bookkeeping (see WaitEventsProcessingAndNotify). iOS grants a woken app
    // roughly 30 s; the whole wait stays well inside that.
    private static readonly TimeSpan _backgroundEventsDeliveryTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _backgroundQuietFallback = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _backgroundProcessingBudget = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan _backgroundQuietFloor = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan _backgroundEarlySignalFreshness = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _backgroundAcknowledgeLookBack = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _infiniteTimeout = Timeout.InfiniteTimeSpan;
    private static readonly Lazy<MessageHandlerNSUrlSessionDownloadDelegate> _instance = new(() => new MessageHandlerNSUrlSessionDownloadDelegate());

    /// <summary>
    /// Test seam (TestApp lifecycle harness): invoked on the delegate queue right after
    /// <see cref="DidFinishDownloading" /> secured the system file, BEFORE the callback returns —
    /// lets a test kill the process at the exact point where the file is already moved but the
    /// event is not yet acknowledged to nsurlsessiond.
    /// </summary>
    internal static Action<NSUrlSessionDownloadTask, string?>? DownloadSecuredForTesting;
    private readonly ILogger _emptyLogger = CreateEmptyLogger();
    private NSUrlSession? _nsUrlSession;
    // Background wakes (see HandleEventsForBackgroundUrl / WaitEventsProcessingAndNotify), all
    // guarded by _backgroundEventsLock.
    private readonly List<(Action Handler, long Generation)> _pendingCompletionHandlers = [];
    private long _wakeGeneration;
    private bool _backgroundWaitRunning;
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

    // Responses delivered to a consumer (awaiter or lost-message handler) and not yet acknowledged
    // (content disposed), with their delivery time: a background wake lets them finish before
    // letting iOS suspend the app — including deliveries processed BEFORE the wake was announced.
    private readonly ConcurrentDictionary<NSUrlRequestHandle, long> _unacknowledgedHandles = new();

    // Tasks whose download callback reached THIS process: a successful completion of a task not
    // in here means its download was handed to a previous process that died with it.
    private readonly ConcurrentDictionary<nuint, byte> _receivedDownloadTasks = new();

    private readonly Lock _backgroundEventsLock = new();
    private TaskCompletionSource? _backgroundEventsDelivered;
    private long _backgroundEventsDeliveredEarlyAt;
    private int _inFlightProcessing;

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
        // BEFORE the session exists: no callback can be creating files concurrently, so every
        // matching file is stale by construction.
        CleanupOrphanedFiles();
        _ = Session;
    }

    /// <summary>
    /// Deletes request/response temp files a PREVIOUS process left behind — e.g. an app killed
    /// between a response being delivered and its content being consumed (the handoff is not
    /// transactional: that response is gone for good — nsurlsessiond never re-delivers events it
    /// already delivered — and its <c>.nsresponse</c> file would otherwise sit in tmp forever).
    /// Same for <c>.nsrequest</c> bodies (nsurlsessiond owns its own copy of the body once the
    /// task is created) and <c>.nsdownload</c> staging leftovers.
    /// </summary>
    internal void CleanupOrphanedFiles()
    {
        try
        {
            var tempPath = Path.GetTempPath();
            var deleted = 0;

            foreach (var pattern in (string[]) ["*.nsrequest", "*.nsresponse", "*.nsdownload"])
            {
                foreach (var file in Directory.EnumerateFiles(tempPath, pattern))
                {
                    try
                    {
                        File.Delete(file);
                        deleted++;
                    }
                    catch (Exception)
                    {
                        // Never let cleanup interfere with startup.
                    }
                }
            }

            if (deleted > 0)
            {
                Logger.LogDebug("Deleted {Count} orphaned background-session temp files", deleted);
            }
        }
        catch (Exception)
        {
            // e.g. the temp directory itself is missing: nothing to clean.
        }
    }

    public IReadOnlyDictionary<string, Task<HttpResponseMessage>> GetPendingResponses()
        => _pendingRequests.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ResponseCompletionSource.Task);

    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CookieContainer? cookieContainer, TimeSpan defaultTimeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request.RequestUri);
        // Build NSUrl from Uri components to preserve proper encoding of path, query items, user info, etc.
        var requestUrl = BuildNativeUrl(request.RequestUri);
        var requestIdentifier = TryGetRequestIdentifier(request, out var id) ? id : Guid.NewGuid().ToString("N");

        // iOS IGNORES the per-request timeout on BACKGROUND sessions (verified on-device with
        // the chaos harness), so DefaultTimeout is enforced MANAGED-side: an ad-hoc token linked
        // to the caller's cancels the transfer when the timeout elapses, surfaced with the
        // HttpClient.Timeout convention (TaskCanceledException wrapping a TimeoutException) so
        // callers can tell a timeout from their own cancellation.
        using var timeoutCts = defaultTimeout == _infiniteTimeout ? null : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts?.CancelAfter(defaultTimeout);
        var effectiveToken = timeoutCts?.Token ?? cancellationToken;

        [DoesNotReturn]
        void ThrowCancellation(OperationCanceledException original)
        {
            if (timeoutCts is { IsCancellationRequested: true } && !cancellationToken.IsCancellationRequested)
            {
                var message = FormattableString.Invariant(
                    $"The request timed out after {defaultTimeout.TotalSeconds:0.###} seconds (NSUrlBackgroundSessionHttpMessageHandler.DefaultTimeout)."
                );

                throw new TaskCanceledException(message, new TimeoutException(message, original));
            }

            throw original;
        }

        // A canceled or timed-out attached wait WALKS AWAY and nothing more: the transfer is
        // still healthy and owned by the ORIGINAL awaiter (an attach always coexists with one:
        // only in-flight transfers of THIS process are attachable, never lost deliveries).
        // Releasing the handle here would delete the shared files and orphan that original
        // awaiter forever, while ITS cancellation path handles the teardown.
        async Task<HttpResponseMessage> AttachAsync(NSUrlRequestHandle handle)
        {
            try
            {
                return await handle.ResponseCompletionSource.Task.WaitAsync(effectiveToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                ThrowCancellation(ex);

                throw; // Unreachable: ThrowCancellation always throws (reachability analysis ignores [DoesNotReturn]).
            }
        }

        // One transfer per identifier: a second send while one is in flight ATTACHES to it and
        // returns its response, exactly as if it awaited the original call — it must not start
        // a competing transfer (which would also overwrite the pending handle, orphaning the
        // first awaiter, and the shared on-disk body file). WaitAsync scopes the duplicate's
        // token to ITS wait only: cancelling an attached awaiter never cancels the transfer.
        // Checked before any side effect; the TryAdd below closes the remaining race window.
        if (_pendingRequests.TryGetValue(requestIdentifier, out var inFlightHandle) && inFlightHandle.IsAttachable)
        {
            Logger.LogDebug("SendAsync {RequestName} attached to the already in-flight request", requestIdentifier);

            return await AttachAsync(inFlightHandle).ConfigureAwait(false);
        }

        Logger.LogDebug("SendAsync {RequestName} for [{Method}] {Url}", requestIdentifier, request.Method.Method, request.RequestUri);

        var nativeHttpRequest = new NSMutableUrlRequest(requestUrl)
                                {
                                    HttpMethod = request.Method.Method
                                };

        if (defaultTimeout != _infiniteTimeout)
        {
            // Kept for completeness (and in case a future iOS honors it), but INERT on
            // background sessions — the linked timeoutCts above is what actually enforces it.
            nativeHttpRequest.TimeoutInterval = defaultTimeout.TotalSeconds;
        }

        string? contentPath = null;

        if (request.Content is { } content)
        {
            if (content is MultipartContent or StreamContent)
            {
                contentPath = GetRequestBodyPath(requestIdentifier);
                await using var fileStream = File.Create(contentPath);
                await content.CopyToAsync(fileStream, effectiveToken).ConfigureAwait(false);
                Logger.LogDebug("MultipartContent or StreamContent for request {RequestName}", requestIdentifier);
            }
            else
            {
                await using var memoryStream = new MemoryStream();
                await content.CopyToAsync(memoryStream, effectiveToken).ConfigureAwait(false);
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

        // Stamped at creation, before anything can cancel the task, so that EVERY callback of it —
        // including the -999 of a task withdrawn below — carries its request identifier.
        task.TaskDescription = requestIdentifier;
        var requestHandle = new NSUrlRequestHandle(requestIdentifier, cookieContainer, contentPath) { TaskIdentifier = task.TaskIdentifier };
        requestHandle.CancellationTokenRegistration = effectiveToken.Register(() =>
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
                    requestHandle.ResponseCompletionSource.TrySetCanceled(effectiveToken);
                    requestHandle.Complete();
                }
            }
        );

        while (!_pendingRequests.TryAdd(requestIdentifier, requestHandle))
        {
            if (!_pendingRequests.TryGetValue(requestIdentifier, out var occupant))
            {
                // The occupant completed and vanished between TryAdd and TryGetValue: the slot is free.
                continue;
            }

            if (occupant.IsAttachable)
            {
                // Lost a race with a concurrent same-identifier request past the early check:
                // withdraw our native task (its body file is our own) and attach to the winner instead.
                requestHandle.CancellationTokenRegistration?.Dispose();
                requestHandle.CancellationTokenRegistration = null;
                task.Cancel();
                requestHandle.Complete();
                Logger.LogDebug("SendAsync {RequestName} attached to a concurrently started request", requestIdentifier);

                return await AttachAsync(occupant).ConfigureAwait(false);
            }

            // The occupant is no longer in flight (a delivered but not yet disposed response, or a
            // lost delivery awaiting the lost-message handler): this send starts a FRESH transfer and
            // takes the slot. Every handle owns its files, and removal is key+value, so the
            // occupant's eventual teardown can no longer evict this request.
            if (_pendingRequests.TryUpdate(requestIdentifier, requestHandle, occupant))
            {
                break;
            }
        }

        // A token canceled before (or while) registering already completed the handle — the
        // registration callback ran synchronously: never resume a task nobody awaits. The await
        // below observes the cancellation and releases the handle.
        if (!requestHandle.ResponseCompletionSource.Task.IsCompleted)
        {
            task.Resume();
            Logger.LogDebug("Task resumed for request {RequestName}", requestIdentifier);
        }

        await Task.Yield();

        try
        {
            return await requestHandle.ResponseCompletionSource.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException ex)
        {
            // The native completion callback may never arrive for a canceled background task,
            // so proactively release the handle and its temporary files to avoid leaking them.
            CompleteAndRemoveHandle(requestHandle);

            ThrowCancellation(ex);

            throw; // Unreachable: ThrowCancellation always throws (reachability analysis ignores [DoesNotReturn]).
        }
    }

    /// <inheritdoc />
    public override void DidBecomeInvalid(NSUrlSession session, NSError? error)
    {
        try
        {
            Logger.LogDebug("DidBecomeInvalid");
            _nsUrlSession = null;

            // DidBecomeInvalid is the session's LAST message: task identifiers are only unique per
            // session, so the recreated session's tasks must not match the dead one's.
            _receivedDownloadTasks.Clear();

            if (error != null)
            {
                Logger.LogError(new InvalidOperationException(FormatNSError(error)), "Exception in DidBecomeInvalid");
            }
        }
        catch (Exception)
        {
            // A managed exception escaping an ObjC-registrar callback aborts the process (SIGABRT).
            _nsUrlSession = null;
            _receivedDownloadTasks.Clear();
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
            Logger.LogError(ex, "Unhandled error in DidCompleteWithError for {TaskDescription}", SafeTaskDescription(task));
            FaultOwningRequest(task, ex);
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
            // Every task this delegate creates is described at creation: nothing to complete.
            Logger.LogWarning("DidCompleteWithError for a task without TaskDescription");

            return;
        }

        var requestIdentifier = task.TaskDescription!;
        var taskIdentifier = task.TaskIdentifier;
        var handle = FindOwningHandle(requestIdentifier, taskIdentifier);

        if (task is { State: NSUrlSessionTaskState.Completed, Error: null })
        {
            // A successful download task delivers its file BEFORE completing, on this same serial
            // queue. Completing without it means the file event went to a PREVIOUS process, which
            // acknowledged it on arrival and died before processing it (verified on device): that
            // response is gone for good. Otherwise the success is finalized by DidFinishDownloading
            // and the disposal of the response content (see AcknowledgingStreamContent).
            if (_receivedDownloadTasks.TryRemove(taskIdentifier, out _))
            {
                return;
            }

            if (handle is { IsLostRequest: false })
            {
                FailDownload(
                    handle,
                    CreateDownloadLostException("the task completed without delivering its downloaded file"),
                    task,
                    null,
                    null
                );
            }
            else
            {
                Logger.LogWarning(
                    "Background download {RequestIdentifier} (task {TaskIdentifier}) completed, but its file was handed to a previous process that did not survive to process it: the response is lost",
                    requestIdentifier,
                    taskIdentifier
                );
            }

            return;
        }

        if (handle is null)
        {
            // Not a request this process awaits: a task that completed for a previous process, one
            // superseded by a re-send, or one this process withdrew or canceled. Failed requests are
            // never reported to the lost-message handler, so there is nobody to fail.
            Logger.LogDebug(
                "Task {RequestIdentifier} (task {TaskIdentifier}) that no request is awaiting completed with {State}: {Error}",
                requestIdentifier,
                taskIdentifier,
                task.State,
                FormatNSError(task.Error ?? error)
            );

            return;
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
                        handle.ResponseCompletionSource.TrySetException(new HttpRequestException(MapHttpRequestError(task.Error), msg));
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
            // this callback executes, so the callback only renames it into app-owned storage
            // (microseconds) and defers everything else, keeping the serial delegate queue moving.
            var stagedFilePath = SecureDownloadedFile(location, out var stagingFailureReason);
            var originalSourcePath = location.Path;
            DownloadSecuredForTesting?.Invoke(task, stagedFilePath);

            // Recorded on the serial queue BEFORE deferring: the completion callback queued right
            // behind this one must know the download reached this process (see ProcessTaskCompletion).
            _receivedDownloadTasks[task.TaskIdentifier] = 0;

            ProcessCallback(task, () => ProcessFinishedDownload(task, stagedFilePath, originalSourcePath, stagingFailureReason), stagedFilePath);
        }
        catch (Exception ex)
        {
            // A managed exception escaping an ObjC-registrar callback aborts the process (SIGABRT).
            Logger.LogError(ex, "Unhandled error in DidFinishDownloading for {TaskDescription}", SafeTaskDescription(task));
            FaultOwningRequest(task, ex);
        }
    }

    private void ProcessFinishedDownload(NSUrlSessionDownloadTask task, string? stagedFilePath, string? originalSourcePath, string? stagingFailureReason)
    {
        if (string.IsNullOrWhiteSpace(task.TaskDescription))
        {
            Logger.LogWarning("DidFinishDownloading for a task without TaskDescription");
            DeleteStagedFile(stagedFilePath);

            return;
        }

        var requestIdentifier = task.TaskDescription!;
        var taskIdentifier = task.TaskIdentifier;
        Logger.LogDebug("DidFinishDownloading {RequestIdentifier} (task {TaskIdentifier})", requestIdentifier, taskIdentifier);

        if (FindOwningHandle(requestIdentifier, taskIdentifier) is not { } handle)
        {
            // No request of THIS process awaits this task: a download that completed for a previous
            // process (or was superseded by a re-send). It becomes a LOST delivery, published under
            // its identifier for GetPendingResponses — unless a newer request holds that identifier,
            // which must stay untouched: then the lost delivery is handed over unpublished.
            // GetOrAdd also dedupes a duplicate callback of the same task processed in parallel.
            var lost = new NSUrlRequestHandle(requestIdentifier, null, null, true) { TaskIdentifier = taskIdentifier };
            handle = _pendingRequests.GetOrAdd(requestIdentifier, lost);

            if (handle.TaskIdentifier != taskIdentifier)
            {
                handle = lost;
            }
        }

        if (handle.ResponseCompletionSource.Task.IsCompleted)
        {
            Logger.LogDebug("Response task for {RequestIdentifier} already completed, ignoring", requestIdentifier);
            DeleteStagedFile(stagedFilePath);

            return;
        }

        // Everything from here on must settle the handle, whatever throws: a published lost handle
        // left pending would sit in GetPendingResponses forever.
        FileStream? fileStream = null;
        HttpResponseMessage? httpResponseMessage = null;
        var targetResponseContentFilePath = Path.Combine(Path.GetTempPath(), $"{ToSafeUnixFileName(requestIdentifier)}-{Guid.NewGuid():N}.nsresponse");

        try
        {
            if (task.Response is not NSHttpUrlResponse response)
            {
                DeleteStagedFile(stagedFilePath);
                FailDownload(handle, new HttpRequestException(HttpRequestError.InvalidResponse, "Response is not NSHttpUrlResponse"), task, originalSourcePath, null);

                return;
            }

            if (stagedFilePath is null)
            {
                // SecureDownloadedFile could not claim the file: surface what NSFileManager
                // actually reported. ENOENT means the file was deleted before delivery — most
                // likely Library/Caches cleared by the app or purged by iOS (see SecureDownloadedFile).
                FailDownload(
                    handle,
                    CreateDownloadLostException($"its file could not be secured: {stagingFailureReason ?? "unknown reason"}. Source: {originalSourcePath}"),
                    task,
                    originalSourcePath,
                    null
                );

                return;
            }

            // https://developer.apple.com/documentation/foundation/urlsessiondownloaddelegate/1411575-urlsession/
            // Because the file is temporary, you must either open the file for reading or move it to a permanent location in your app's sandbox container directory before returning from this delegate method.
            // The file was already secured into app-owned storage by DidFinishDownloading; here it
            // is renamed to the transfer's own response path (unique: two transfers may share an
            // identifier) and opened for reading. NSFileManager handles iOS sandbox and symbolic
            // link quirks that can make .NET's File.Move fail (e.g. the /.nofollow/ path prefix).
            handle.ResponseContentFile = targetResponseContentFilePath;

            if (!NSFileManager.DefaultManager.Move(NSUrl.FromFilename(stagedFilePath), NSUrl.FromFilename(targetResponseContentFilePath), out var moveError))
            {
                throw new IOException($"NSFileManager.Move failed: {FormatNSError(moveError)}. Source: {stagedFilePath}, Destination: {targetResponseContentFilePath}");
            }

            fileStream = new FileStream(targetResponseContentFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            httpResponseMessage = new HttpResponseMessage(task.GetHttpStatusCode())
                                  {
                                      RequestMessage = CreateHttpRequestMessage(task.CurrentRequest ?? task.OriginalRequest)
                                  };
            httpResponseMessage.Content = new AcknowledgingStreamContent(this, handle, fileStream);
            ApplyResponseHeaders(httpResponseMessage, response, handle.CookieContainer);

            // Tracked BEFORE publishing the result, so a background wake can never miss it.
            _unacknowledgedHandles[handle] = Stopwatch.GetTimestamp();

            if (!handle.ResponseCompletionSource.TrySetResult(httpResponseMessage))
            {
                // Lost a race (a cancellation, a timeout, a parallel duplicate settled it first):
                // nobody will read this response. Disposing only the stream — the content's own
                // disposal would tear down a handle that already belongs to its winner.
                _unacknowledgedHandles.TryRemove(handle, out _);
                fileStream.Dispose();
                TryDeleteFile(targetResponseContentFilePath);

                return;
            }
        }
        catch (Exception ex)
        {
            if (httpResponseMessage is null)
            {
                fileStream?.Dispose();
            }

            DeleteStagedFile(stagedFilePath);
            FailDownload(handle, ex as HttpRequestException ?? new HttpRequestException("Failed to process downloaded file", ex), task, originalSourcePath, targetResponseContentFilePath);

            return;
        }

        Logger.LogDebug("DidFinishDownloading set response for {RequestIdentifier}", requestIdentifier);
        var deliveredResponse = httpResponseMessage;

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
                // Nobody will ever read it: disposing the response releases the file stream and,
                // through AcknowledgingStreamContent, the handle and its file.
                deliveredResponse.Dispose();
            }
        }
    }

    private void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to delete {Path}", path);
        }
    }

    /// <summary>
    /// Fails a download outcome. A request of this process is faulted for its awaiter; a LOST
    /// delivery has nobody to fail — failures are never reported to the lost-message handler —
    /// so it is logged and its (observed) fault never surfaces as an unobserved task exception.
    /// </summary>
    private void FailDownload(NSUrlRequestHandle handle, Exception exception, NSUrlSessionTask task, string? sourcePath, string? destinationPath)
    {
        var url = SafeTaskUrl(task);

        if (handle.IsLostRequest)
        {
            Logger.LogWarning(
                exception,
                "Background download {RequestIdentifier} (task {TaskIdentifier}) at {Url}, which no request of this process was awaiting, could not be delivered and is dropped. Source: {Source}",
                handle.Identifier,
                handle.TaskIdentifier,
                url,
                sourcePath
            );
        }
        else
        {
            Logger.LogWarning(
                exception,
                "Background download {RequestIdentifier} (task {TaskIdentifier}) at {Url} failed. Source: {Source}, Destination: {Destination}",
                handle.Identifier,
                handle.TaskIdentifier,
                url,
                sourcePath,
                destinationPath
            );
        }

        handle.ResponseCompletionSource.TrySetException(exception);

        if (handle.IsLostRequest)
        {
            _ = handle.ResponseCompletionSource.Task.Exception;
        }

        CompleteAndRemoveHandle(handle);
    }

    /// <summary>
    /// The error of a download that completed on the network but whose response can no longer be
    /// read. <see cref="HttpRequestError.ResponseEnded" /> lets callers recognize it and retry.
    /// </summary>
    private static HttpRequestException CreateDownloadLostException(string reason)
        => new(
            HttpRequestError.ResponseEnded,
            $"The background download completed but its response is lost: {reason}. A downloaded file disappears before delivery when Library/Caches is cleared by the app or purged by iOS on low storage; retry the request."
        );

    private static string? SafeTaskDescription(NSUrlSessionTask task)
    {
        try
        {
            return task.TaskDescription;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string SafeTaskUrl(NSUrlSessionTask task)
    {
        try
        {
            return (task.CurrentRequest ?? task.OriginalRequest)?.Url.ToString() ?? "Missing URL";
        }
        catch (Exception)
        {
            return "Missing URL";
        }
    }

#pragma warning disable IDE0060, VSTHRD110
    public bool HandleEventsForBackgroundUrl(UIApplication application, string sessionIdentifier, Action completionHandler)
    {
        // An invalidated session (DidBecomeInvalid) is recreated here: without a session the
        // pending events would never be delivered and the completion handler never invoked.
        _ = Session;

        if (!ReferenceEquals(this, _nsUrlSession?.Delegate))
        {
            return false;
        }

        Logger.LogDebug("HandleEventsForBackgroundUrl");
        Volatile.Write(ref _lastCompletedTaskTimestamp, Stopwatch.GetTimestamp());

        bool startWait;

        lock (_backgroundEventsLock)
        {
            // Each wake waits for ITS OWN events: a fresh delivery signal unless the current one
            // is still pending (then both wakes share it).
            if (_backgroundEventsDelivered is not { Task.IsCompleted: false })
            {
                var delivered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                // The session (already alive) may have finished delivering just BEFORE this call:
                // honored only while fresh, so an old unpaired signal can never pre-complete a wake.
                if (_backgroundEventsDeliveredEarlyAt != 0 && Stopwatch.GetElapsedTime(_backgroundEventsDeliveredEarlyAt) < _backgroundEarlySignalFreshness)
                {
                    delivered.TrySetResult();
                }

                _backgroundEventsDelivered = delivered;
            }

            _backgroundEventsDeliveredEarlyAt = 0;
            _pendingCompletionHandlers.Add((completionHandler, ++_wakeGeneration));
            startWait = !_backgroundWaitRunning;
            _backgroundWaitRunning = true;
        }

        // A wake arriving while a wait is running joins it: the running wait serves it in its
        // next round (see WaitEventsProcessingAndNotify).
        if (startWait)
        {
            WaitEventsProcessingAndNotify();
        }

        return true;
    }
#pragma warning restore IDE0060, VSTHRD110

    /// <inheritdoc />
    /// <remarks>
    /// Apple's signal that every event enqueued for the background wake has been delivered: only
    /// from then on is it safe to invoke the system completion handler (which lets iOS suspend the app).
    /// </remarks>
    public override void DidFinishEventsForBackgroundSession(NSUrlSession session)
    {
        try
        {
            Logger.LogDebug("DidFinishEventsForBackgroundSession");

            lock (_backgroundEventsLock)
            {
                if (_backgroundEventsDelivered is { Task.IsCompleted: false } delivered)
                {
                    delivered.TrySetResult();
                }
                else
                {
                    _backgroundEventsDeliveredEarlyAt = Stopwatch.GetTimestamp();
                }
            }
        }
        catch (Exception)
        {
            // A managed exception escaping an ObjC-registrar callback aborts the process (SIGABRT).
        }
    }

    internal void CompleteAndRemoveHandle(NSUrlRequestHandle handle)
    {
        handle.Complete();
        _unacknowledgedHandles.TryRemove(handle, out _);

        // Key+VALUE: a late teardown of an older handle must never evict a newer request that
        // reuses the identifier (its awaiter would then hang forever).
        _pendingRequests.TryRemove(new KeyValuePair<string, NSUrlRequestHandle>(handle.Identifier, handle));
    }

    /// <summary>
    /// The handle a callback of task <paramref name="taskIdentifier" /> belongs to: the one stored
    /// under its request identifier AND created for that very task. Identifiers are caller-chosen and survive
    /// relaunches, so an older task — created by a previous process, or superseded by a re-send —
    /// can carry the identifier of a newer request and must never complete it.
    /// </summary>
    private NSUrlRequestHandle? FindOwningHandle(string requestIdentifier, nuint taskIdentifier)
        => _pendingRequests.TryGetValue(requestIdentifier, out var handle) && handle.TaskIdentifier == taskIdentifier ? handle : null;

    /// <summary>
    /// Test seam: the native task identifier of the pending request <paramref name="requestIdentifier" />
    /// (the callback-injection harness gives its fake tasks the identity of the real ones).
    /// </summary>
    internal bool TryGetPendingTaskIdentifier(string requestIdentifier, out nuint taskIdentifier)
    {
        if (_pendingRequests.TryGetValue(requestIdentifier, out var handle))
        {
            taskIdentifier = handle.TaskIdentifier;

            return true;
        }

        taskIdentifier = 0;

        return false;
    }

    /// <summary>
    /// Faults the request of this process that <paramref name="task" /> belongs to, if any, so a
    /// contained callback failure surfaces to the awaiting caller instead of leaking the handle.
    /// </summary>
    private void FaultOwningRequest(NSUrlSessionTask task, Exception exception)
    {
        try
        {
            if (task.TaskDescription is not { Length: > 0 } requestIdentifier
                || FindOwningHandle(requestIdentifier, task.TaskIdentifier) is not { IsLostRequest: false } handle)
            {
                return;
            }

            handle.ResponseCompletionSource.TrySetException(new HttpRequestException("Failed to process session callback", exception));
            CompleteAndRemoveHandle(handle);
        }
        catch (Exception)
        {
            // The task's own getters may be what failed: nothing more can be done for it.
        }
    }

    /// <summary>
    /// Runs download processing OFF the serial delegate queue, fire-and-forget. Keeping
    /// <see cref="DidFinishDownloading" /> near-instant is what lets burst completions all
    /// secure their temp files before the system reclaims them. Once a download is secured,
    /// tasks are fully independent — state lives on each request's own handle, the shared maps
    /// are concurrent, and completion races are settled by TrySet* — so processing runs in
    /// parallel with no ordering requirements.
    /// </summary>
    /// <remarks>
    /// <paramref name="stagedFilePath" /> is the secured download awaiting processing: when
    /// <paramref name="action" /> throws before consuming it, it must be deleted here or it
    /// leaks in the temp directory until the system purges it (deleting is idempotent, so a
    /// path the action already moved or removed is safe to pass).
    /// </remarks>
    private void ProcessCallback(NSUrlSessionTask task, Action action, string? stagedFilePath)
    {
        // Counted so a background wake waits for deferred processing before letting iOS suspend us.
        Interlocked.Increment(ref _inFlightProcessing);

        _ = Task.Run(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Unhandled error while processing a session callback for {RequestIdentifier}", SafeTaskDescription(task));
                    DeleteStagedFile(stagedFilePath);
                    FaultOwningRequest(task, ex);
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlightProcessing);
                    Volatile.Write(ref _lastCompletedTaskTimestamp, Stopwatch.GetTimestamp());
                }
            }
        );
    }

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

        // Facts that tell the causes apart: the downloads FOLDER gone means Library/Caches was
        // cleared wholesale (an app "clear cache"); folder present but file gone points at an iOS
        // low-storage purge; a file outside the current container means the container moved
        // (app update) and the system handed out a stale path.
        var sourcePath = location.Path!;
        var downloadsFolder = Path.GetDirectoryName(sourcePath);
        var folderIsDirectory = false;
        var folderExists = downloadsFolder is not null && fileManager.FileExists(downloadsFolder, ref folderIsDirectory) && folderIsDirectory;
        var insideContainer = IsInsideCurrentContainer(sourcePath) is { } inside ? inside ? "yes" : "NO" : "unknown";

        // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
        failureReason = $"{FormatNSError(moveError)} (source file exists: {fileManager.FileExists(sourcePath)}, downloads folder exists: {folderExists}, inside current app container: {insideContainer})";
        Logger.LogDebug("Failed to stage downloaded file {Source}: {Reason}", sourcePath, failureReason);

        return null;
    }

    /// <summary>
    /// Whether <paramref name="path" /> lives in the app's CURRENT data container (compared by
    /// container id, ignoring the /private and /.nofollow prefixes); null when undeterminable.
    /// </summary>
    private static bool? IsInsideCurrentContainer(string path)
    {
        var current = ExtractContainerId(NSFileManager.HomeDirectory);
        var candidate = ExtractContainerId(path);

        return current is null || candidate is null ? null : string.Equals(current, candidate, StringComparison.OrdinalIgnoreCase);

        static string? ExtractContainerId(string? value)
        {
            const string marker = "/Containers/Data/Application/";
            var index = value?.IndexOf(marker, StringComparison.Ordinal) ?? -1;

            if (index < 0)
            {
                return null;
            }

            var start = index + marker.Length;
            var end = value!.IndexOf('/', start);

            return end < 0 ? value[start..] : value[start..end];
        }
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
        while (true)
        {
            long generation;
            Task delivered;

            lock (_backgroundEventsLock)
            {
                generation = _wakeGeneration;
                delivered = _backgroundEventsDelivered?.Task ?? Task.CompletedTask;
            }

            try
            {
                await WaitBackgroundWakeAsync(delivered).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while waiting for background events processing");
            }

            List<Action> handlers;
            bool moreWakes;

            lock (_backgroundEventsLock)
            {
                // Serves the wakes announced up to this round; a wake that joined meanwhile gets
                // its own round (its events may not have been waited for yet).
                handlers = _pendingCompletionHandlers.Where(h => h.Generation <= generation).Select(h => h.Handler).ToList();
                _pendingCompletionHandlers.RemoveAll(h => h.Generation <= generation);
                moreWakes = _pendingCompletionHandlers.Count > 0;

                if (!moreWakes)
                {
                    _backgroundWaitRunning = false;
                    _backgroundEventsDelivered = null;
                }
            }

            Logger.LogDebug("WaitEventsProcessingAndNotify completed for {Count} wake(s)", handlers.Count);

            // The system-provided completion handler MUST always be invoked, otherwise iOS may
            // throttle or stop delivering background URL session events to the app.
            foreach (var handler in handlers)
            {
                DispatchQueue.MainQueue.DispatchAsync(handler);
            }

            if (!moreWakes)
            {
                return;
            }
        }
    }

    private async Task WaitBackgroundWakeAsync(Task delivered)
    {
        var started = Stopwatch.GetTimestamp();

        // 1. Every enqueued event delivered — DidFinishEventsForBackgroundSession, Apple's
        //    documented signal. Calling the handler earlier lets iOS suspend the app while events
        //    it already acknowledged still sit on our delegate queue, their files exposed in
        //    Library/Caches. Fallback, should the signal never come: a quiet period, capped.
        while (!delivered.IsCompleted
               && Stopwatch.GetElapsedTime(started) < _backgroundEventsDeliveryTimeout
               && (Stopwatch.GetElapsedTime(started) < _backgroundQuietFallback
                   || Stopwatch.GetElapsedTime(Volatile.Read(ref _lastCompletedTaskTimestamp)) < _backgroundQuietFallback))
        {
            await Task.WhenAny(delivered, Task.Delay(200)).ConfigureAwait(false);
        }

        Logger.LogDebug("Background events delivered: {Delivered}", delivered.IsCompleted);

        // A short quiet floor even when delivery was already signaled: never let iOS suspend the
        // app synchronously inside the wake announcement or right behind a burst of callbacks.
        while (Stopwatch.GetElapsedTime(Volatile.Read(ref _lastCompletedTaskTimestamp)) < _backgroundQuietFloor
               && Stopwatch.GetElapsedTime(started) < _backgroundProcessingBudget)
        {
            await Task.Delay(100).ConfigureAwait(false);
        }

        // 2. Their deferred processing (thread pool) has run.
        while (Volatile.Read(ref _inFlightProcessing) > 0 && Stopwatch.GetElapsedTime(started) < _backgroundProcessingBudget)
        {
            await Task.Delay(50).ConfigureAwait(false);
        }

        // 3. Recently delivered responses acknowledged by their consumer (awaiter or lost-message
        //    handler), within the remaining budget — including those processed BEFORE the wake was
        //    announced. Responses delivered long ago (held by the app) are not this wake's business.
        var acknowledgeTasks = _unacknowledgedHandles
                               .Where(entry => Stopwatch.GetElapsedTime(entry.Value) < Stopwatch.GetElapsedTime(started) + _backgroundAcknowledgeLookBack)
                               .Select(entry => entry.Key.CompletedTask)
                               .ToList();

        if (acknowledgeTasks.Count > 0)
        {
            var remaining = _backgroundProcessingBudget - Stopwatch.GetElapsedTime(started);
            await Task.WhenAny(Task.WhenAll(acknowledgeTasks), Task.Delay(remaining > TimeSpan.FromMilliseconds(500) ? remaining : TimeSpan.FromMilliseconds(500))).ConfigureAwait(false);
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

    // Sanitized like the response path: the identifier is caller-provided header content and
    // must not be able to escape the temp directory or produce an invalid file name. Unique per
    // transfer: two transfers may share an identifier (a re-send after the first completed).
    private static string GetRequestBodyPath(string requestIdentifier)
        => Path.Combine(Path.GetTempPath(), $"{ToSafeUnixFileName(requestIdentifier)}-{Guid.NewGuid():N}.nsrequest");

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

    /// <summary>
    /// Maps NSURLErrorDomain codes to <see cref="HttpRequestError" /> so callers can branch on
    /// <see cref="HttpRequestException.HttpRequestError" /> without parsing messages — the same
    /// contract SocketsHttpHandler provides since .NET 8. Codes without a faithful counterpart
    /// (e.g. -1001 native timeout) stay <see cref="HttpRequestError.Unknown" />.
    /// </summary>
    private static HttpRequestError MapHttpRequestError(NSError? error)
    {
        if (error is null || error.Domain?.ToString() != "NSURLErrorDomain")
        {
            return HttpRequestError.Unknown;
        }

        return (long) error.Code switch
        {
            -1003 or -1006 => HttpRequestError.NameResolutionError, // CannotFindHost, DNSLookupFailed
            -1004 or -1005 or -1009 or -1018 or -1020 => HttpRequestError.ConnectionError, // CannotConnectToHost, NetworkConnectionLost, NotConnectedToInternet, RoamingOff, DataNotAllowed
            -1022 or (>= -1206 and <= -1200) => HttpRequestError.SecureConnectionError, // ATS violation, TLS/certificate family
            -1007 => HttpRequestError.HttpProtocolError, // HTTPTooManyRedirects
            -1011 or -1017 => HttpRequestError.InvalidResponse, // BadServerResponse, CannotParseResponse
            -1013 => HttpRequestError.UserAuthenticationError, // UserAuthenticationRequired
            _ => HttpRequestError.Unknown
        };
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
