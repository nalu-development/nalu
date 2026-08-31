#if IOS && !MACCATALYST
using System.Collections.ObjectModel;
using Foundation;
using JetBrains.Annotations;
using ObjCRuntime;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Self-asserting CALLBACK-INJECTION harness for
/// <see cref="MessageHandlerNSUrlSessionDownloadDelegate" />: invokes the delegate's session
/// callbacks directly with FAKE <c>NSUrlSessionDownloadTask</c> objects to reach every error
/// branch deterministically — states that real networking can't produce on demand (Running /
/// Suspended / unknown), null task descriptions, non-HTTP responses, missing download files,
/// property getters that throw (the SIGABRT-protection paths), duplicate callbacks, the
/// lost-request flow and the background-completion bookkeeping.
/// </summary>
/// <remarks>
/// <para>
/// Real pending requests are created through <c>SendAsync</c> against a stall URL (the chaos
/// server's <c>/stall</c>, or the default blackhole TEST-NET address) so the synthesized outcome
/// is observed exactly the way production code observes it: as the completion of the
/// <c>HttpClient</c> task.
/// </para>
/// <para>
/// "RUN ALL" executes the scenario list sequentially and publishes machine-readable results:
/// <c>CallbackSummary</c> is "idle" → "running k/N" → "PASS N/N" / "FAIL k/N" / "SKIP: reason",
/// with per-scenario detail in <c>CallbackFailures</c> and the log list.
/// Driven by UITests.DevFlow's <c>BackgroundHttpCallbackUiTests</c>; also runnable by hand.
/// </para>
/// </remarks>
[UsedImplicitly]
[TestPage("Background Http Callbacks")]
public class BackgroundHttpCallbackTests : ContentPage
{
    private const string StallUrlPreferenceKey = "CallbackStallUrl";

    // 192.0.2.0/24 is TEST-NET-1: guaranteed unroutable, so a request to it stays pending
    // for minutes — long enough to synthesize its outcome. No server needed.
    private const string DefaultStallUrl = "http://192.0.2.1:81/stall";

    private static readonly ObservableCollection<string> _log = [];

    private readonly Entry _stallUrlEntry;
    private readonly Label _summaryLabel;
    private readonly Label _failuresLabel;
    private int _running;

    public BackgroundHttpCallbackTests()
    {
        _stallUrlEntry = new Entry
                         {
                             Placeholder = DefaultStallUrl,
                             Text = Preferences.Default.Get(StallUrlPreferenceKey, DefaultStallUrl),
                             AutomationId = "CallbackStallUrl",
                             MinimumWidthRequest = 260,
                             Keyboard = Keyboard.Url
                         };
        _stallUrlEntry.TextChanged += (_, e) => Preferences.Default.Set(StallUrlPreferenceKey, e.NewTextValue ?? string.Empty);

        _summaryLabel = new Label { AutomationId = "CallbackSummary", Text = "idle", FontSize = 12 };
        _failuresLabel = new Label { AutomationId = "CallbackFailures", Text = string.Empty, FontSize = 10, LineBreakMode = LineBreakMode.CharacterWrap };

        var runButton = new Button { Text = "RUN ALL", AutomationId = "CallbackRunAllButton", FontSize = 12 };
        runButton.Clicked += (_, _) => _ = Task.Run(RunAllAsync);

        var clearButton = new Button { Text = "Clear", AutomationId = "CallbackClearButton", FontSize = 12 };
        clearButton.Clicked += (_, _) =>
        {
            _log.Clear();
            _summaryLabel.Text = "idle";
            _failuresLabel.Text = string.Empty;
        };

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 _stallUrlEntry,
                                 runButton,
                                 clearButton
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var logScroll = new VirtualScroll
                        {
                            AutomationId = "CallbackLog",
                            ItemsSource = _log,
                            ItemTemplate = new DataTemplate(() =>
                                {
                                    var label = new Label { FontSize = 11, Margin = new Thickness(16, 2), LineBreakMode = LineBreakMode.CharacterWrap };
                                    label.SetBinding(Label.TextProperty, Binding.SelfPath);

                                    return label;
                                }
                            )
                        };

        var grid = new Grid
                   {
                       RowDefinitions =
                       [
                           new RowDefinition(GridLength.Auto),
                           new RowDefinition(GridLength.Auto),
                           new RowDefinition(GridLength.Auto),
                           new RowDefinition(GridLength.Star)
                       ]
                   };
        grid.Add(controlsLayout);
        grid.Add(_summaryLabel, 0, 1);
        grid.Add(_failuresLabel, 0, 2);
        grid.Add(logScroll, 0, 3);
        _summaryLabel.Margin = _failuresLabel.Margin = new Thickness(16, 2);

        Content = grid;
    }

    private static void Append(string message)
        => MainThread.BeginInvokeOnMainThread(() => _log.Insert(0, $"{DateTime.Now:HH:mm:ss.f} {message}"));

    private void SetSummary(string text)
        => MainThread.BeginInvokeOnMainThread(() => _summaryLabel.Text = text);

    private void SetFailures(string text)
        => MainThread.BeginInvokeOnMainThread(() => _failuresLabel.Text = text);

    #region Scenario engine

    private sealed class ScenarioFailedException(string message) : Exception(message);

    private static void Check(bool condition, string what)
    {
        if (!condition)
        {
            throw new ScenarioFailedException(what);
        }
    }

    private async Task RunAllAsync()
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return;
        }

        try
        {
            SetFailures(string.Empty);

            try
            {
                _ = SessionHandler;
            }
            catch (Exception ex)
            {
                SetSummary($"SKIP: {ex.Message}");

                return;
            }

            var scenarios = BuildScenarios();
            var failures = new List<string>();
            var completed = 0;

            foreach (var (name, run) in scenarios)
            {
                SetSummary($"running {completed}/{scenarios.Count}");

                try
                {
                    var scenarioTask = Task.Run(run);
                    var winner = await Task.WhenAny(scenarioTask, Task.Delay(25_000));

                    if (winner != scenarioTask)
                    {
                        throw new ScenarioFailedException("scenario watchdog expired (25s)");
                    }

                    await scenarioTask;
                    Append($"PASS {name}");
                }
                catch (Exception ex)
                {
                    var detail = ex is ScenarioFailedException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
                    failures.Add($"{name}: {detail}");
                    Append($"FAIL {name}: {detail}");
                }

                completed++;
            }

            if (failures.Count == 0)
            {
                SetSummary($"PASS {scenarios.Count}/{scenarios.Count}");
            }
            else
            {
                SetSummary($"FAIL {scenarios.Count - failures.Count}/{scenarios.Count}");
                var text = string.Join(" | ", failures);
                SetFailures(text.Length <= 900 ? text : text[..900]);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    private List<(string Name, Func<Task> Run)> BuildScenarios()
        =>
        [
            ("error-timeout", ErrorTimeoutAsync),
            ("error-canceled", ErrorCanceledAsync),
            ("error-mapping", ErrorMappingAsync),
            ("state-running", () => UnexpectedStateAsync(NSUrlSessionTaskState.Running, "running state")),
            ("state-suspended", () => UnexpectedStateAsync(NSUrlSessionTaskState.Suspended, "suspended state")),
            ("state-unknown", StateUnknownAsync),
            ("null-task-description", NullTaskDescriptionAsync),
            ("state-getter-throws", StateGetterThrowsAsync),
            ("response-not-http", ResponseNotHttpAsync),
            ("missing-download-file", MissingDownloadFileAsync),
            ("response-getter-throws", ResponseGetterThrowsAsync),
            ("happy-download", HappyDownloadAsync),
            ("duplicate-finish", DuplicateFinishAsync),
            ("duplicate-identifier", DuplicateIdentifierAsync),
            ("lost-download", LostDownloadAsync),
            ("lost-error", LostErrorAsync),
            ("response-file-unlinked", ResponseFileUnlinkedAsync),
            ("staged-file-race", StagedFileRaceAsync),
            ("burst-completions", BurstCompletionsAsync),
            ("orphan-cleanup", OrphanCleanupAsync),
            ("cancellation", CancellationAsync),
            ("tmp-dir-purged", TmpDirPurgedAsync),
            ("background-completion", BackgroundCompletionAsync),
            ("session-invalid-recovery", SessionInvalidRecoveryAsync)
        ];

    #endregion

    #region Delegate plumbing

    private static MessageHandlerNSUrlSessionDownloadDelegate SessionHandler => MessageHandlerNSUrlSessionDownloadDelegate.Current;

    /// <summary>
    /// A managed stand-in for a native download task: never allocates a native object
    /// (<see cref="NSObjectFlag.Empty" />), overriding every member the delegate touches so the
    /// callbacks can be driven with arbitrary — including throwing — task state.
    /// </summary>
    private sealed class FakeTask : NSUrlSessionDownloadTask
    {
        public string? Desc { get; set; }
        public NSUrlSessionTaskState TaskState { get; set; } = NSUrlSessionTaskState.Completed;
        public NSError? TaskError { get; set; }
        public NSUrlResponse? TaskResponse { get; set; }
        public bool ThrowOnStateAccess { get; set; }
        public bool ThrowOnResponseAccess { get; set; }

        public FakeTask() : base(NSObjectFlag.Empty)
        {
        }

        public override string? TaskDescription
        {
            get => Desc;
            set => Desc = value;
        }

        public override NSUrlSessionTaskState State
            => ThrowOnStateAccess ? throw new InvalidOperationException("FakeTask.State access failure") : TaskState;

        public override NSError? Error => TaskError;

        public override NSUrlResponse? Response
            => ThrowOnResponseAccess ? throw new InvalidOperationException("FakeTask.Response access failure") : TaskResponse;

        public override NSUrlRequest? CurrentRequest => null;

        public override NSUrlRequest? OriginalRequest => null;
    }

    private sealed class PendingRequest(string id, Task<HttpResponseMessage> responseTask, CancellationTokenSource cts) : IDisposable
    {
        public string Id => id;
        public Task<HttpResponseMessage> ResponseTask => responseTask;
        public CancellationTokenSource Cts => cts;

        public void Dispose()
        {
            // Kills the REAL stalled native task backing this pending request; the outcome the
            // scenario asserted on has already been synthesized at this point.
            try
            {
                cts.Cancel();
            }
            catch (Exception)
            {
                // Never let cleanup mask the scenario outcome.
            }

            cts.Dispose();
        }
    }

    /// <summary>
    /// A real <c>SendAsync</c> against the stall URL: the native task hangs connecting/waiting,
    /// leaving a genuine pending handle whose outcome the scenario then synthesizes.
    /// </summary>
    private HttpRequestMessage CreateStallRequest(string requestIdentifier)
    {
        var stallUrl = _stallUrlEntry.Text?.Trim() is { Length: > 0 } text ? text : DefaultStallUrl;

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(stallUrl));
        request.Headers.Add(NSUrlBackgroundSessionHttpMessageHandler.RequestIdentifierHeaderName, requestIdentifier);

        return request;
    }

    private async Task<PendingRequest> CreatePendingAsync(string name)
    {
        var id = $"cb-{name}-{Guid.NewGuid():N}";
        var cts = new CancellationTokenSource();

        var request = CreateStallRequest(id);
        var responseTask = SessionHandler.SendAsync(request, null, Timeout.InfiniteTimeSpan, cts.Token);

        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (!SessionHandler.GetPendingResponses().ContainsKey(id))
        {
            if (responseTask.IsCompleted)
            {
                // Most likely the stall URL answered or refused: surface the actual outcome.
                var outcome = responseTask.Exception?.GetBaseException().Message ?? "completed successfully";

                throw new ScenarioFailedException($"stall request settled before it could be used ({outcome}) — check the stall URL");
            }

            Check(DateTime.UtcNow < deadline, "pending request was never registered");
            await Task.Delay(50);
        }

        return new PendingRequest(id, responseTask, cts);
    }

    private static void InvokeCompletion(FakeTask task, NSError? error)
        => SessionHandler.DidCompleteWithError(null!, task, error);

    private static void InvokeFinished(FakeTask task, string filePath)
        => SessionHandler.DidFinishDownloading(null!, task, NSUrl.FromFilename(filePath));

    private static NSError MakeError(nint code, string? failingUrl = null)
    {
        var userInfo = failingUrl is null
            ? null
            : NSDictionary.FromObjectAndKey(new NSString(failingUrl), new NSString("NSErrorFailingURLStringKey"));

        return new NSError(NSError.NSUrlErrorDomain, code, userInfo);
    }

    private static string CreateDownloadFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"chaos-{Guid.NewGuid():N}.download");
        File.WriteAllText(path, content);

        return path;
    }

    private static NSHttpUrlResponse MakeHttpResponse(int statusCode = 200)
        => new(
            NSUrl.FromString("http://chaos.test/fake")!,
            statusCode,
            "HTTP/1.1",
            NSDictionary.FromObjectsAndKeys(
                [new NSString("yes"), new NSString("text/plain")],
                [new NSString("X-Chaos"), new NSString("Content-Type")]
            )
        );

    private static async Task<Exception> AwaitFaultAsync(Task task, string what, int timeoutMs = 10_000)
    {
        var winner = await Task.WhenAny(task, Task.Delay(timeoutMs));
        Check(winner == task, $"{what}: request did not settle within {timeoutMs}ms");

        try
        {
            await task;
        }
        catch (Exception ex)
        {
            return ex;
        }

        throw new ScenarioFailedException($"{what}: request succeeded but a fault was expected");
    }

    private static async Task WaitRemovedFromPendingAsync(string id)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);

        while (SessionHandler.GetPendingResponses().ContainsKey(id))
        {
            Check(DateTime.UtcNow < deadline, "handle was not removed from the pending requests");
            await Task.Delay(50);
        }
    }

    #endregion

    #region Scenarios

    private async Task ErrorTimeoutAsync()
    {
        using var pending = await CreatePendingAsync("timeout");
        InvokeCompletion(new FakeTask { Desc = pending.Id, TaskError = MakeError(-1001, "http://chaos.test/x") }, MakeError(-1001));

        var exception = await AwaitFaultAsync(pending.ResponseTask, "timeout error");
        Check(exception is HttpRequestException, $"expected HttpRequestException, got {exception.GetType().Name}");
        Check(exception.Message.Contains("NSURLErrorDomain", StringComparison.Ordinal), $"error domain missing from '{exception.Message}'");
        Check(exception.Message.Contains("-1001", StringComparison.Ordinal), $"error code missing from '{exception.Message}'");
        Check(exception.Message.Contains("http://chaos.test/x", StringComparison.Ordinal), $"failing URL missing from '{exception.Message}'");
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task ErrorCanceledAsync()
    {
        using var pending = await CreatePendingAsync("canceled");
        InvokeCompletion(new FakeTask { Desc = pending.Id, TaskError = MakeError(-999) }, MakeError(-999));

        var exception = await AwaitFaultAsync(pending.ResponseTask, "cancel error");
        Check(exception is OperationCanceledException, $"expected OperationCanceledException, got {exception.GetType().Name}");
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task ErrorMappingAsync()
    {
        // NSURLError codes must map to HttpRequestError (SocketsHttpHandler parity) so callers
        // can branch on the enum instead of parsing messages: -1003 = CannotFindHost.
        using var pending = await CreatePendingAsync("mapping");
        InvokeCompletion(new FakeTask { Desc = pending.Id, TaskError = MakeError(-1003) }, MakeError(-1003));

        var exception = await AwaitFaultAsync(pending.ResponseTask, "DNS error mapping");
        Check(exception is HttpRequestException, $"expected HttpRequestException, got {exception.GetType().Name}");
        Check(((HttpRequestException) exception).HttpRequestError == HttpRequestError.NameResolutionError,
            $"expected HttpRequestError.NameResolutionError, got {((HttpRequestException) exception).HttpRequestError}");
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task UnexpectedStateAsync(NSUrlSessionTaskState state, string expectedMessagePart)
    {
        using var pending = await CreatePendingAsync("state");
        InvokeCompletion(new FakeTask { Desc = pending.Id, TaskState = state }, null);

        var exception = await AwaitFaultAsync(pending.ResponseTask, expectedMessagePart);
        Check(exception is InvalidOperationException, $"expected InvalidOperationException, got {exception.GetType().Name}");
        Check(exception.Message.Contains(expectedMessagePart, StringComparison.Ordinal), $"'{expectedMessagePart}' missing from '{exception.Message}'");
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task StateUnknownAsync()
    {
        using var pending = await CreatePendingAsync("unknown");
        InvokeCompletion(new FakeTask { Desc = pending.Id, TaskState = (NSUrlSessionTaskState) 99 }, null);

        var exception = await AwaitFaultAsync(pending.ResponseTask, "unknown state");
        Check(exception is HttpRequestException, $"expected HttpRequestException, got {exception.GetType().Name}");
        Check(exception.Message.Contains("Unknown task state", StringComparison.Ordinal), $"'Unknown task state' missing from '{exception.Message}'");
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task NullTaskDescriptionAsync()
    {
        using var pending = await CreatePendingAsync("nulldesc");

        // A callback with no task description must be IGNORED: it cannot be correlated, and
        // faulting an arbitrary request would be worse than logging.
        InvokeCompletion(new FakeTask { Desc = null, TaskError = MakeError(-1009) }, MakeError(-1009));
        await Task.Delay(750);
        Check(!pending.ResponseTask.IsCompleted, "an uncorrelatable callback settled a pending request");
        Check(SessionHandler.GetPendingResponses().ContainsKey(pending.Id), "the pending handle disappeared");

        // Cleanup through the normal error path, which also proves the handle survived intact.
        InvokeCompletion(new FakeTask { Desc = pending.Id, TaskError = MakeError(-1009) }, MakeError(-1009));
        _ = await AwaitFaultAsync(pending.ResponseTask, "cleanup completion");
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task StateGetterThrowsAsync()
    {
        using var pending = await CreatePendingAsync("statethrow");
        InvokeCompletion(new FakeTask { Desc = pending.Id, ThrowOnStateAccess = true }, null);

        var exception = await AwaitFaultAsync(pending.ResponseTask, "state getter failure");
        Check(exception is HttpRequestException, $"expected HttpRequestException, got {exception.GetType().Name}");
        Check(exception.Message.Contains("Failed to process session callback", StringComparison.Ordinal), $"guard message missing from '{exception.Message}'");
        Check(exception.InnerException?.Message.Contains("FakeTask.State", StringComparison.Ordinal) == true, "the original failure was not preserved as inner exception");
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task ResponseNotHttpAsync()
    {
        using var pending = await CreatePendingAsync("nothttp");
        var downloadFile = CreateDownloadFile("irrelevant");

        var task = new FakeTask
                   {
                       Desc = pending.Id,
                       TaskResponse = new NSUrlResponse(NSUrl.FromString("http://chaos.test/fake")!, "text/plain", -1, null)
                   };
        InvokeFinished(task, downloadFile);

        var exception = await AwaitFaultAsync(pending.ResponseTask, "non-HTTP response");
        Check(exception is HttpRequestException, $"expected HttpRequestException, got {exception.GetType().Name}");
        Check(exception.Message.Contains("Response is not NSHttpUrlResponse", StringComparison.Ordinal), $"unexpected message '{exception.Message}'");
        Check(((HttpRequestException) exception).HttpRequestError == HttpRequestError.InvalidResponse,
            $"expected HttpRequestError.InvalidResponse, got {((HttpRequestException) exception).HttpRequestError}");
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task MissingDownloadFileAsync()
    {
        using var pending = await CreatePendingAsync("nofile");
        var missingPath = Path.Combine(Path.GetTempPath(), $"chaos-missing-{Guid.NewGuid():N}.download");

        InvokeFinished(new FakeTask { Desc = pending.Id, TaskResponse = MakeHttpResponse() }, missingPath);

        var exception = await AwaitFaultAsync(pending.ResponseTask, "missing download file");
        Check(exception is HttpRequestException, $"expected HttpRequestException, got {exception.GetType().Name}");
        Check(exception.Message.Contains("Failed to process downloaded file", StringComparison.Ordinal), $"unexpected message '{exception.Message}'");
        Check(exception.InnerException?.Message.Contains("Failed to secure downloaded file", StringComparison.Ordinal) == true,
            $"staging failure reason missing from inner '{exception.InnerException?.Message}'");
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task ResponseGetterThrowsAsync()
    {
        using var pending = await CreatePendingAsync("respthrow");

        // Snapshot BEFORE: a processing failure after staging must not leak the staged
        // .nsdownload file (regression: the exception path used to skip DeleteStagedFile).
        var stagedFilesBefore = Directory.GetFiles(Path.GetTempPath(), "*.nsdownload").ToHashSet();
        var downloadFile = CreateDownloadFile("irrelevant");

        InvokeFinished(new FakeTask { Desc = pending.Id, ThrowOnResponseAccess = true }, downloadFile);

        var exception = await AwaitFaultAsync(pending.ResponseTask, "response getter failure");
        Check(exception is HttpRequestException, $"expected HttpRequestException, got {exception.GetType().Name}");
        Check(exception.Message.Contains("Failed to process session callback", StringComparison.Ordinal), $"guard message missing from '{exception.Message}'");
        await WaitRemovedFromPendingAsync(pending.Id);

        // Every .nsdownload staged by this scenario must be gone (deletion happens on the
        // callback's failure path, slightly after the fault surfaces — poll briefly).
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(3);

        while (true)
        {
            var leaked = Directory.GetFiles(Path.GetTempPath(), "*.nsdownload").Where(f => !stagedFilesBefore.Contains(f)).ToList();

            if (leaked.Count == 0)
            {
                break;
            }

            Check(DateTime.UtcNow < deadline, $"staged download leaked after processing failure: {string.Join(", ", leaked.Select(Path.GetFileName))}");
            await Task.Delay(100);
        }
    }

    private async Task HappyDownloadAsync()
    {
        using var pending = await CreatePendingAsync("happy");
        const string content = "hello-chaos-payload";
        var downloadFile = CreateDownloadFile(content);

        InvokeFinished(new FakeTask { Desc = pending.Id, TaskResponse = MakeHttpResponse() }, downloadFile);

        var winner = await Task.WhenAny(pending.ResponseTask, Task.Delay(10_000));
        Check(winner == pending.ResponseTask, "synthesized download did not produce a response");

        using (var response = await pending.ResponseTask)
        {
            Check((int) response.StatusCode == 200, $"expected 200, got {(int) response.StatusCode}");
            var text = await response.Content.ReadAsStringAsync();
            Check(text == content, $"body mismatch: '{text}'");
            Check(response.Headers.TryGetValues("X-Chaos", out var values) && values.First() == "yes", "X-Chaos response header missing");
        }

        // Disposing the content is the acknowledgment that releases the handle.
        await WaitRemovedFromPendingAsync(pending.Id);
        Check(!File.Exists(downloadFile), "the download file should have been moved out of its original location");
    }

    private async Task DuplicateFinishAsync()
    {
        using var pending = await CreatePendingAsync("duplicate");
        const string content = "first-delivery";
        var firstFile = CreateDownloadFile(content);

        InvokeFinished(new FakeTask { Desc = pending.Id, TaskResponse = MakeHttpResponse() }, firstFile);

        var winner = await Task.WhenAny(pending.ResponseTask, Task.Delay(10_000));
        Check(winner == pending.ResponseTask, "first delivery did not produce a response");

        using (var response = await pending.ResponseTask)
        {
            // A RE-DELIVERED completion for a settled request must be ignored (and its file discarded).
            var secondFile = CreateDownloadFile("second-delivery");
            InvokeFinished(new FakeTask { Desc = pending.Id, TaskResponse = MakeHttpResponse() }, secondFile);
            await Task.Delay(500);

            var text = await response.Content.ReadAsStringAsync();
            Check(text == content, $"the duplicate delivery corrupted the response: '{text}'");
        }

        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task DuplicateIdentifierAsync()
    {
        using var pending = await CreatePendingAsync("dupid");

        // A second request with the same identifier must ATTACH to the in-flight transfer,
        // not start a competing one.
        using var duplicateRequest = CreateStallRequest(pending.Id);
        var attachedTask = SessionHandler.SendAsync(duplicateRequest, null, Timeout.InfiniteTimeSpan, CancellationToken.None);

        await Task.Delay(250);
        Check(!attachedTask.IsCompleted, "the attached request settled on its own");
        Check(SessionHandler.GetPendingResponses().ContainsKey(pending.Id), "the pending handle disappeared");

        // Cancelling an attached awaiter stops ITS wait only, never the shared transfer.
        using var canceledCts = new CancellationTokenSource();
        await canceledCts.CancelAsync();
        using var canceledDuplicate = CreateStallRequest(pending.Id);
        var canceledWait = SessionHandler.SendAsync(canceledDuplicate, null, Timeout.InfiniteTimeSpan, canceledCts.Token);
        var canceledException = await AwaitFaultAsync(canceledWait, "canceled attached wait", 5_000);
        Check(canceledException is OperationCanceledException, $"expected OperationCanceledException, got {canceledException.GetType().Name}");
        Check(!pending.ResponseTask.IsCompleted, "cancelling an attached wait disturbed the original transfer");
        Check(SessionHandler.GetPendingResponses().ContainsKey(pending.Id), "the original pending handle disappeared after an attached-wait cancel");

        // Completing the transfer resolves BOTH awaiters with the very same response.
        const string content = "shared-completion";
        var downloadFile = CreateDownloadFile(content);
        InvokeFinished(new FakeTask { Desc = pending.Id, TaskResponse = MakeHttpResponse() }, downloadFile);

        var winner = await Task.WhenAny(Task.WhenAll(pending.ResponseTask, attachedTask), Task.Delay(10_000));
        Check(winner is Task<HttpResponseMessage[]>, "the original and attached awaiters did not both settle");
        Check(pending.ResponseTask.IsCompletedSuccessfully && attachedTask.IsCompletedSuccessfully, "both awaiters must succeed");
        Check(ReferenceEquals(await pending.ResponseTask, await attachedTask), "the attached awaiter received a different response instance");

        using var response = await pending.ResponseTask;
        var text = await response.Content.ReadAsStringAsync();
        Check(text == content, $"content mismatch: '{text}'");

        response.Dispose();
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task ResponseFileUnlinkedAsync()
    {
        using var pending = await CreatePendingAsync("unlink");
        const string content = "unlink-me-not";
        var downloadFile = CreateDownloadFile(content);

        InvokeFinished(new FakeTask { Desc = pending.Id, TaskResponse = MakeHttpResponse() }, downloadFile);

        var winner = await Task.WhenAny(pending.ResponseTask, Task.Delay(10_000));
        Check(winner == pending.ResponseTask, "no response was produced");

        using var response = await pending.ResponseTask;

        // Simulate a tmp purge landing BETWEEN delivery and consumption: POSIX unlink semantics
        // must keep the already-open response stream fully readable.
        var responsePath = Path.Combine(Path.GetTempPath(), pending.Id + ".nsresponse");
        Check(File.Exists(responsePath), $"expected the response content file at {responsePath}");
        File.Delete(responsePath);

        var text = await response.Content.ReadAsStringAsync();
        Check(text == content, $"content lost after unlink: '{text}'");

        response.Dispose();
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task StagedFileRaceAsync()
    {
        using var pending = await CreatePendingAsync("stagedrace");
        const string content = "raced-content";
        var downloadFile = CreateDownloadFile(content);

        // Emulates nsurlsessiond/tmp cleanup racing the DEFERRED processing: a hot loop deletes
        // any staged file the instant it appears. Whichever side wins, the request must settle
        // cleanly — success with intact content, or the documented processing failure.
        var preexisting = Directory.GetFiles(Path.GetTempPath(), "*.nsdownload").ToHashSet();
        using var raceCts = new CancellationTokenSource();

        var deleter = Task.Run(() =>
            {
                while (!raceCts.IsCancellationRequested)
                {
                    foreach (var file in Directory.GetFiles(Path.GetTempPath(), "*.nsdownload"))
                    {
                        if (!preexisting.Contains(file))
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception)
                            {
                                // Processing may have moved it away already.
                            }
                        }
                    }
                }
            }
        );

        InvokeFinished(new FakeTask { Desc = pending.Id, TaskResponse = MakeHttpResponse() }, downloadFile);

        var winner = await Task.WhenAny(pending.ResponseTask, Task.Delay(10_000));
        await raceCts.CancelAsync();
        await deleter;
        Check(winner == pending.ResponseTask, "the request must settle either way, never hang");

        if (pending.ResponseTask.IsCompletedSuccessfully)
        {
            using var response = await pending.ResponseTask;
            var text = await response.Content.ReadAsStringAsync();
            Check(text == content, $"processing won the race but the content is corrupt: '{text}'");
        }
        else
        {
            var exception = await AwaitFaultAsync(pending.ResponseTask, "staged file race");
            Check(exception is HttpRequestException, $"expected HttpRequestException, got {exception.GetType().Name}");
            Check(exception.Message.Contains("Failed to process", StringComparison.Ordinal), $"unexpected message '{exception.Message}'");
        }

        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task BurstCompletionsAsync()
    {
        // A burst of simultaneous completions — the field condition that motivated the
        // secure-first-process-later design — with one download file missing mid-burst:
        // the others must all survive it.
        const int count = 8;
        const int missingIndex = 3;
        var pendings = new List<PendingRequest>();

        try
        {
            for (var i = 0; i < count; i++)
            {
                pendings.Add(await CreatePendingAsync($"burst{i}"));
            }

            for (var i = 0; i < count; i++)
            {
                var file = i == missingIndex
                    ? Path.Combine(Path.GetTempPath(), $"chaos-missing-{Guid.NewGuid():N}.download")
                    : CreateDownloadFile($"burst-{i}");

                InvokeFinished(new FakeTask { Desc = pendings[i].Id, TaskResponse = MakeHttpResponse() }, file);
            }

            for (var i = 0; i < count; i++)
            {
                var winner = await Task.WhenAny(pendings[i].ResponseTask, Task.Delay(15_000));
                Check(winner == pendings[i].ResponseTask, $"burst request {i} never settled");

                if (i == missingIndex)
                {
                    var exception = await AwaitFaultAsync(pendings[i].ResponseTask, $"burst request {i}");
                    Check(exception.Message.Contains("Failed to process downloaded file", StringComparison.Ordinal), $"unexpected message '{exception.Message}'");
                }
                else
                {
                    using var response = await pendings[i].ResponseTask;
                    var text = await response.Content.ReadAsStringAsync();
                    Check(text == $"burst-{i}", $"burst request {i} content mismatch: '{text}'");
                }
            }

            foreach (var pending in pendings)
            {
                await WaitRemovedFromPendingAsync(pending.Id);
            }
        }
        finally
        {
            foreach (var pending in pendings)
            {
                pending.Dispose();
            }
        }
    }

    private async Task OrphanCleanupAsync()
    {
        // Orphans a previous process could have left behind — e.g. killed between a response
        // being delivered and its content being consumed. The delegate sweeps them at init;
        // this drives the sweep directly (in-flight stall requests are GETs, so no live file
        // matches the patterns here).
        var tempPath = Path.GetTempPath();
        string[] orphans =
        [
            Path.Combine(tempPath, $"orphan-{Guid.NewGuid():N}.nsresponse"),
            Path.Combine(tempPath, $"orphan-{Guid.NewGuid():N}.nsdownload"),
            Path.Combine(tempPath, $"orphan-{Guid.NewGuid():N}.nsrequest")
        ];

        foreach (var orphan in orphans)
        {
            await File.WriteAllTextAsync(orphan, "stale");
        }

        SessionHandler.CleanupOrphanedFiles();

        foreach (var orphan in orphans)
        {
            Check(!File.Exists(orphan), $"orphan survived the sweep: {Path.GetFileName(orphan)}");
        }
    }

    private async Task TmpDirPurgedAsync()
    {
        using var pending = await CreatePendingAsync("tmppurge");
        const string content = "reborn-from-purge";

        // The file iOS hands over lives OUTSIDE tmp here, so it survives the purge simulated below.
        var downloadFile = Path.Combine(FileSystem.CacheDirectory, $"chaos-{Guid.NewGuid():N}.download");
        await File.WriteAllTextAsync(downloadFile, content);

        // iOS may purge the ENTIRE temp directory: staging must recreate it and still deliver.
        Directory.Delete(Path.GetTempPath(), true);

        InvokeFinished(new FakeTask { Desc = pending.Id, TaskResponse = MakeHttpResponse() }, downloadFile);

        var winner = await Task.WhenAny(pending.ResponseTask, Task.Delay(10_000));
        Check(winner == pending.ResponseTask, "no response after the tmp purge");

        using var response = await pending.ResponseTask;
        var text = await response.Content.ReadAsStringAsync();
        Check(text == content, $"content mismatch after tmp-dir recreation: '{text}'");

        response.Dispose();
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task LostDownloadAsync()
    {
        var id = $"cb-lost-{Guid.NewGuid():N}";
        const string content = "lost-but-found";
        var downloadFile = CreateDownloadFile(content);

        var lostTcs = new TaskCompletionSource<(string Id, int Status, string Body)>(TaskCreationOptions.RunContinuationsAsynchronously);

        BackgroundHttpLostMessageHandler.Interceptor = async handle =>
        {
            try
            {
                using var response = await handle.GetResponseAsync();
                var body = await response.Content.ReadAsStringAsync();
                lostTcs.TrySetResult((handle.RequestIdentifier, (int) response.StatusCode, body));
            }
            catch (Exception ex)
            {
                lostTcs.TrySetException(ex);
            }
        };

        try
        {
            // No pending request for this identifier: the delegate must treat the download as a
            // LOST response and hand it to the registered lost-message handler.
            InvokeFinished(new FakeTask { Desc = id, TaskResponse = MakeHttpResponse() }, downloadFile);

            var winner = await Task.WhenAny(lostTcs.Task, Task.Delay(10_000));
            Check(winner == lostTcs.Task, "the lost-message handler was never invoked");

            var (lostId, status, body) = await lostTcs.Task;
            Check(lostId == id, $"lost handler received identifier '{lostId}'");
            Check(status == 200, $"lost handler received status {status}");
            Check(body == content, $"lost handler received body '{body}'");
        }
        finally
        {
            BackgroundHttpLostMessageHandler.Interceptor = null;
        }

        await WaitRemovedFromPendingAsync(id);
    }

    private async Task LostErrorAsync()
    {
        var id = $"cb-losterr-{Guid.NewGuid():N}";
        var intercepted = false;
        BackgroundHttpLostMessageHandler.Interceptor = _ =>
        {
            intercepted = true;

            return Task.CompletedTask;
        };

        try
        {
            // An ERROR completion with no pending request has nothing to hand over: it must be
            // absorbed (faulted internal handle, immediately released) without invoking the
            // lost-message handler and without leaking into the pending map.
            InvokeCompletion(new FakeTask { Desc = id, TaskError = MakeError(-1005) }, MakeError(-1005));
            await Task.Delay(750);

            Check(!SessionHandler.GetPendingResponses().ContainsKey(id), "the lost error completion leaked a pending handle");
            Check(!intercepted, "the lost-message handler must not receive error completions");
        }
        finally
        {
            BackgroundHttpLostMessageHandler.Interceptor = null;
        }
    }

    private async Task CancellationAsync()
    {
        using var pending = await CreatePendingAsync("cancel");
        await pending.Cts.CancelAsync();

        var exception = await AwaitFaultAsync(pending.ResponseTask, "cancellation", 5_000);
        Check(exception is OperationCanceledException, $"expected OperationCanceledException, got {exception.GetType().Name}");
        await WaitRemovedFromPendingAsync(pending.Id);
    }

    private async Task BackgroundCompletionAsync()
    {
        var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var accepted = SessionHandler.HandleEventsForBackgroundUrl(
            null!,
            MessageHandlerNSUrlSessionDownloadDelegate.SessionIdentifier,
            () => invoked.TrySetResult()
        );

        Check(accepted, "HandleEventsForBackgroundUrl did not accept its own session identifier");

        // The system completion handler MUST always fire (iOS throttles the app otherwise);
        // with no in-flight events it fires as soon as the quiet-period wait elapses.
        var winner = await Task.WhenAny(invoked.Task, Task.Delay(12_000));
        Check(winner == invoked.Task, "the background completion handler was never invoked");
    }

    private async Task SessionInvalidRecoveryAsync()
    {
        // LAST on purpose, and invalidating the REAL session rather than synthesizing the
        // callback: a synthetic DidBecomeInvalid leaves a live duplicate background session
        // with the same identifier, and on a physical device nsurlsessiond then wedges every
        // subsequent request into eternal "pending" (observed: the whole chaos suite red).
        // InvalidateAndCancel makes iOS deliver DidBecomeInvalid naturally — the production flow.
        var sessionField = typeof(MessageHandlerNSUrlSessionDownloadDelegate)
                           .GetField("_nsUrlSession", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                           ?? throw new ScenarioFailedException("_nsUrlSession field not found — the delegate implementation changed");

        var session = sessionField.GetValue(SessionHandler) as NSUrlSession;
        Check(session is not null, "no live session to invalidate");
        session!.InvalidateAndCancel();

        // DidBecomeInvalid (delivered on the delegate queue) nulls the field; wait for it.
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);

        while (sessionField.GetValue(SessionHandler) is not null)
        {
            Check(DateTime.UtcNow < deadline, "DidBecomeInvalid was never delivered after InvalidateAndCancel");
            await Task.Delay(100);
        }

        using var pending = await CreatePendingAsync("recovery");
        const string content = "post-invalidation";
        var downloadFile = CreateDownloadFile(content);

        InvokeFinished(new FakeTask { Desc = pending.Id, TaskResponse = MakeHttpResponse() }, downloadFile);

        var winner = await Task.WhenAny(pending.ResponseTask, Task.Delay(10_000));
        Check(winner == pending.ResponseTask, "no response after session invalidation — the session did not recover");

        using var response = await pending.ResponseTask;
        var text = await response.Content.ReadAsStringAsync();
        Check(text == content, $"body mismatch after recovery: '{text}'");
    }

    #endregion
}
#endif
