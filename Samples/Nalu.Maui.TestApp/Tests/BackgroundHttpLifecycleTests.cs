#if IOS && !MACCATALYST
using System.Collections.ObjectModel;
using System.Globalization;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Harness page for APP-LIFECYCLE background-HTTP scenarios: requests are started here, then the
/// HOST backgrounds, kills and relaunches the app (simctl / devicectl driven by
/// <c>BackgroundHttpLifecycleUiTests</c>) while the chaos server serves delayed responses and
/// faults. The page publishes machine-readable outcome labels that survive the choreography:
/// per-run counters (<c>LifecycleSummary</c>), lost-response counters accumulated by the
/// relaunched process (<c>LostSummary</c> via <see cref="BackgroundHttpLostResults" />),
/// <c>handleEventsForBackgroundURLSession</c> invocations (<c>BgEventsLabel</c>) and the live
/// pending-request count (<c>PendingLabel</c>).
/// </summary>
/// <remarks>
/// Every request carries an <c>X-NSUrlRequest-Identifier</c> header ("lc-…"), so responses that
/// outlive the process are delivered on relaunch through the lost-message flow. Opening the page
/// touches the session delegate on purpose: recreating the background session is what makes iOS
/// re-deliver events for tasks that completed while the app was dead.
/// </remarks>
[UsedImplicitly]
[TestPage("Background Http Lifecycle")]
public class BackgroundHttpLifecycleTests : ContentPage
{
    private const string BaseUrlPreferenceKey = "ChaosServerBaseUrl";

    // This log's post-relaunch burst of Insert(0) at page appearance is the flow that exposed
    // the VirtualScroll invalid-batch-updates crash (fixed; dedicated coverage in
    // "Virtual Scroll Burst Insert Tests") — keeping the VirtualScroll here doubles as an
    // in-situ regression canary.
    private static readonly ObservableCollection<string> _log = [];

    private readonly Entry _baseUrlEntry;
    private readonly Entry _countEntry;
    private readonly Entry _delayEntry;
    private readonly Label _summaryLabel;
    private readonly Label _lostSummaryLabel;
    private readonly Label _lostByKindLabel;
    private readonly Label _lostBytesLabel;
    private readonly Label _bgEventsLabel;
    private readonly Label _pendingLabel;

    private readonly List<CancellationTokenSource> _inFlight = [];
    private readonly Lock _stateLock = new();
    private int _done;
    private int _ok;
    private int _err;
    private int _canceled;
    private int _inflight;

    public BackgroundHttpLifecycleTests()
    {
        // Recreating the session delegate is what re-attaches to nsurlsessiond after a relaunch,
        // making it re-deliver completions for tasks that finished while the app was dead.
        try
        {
            _ = MessageHandlerNSUrlSessionDownloadDelegate.Current;
        }
        catch (Exception)
        {
            // Simulator without background-session support: the mode label says so.
        }

        _baseUrlEntry = new Entry
                        {
                            Placeholder = "http://192.168.1.x:9666",
                            Text = Preferences.Default.Get(BaseUrlPreferenceKey, string.Empty),
                            AutomationId = "LifecycleBaseUrl",
                            MinimumWidthRequest = 220,
                            Keyboard = Keyboard.Url
                        };
        _baseUrlEntry.TextChanged += (_, e) => Preferences.Default.Set(BaseUrlPreferenceKey, e.NewTextValue ?? string.Empty);

        _countEntry = new Entry { Placeholder = "count", Text = "3", AutomationId = "LifecycleCount", MinimumWidthRequest = 60, Keyboard = Keyboard.Numeric };
        _delayEntry = new Entry { Placeholder = "delay ms", Text = "5000", AutomationId = "LifecycleDelayMs", MinimumWidthRequest = 80, Keyboard = Keyboard.Numeric };

        _summaryLabel = new Label { AutomationId = "LifecycleSummary", FontSize = 11 };
        _lostSummaryLabel = new Label { AutomationId = "LostSummary", FontSize = 11 };
        _lostByKindLabel = new Label { AutomationId = "LostByKindLabel", FontSize = 11, LineBreakMode = LineBreakMode.CharacterWrap };
        _lostBytesLabel = new Label { AutomationId = "LostBytesLabel", FontSize = 11 };
        _bgEventsLabel = new Label { AutomationId = "BgEventsLabel", FontSize = 11 };
        _pendingLabel = new Label { AutomationId = "PendingLabel", FontSize = 11 };
        var modeLabel = new Label { AutomationId = "LifecycleModeLabel", Text = BackgroundHttpClientFactory.Mode, FontSize = 11 };

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 _baseUrlEntry,
                                 _countEntry,
                                 _delayEntry,
                                 MakeButton("Start N", "LifecycleStartButton", StartDelayedBatch),
                                 MakeButton("Upload", "LifecycleUploadButton", StartUpload),
                                 MakeButton("Slow upload", "LifecycleSlowUploadButton", StartSlowUpload),
                                 MakeButton("Fault", "LifecycleFaultButton", StartFault),
                                 MakeButton("Retrying", "LifecycleRetryButton", StartRetryingFault),
                                 MakeButton("Large", "LifecycleLargeButton", StartLarge),
                                 MakeButton("Cancel", "LifecycleCancelButton", CancelAll),
                                 MakeButton("Refresh", "LifecycleRefreshButton", UpdateLabels),
                                 MakeButton("Clear", "LifecycleClearButton", Clear)
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var statusLayout = new VerticalStackLayout
                           {
                               Padding = new Thickness(16, 0),
                               Spacing = 2,
                               Children = { modeLabel, _summaryLabel, _lostSummaryLabel, _lostByKindLabel, _lostBytesLabel, _bgEventsLabel, _pendingLabel }
                           };

        var logScroll = new VirtualScroll
                        {
                            AutomationId = "LifecycleLog",
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
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)]
                   };
        grid.Add(controlsLayout);
        grid.Add(statusLayout, 0, 1);
        grid.Add(logScroll, 0, 2);

        Content = grid;
        UpdateLabels();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        BackgroundHttpLostResults.Changed += OnLostResultsChanged;
        SyncLostLines();
        UpdateLabels();
    }

    protected override void OnDisappearing()
    {
        BackgroundHttpLostResults.Changed -= OnLostResultsChanged;
        base.OnDisappearing();
    }

    private void OnLostResultsChanged()
        => MainThread.BeginInvokeOnMainThread(() =>
            {
                SyncLostLines();
                UpdateLabels();
            }
        );

    private int _syncedLostLines;

    private void SyncLostLines()
    {
        var lines = BackgroundHttpLostResults.Lines;

        for (; _syncedLostLines < lines.Count; _syncedLostLines++)
        {
            Append(lines[_syncedLostLines]);
        }
    }

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private static void Append(string message)
        => MainThread.BeginInvokeOnMainThread(() => _log.Insert(0, $"{DateTime.Now:HH:mm:ss.f} {message}"));

    private void UpdateLabels()
        => MainThread.BeginInvokeOnMainThread(() =>
            {
                lock (_stateLock)
                {
                    _summaryLabel.Text = FormattableString.Invariant($"done={_done} ok={_ok} err={_err} canceled={_canceled} inflight={_inflight}");
                }

                _lostSummaryLabel.Text = BackgroundHttpLostResults.Summary;
                _lostByKindLabel.Text = BuildLostByKind();
                _lostBytesLabel.Text = FormattableString.Invariant($"lostBytes={BackgroundHttpLostResults.LostBytes}");
                _bgEventsLabel.Text = FormattableString.Invariant($"bgEvents={BackgroundHttpLostResults.BackgroundEvents}");
                _pendingLabel.Text = FormattableString.Invariant($"pending={GetPendingCount()}");
            }
        );

    /// <summary>
    /// Per-KIND lost stats ("kind=ok/err/bytes", kind parsed from the "lc-{kind}-…" identifier):
    /// kill tests assert on THEIR kind only, because a kill can make nsurlsessiond re-deliver a
    /// PREVIOUS test's not-fully-acknowledged event into this process ("ghost" deliveries that
    /// pollute the global counters).
    /// </summary>
    private static string BuildLostByKind()
    {
        var byKind = new SortedDictionary<string, (int Ok, int Err, long Bytes)>(StringComparer.Ordinal);

        foreach (var line in BackgroundHttpLostResults.Lines)
        {
            // "LOST lc-{kind}-{guid} OK {status} len={n}" / "LOST lc-{kind}-{guid} ERR {message}"
            var match = System.Text.RegularExpressions.Regex.Match(line, @"^LOST lc-([a-z]+)-\S+ (OK \d+ len=(\d+)|ERR)");

            if (!match.Success)
            {
                continue;
            }

            var kind = match.Groups[1].Value;
            var entry = byKind.TryGetValue(kind, out var existing) ? existing : (0, 0, 0L);

            if (match.Groups[3].Success)
            {
                entry = (entry.Item1 + 1, entry.Item2, entry.Item3 + long.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture));
            }
            else
            {
                entry = (entry.Item1, entry.Item2 + 1, entry.Item3);
            }

            byKind[kind] = entry;
        }

        return string.Join(' ', byKind.Select(kvp => FormattableString.Invariant($"{kvp.Key}={kvp.Value.Ok}/{kvp.Value.Err}/{kvp.Value.Bytes}")));
    }

    private static int GetPendingCount()
    {
        try
        {
            return NSUrlBackgroundSessionHttpMessageHandler.GetPendingResponses().Count;
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private void Clear()
    {
        CancelAll();
        BackgroundHttpLostResults.Reset();

        lock (_stateLock)
        {
            _done = _ok = _err = _canceled = 0;
        }

        _syncedLostLines = 0;
        _log.Clear();
        UpdateLabels();
    }

    private void CancelAll()
    {
        lock (_stateLock)
        {
            foreach (var cts in _inFlight)
            {
                try
                {
                    cts.Cancel();
                }
                catch (Exception)
                {
                    // Already disposed.
                }
            }

            _inFlight.Clear();
        }
    }

    private int DelayMs => int.TryParse(_delayEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 5000;
    private int Count => int.TryParse(_countEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? Math.Clamp(value, 1, 20) : 3;

    private Uri BuildUri(string pathAndQuery)
    {
        var baseUrl = _baseUrlEntry.Text?.Trim().TrimEnd('/') ?? string.Empty;

        return new Uri(baseUrl + pathAndQuery);
    }

    private void StartDelayedBatch()
    {
        var count = Count;

        for (var i = 0; i < count; i++)
        {
            _ = RunRequestAsync("batch", HttpMethod.Get, FormattableString.Invariant($"/delay?ms={DelayMs}"), null);
        }
    }

    private void StartUpload()
    {
        var payload = new byte[8192];
        Random.Shared.NextBytes(payload);

        var content = new MultipartFormDataContent
                      {
                          { new StringContent("lifecycle"), "name" },
                          { new ByteArrayContent(payload), "file", "payload.bin" }
                      };

        _ = RunRequestAsync("upload", HttpMethod.Post, FormattableString.Invariant($"/echo?delayms={DelayMs}"), content);
    }

    private void StartSlowUpload()
    {
        // 8MB against the server's throttled body reader (~6.4s to drain): far beyond any
        // socket buffering, so a kill fired seconds in lands genuinely MID-UPLOAD.
        var payload = new byte[8 * 1024 * 1024];
        Random.Shared.NextBytes(payload);

        var content = new MultipartFormDataContent
                      {
                          { new StringContent("lifecycle-slow"), "name" },
                          { new ByteArrayContent(payload), "file", "payload.bin" }
                      };

        _ = RunRequestAsync("slowupload", HttpMethod.Post, "/slow-read?chunk=65536&delayms=50", content);
    }

    private void StartFault()
        // The delay entry stretches each redirect hop, so the eventual -1007 can be timed to
        // land while the app is backgrounded or dead.
        => _ = RunRequestAsync("fault", HttpMethod.Get, DelayMs > 0 ? FormattableString.Invariant($"/redirect-loop?delayms={DelayMs}") : "/redirect-loop", null);

    private void StartRetryingFault()
        => _ = RunRequestAsync("retry", HttpMethod.Get, "/truncate?declared=100000&send=1000", null);

    private void StartLarge()
        // A dripped 4MB body (~4s): guarantees the download is MID-FLIGHT when the host kills
        // the app, on the simulator (localhost-fast) as much as on a device.
        => _ = RunRequestAsync("large", HttpMethod.Get, "/drip?bytes=4000000&delayms=100&chunk=100000", null);

    private async Task RunRequestAsync(string name, HttpMethod method, string pathAndQuery, HttpContent? content)
    {
        var id = $"lc-{name}-{Guid.NewGuid():N}";
        var cts = new CancellationTokenSource();

        lock (_stateLock)
        {
            _inFlight.Add(cts);
            _inflight++;
        }

        Append($"{id} started {method.Method} {pathAndQuery}");
        UpdateLabels();

        try
        {
            using var request = new HttpRequestMessage(method, BuildUri(pathAndQuery)) { Content = content };
            request.Headers.Add(NSUrlBackgroundSessionHttpMessageHandler.RequestIdentifierHeaderName, id);

            using var response = await BackgroundHttpClientFactory.Client.SendAsync(request, cts.Token);
            var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token);

            lock (_stateLock)
            {
                _ok++;
            }

            Append(FormattableString.Invariant($"{id} OK {(int) response.StatusCode} len={bytes.Length}"));
        }
        catch (OperationCanceledException)
        {
            lock (_stateLock)
            {
                _canceled++;
            }

            Append($"{id} CANCELED");
        }
        catch (Exception ex)
        {
            lock (_stateLock)
            {
                _err++;
            }

            Append($"{id} ERR {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            lock (_stateLock)
            {
                _done++;
                _inflight--;
                _inFlight.Remove(cts);
            }

            UpdateLabels();
        }
    }
}
#endif
