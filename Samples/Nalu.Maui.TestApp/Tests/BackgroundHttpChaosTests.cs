#if IOS && !MACCATALYST
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Harness page for the CHAOS-SERVER fault matrix: sends requests through the background
/// NSUrlSession pipeline to a Mac-hosted <c>Nalu.ChaosServer</c> (Tools/Nalu.ChaosServer) whose
/// per-path behaviors produce real network faults (truncation, RST, garbage, stalls, drips…).
/// Driven by UITests.DevFlow's <c>BackgroundHttpChaosUiTests</c>; also usable manually with the
/// standalone server (<c>dotnet run --project Tools/Nalu.ChaosServer</c>).
/// </summary>
/// <remarks>
/// Results are exposed in INVARIANT machine-readable labels: <c>ChaosLastResult</c> is
/// "pending" → "OK {status} len={n}" / "ERR {ExceptionType}: {messages}" / "CANCELED";
/// <c>ChaosLastBody</c> carries the (truncated) response body; <c>ChaosSummaryLabel</c>
/// aggregates parallel runs as "done=N ok=A err=B canceled=C".
/// </remarks>
[UsedImplicitly]
[TestPage("Background Http Chaos")]
public class BackgroundHttpChaosTests : ContentPage
{
    private const string BaseUrlPreferenceKey = "ChaosServerBaseUrl";

    // Newest-first log; static so it survives page re-opens within one app run.
    private static readonly ObservableCollection<string> _log = [];

    private readonly Entry _baseUrlEntry;
    private readonly Entry _pathEntry;
    private readonly Entry _timeoutEntry;
    private readonly Label _modeLabel;
    private readonly Label _lastResultLabel;
    private readonly Label _lastBodyLabel;
    private readonly Label _summaryLabel;
    private CancellationTokenSource? _flightCts;
    private int _requestNumber;

    public BackgroundHttpChaosTests()
    {
        _baseUrlEntry = new Entry
                        {
                            Placeholder = "http://192.168.1.x:9666",
                            Text = Preferences.Default.Get(BaseUrlPreferenceKey, string.Empty),
                            AutomationId = "ChaosBaseUrl",
                            MinimumWidthRequest = 220,
                            Keyboard = Keyboard.Url
                        };
        _baseUrlEntry.TextChanged += (_, e) => Preferences.Default.Set(BaseUrlPreferenceKey, e.NewTextValue ?? string.Empty);

        _pathEntry = new Entry { Placeholder = "/ok", Text = "/ok", AutomationId = "ChaosPath", MinimumWidthRequest = 220, Keyboard = Keyboard.Url };
        _timeoutEntry = new Entry { Placeholder = "timeout ms (0=none)", Text = "0", AutomationId = "ChaosTimeoutMs", MinimumWidthRequest = 80, Keyboard = Keyboard.Numeric };

        _modeLabel = new Label { AutomationId = "ChaosModeLabel", Text = BackgroundHttpClientFactory.Mode, FontSize = 11 };
        _lastResultLabel = new Label { AutomationId = "ChaosLastResult", Text = "idle", FontSize = 11 };
        _lastBodyLabel = new Label { AutomationId = "ChaosLastBody", Text = string.Empty, FontSize = 10, LineBreakMode = LineBreakMode.CharacterWrap };
        _summaryLabel = new Label { AutomationId = "ChaosSummaryLabel", Text = string.Empty, FontSize = 11 };

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 _baseUrlEntry,
                                 _pathEntry,
                                 _timeoutEntry,
                                 MakeButton("GET", "ChaosGetButton", () => _ = SendAsync(HttpMethod.Get)),
                                 MakeButton("POST multipart", "ChaosPostButton", () => _ = SendAsync(HttpMethod.Post, CreateMultipartContent())),
                                 MakeButton("8 parallel", "ChaosParallelButton", () => _ = SendParallelAsync(8)),
                                 MakeButton("DefaultTimeout 3s", "ChaosNativeTimeoutButton", () => _ = SendAsync(HttpMethod.Get, nativeTimeout: TimeSpan.FromSeconds(3))),
                                 MakeButton("Cancel", "ChaosCancelButton", () => _flightCts?.Cancel()),
                                 MakeButton("Clear", "ChaosClearButton", Clear)
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var statusLayout = new VerticalStackLayout
                           {
                               Padding = new Thickness(16, 0),
                               Spacing = 2,
                               Children = { _modeLabel, _lastResultLabel, _summaryLabel, _lastBodyLabel }
                           };

        var logScroll = new VirtualScroll
                        {
                            AutomationId = "ChaosLog",
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
    }

    private void Clear()
    {
        // Cancel leftovers too: a faulted background download RETRIES silently (for up to the
        // session's 24h resource timeout) and would keep hitting the server — and would try to
        // write the result labels — long after the test that started it moved on.
        _flightCts?.Cancel();
        _log.Clear();
        _lastResultLabel.Text = "idle";
        _lastBodyLabel.Text = string.Empty;
        _summaryLabel.Text = string.Empty;
    }

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private static void Append(string message)
        => MainThread.BeginInvokeOnMainThread(() => _log.Insert(0, $"{DateTime.Now:HH:mm:ss.f} {message}"));

    private Uri BuildUri()
    {
        var path = _pathEntry.Text?.Trim() ?? "/ok";

        if (path.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return new Uri(path);
        }

        var baseUrl = _baseUrlEntry.Text?.Trim().TrimEnd('/') ?? string.Empty;

        return new Uri(baseUrl + (path.StartsWith('/') ? path : "/" + path));
    }

    private CancellationTokenSource CreateFlightCts()
    {
        _flightCts?.Dispose();
        var cts = new CancellationTokenSource();

        if (int.TryParse(_timeoutEntry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timeoutMs) && timeoutMs > 0)
        {
            cts.CancelAfter(timeoutMs);
        }

        _flightCts = cts;

        return cts;
    }

    private async Task SendAsync(HttpMethod method, HttpContent? content = null, TimeSpan? nativeTimeout = null)
    {
        var requestNumber = Interlocked.Increment(ref _requestNumber);
        var uri = BuildUri();
        var cts = CreateFlightCts();

        MainThread.BeginInvokeOnMainThread(() =>
            {
                _lastResultLabel.Text = "pending";
                _lastBodyLabel.Text = string.Empty;
            }
        );
        Append($"#{requestNumber} {method.Method} {uri}");

        var (result, body) = await ExecuteAsync(method, uri, content, cts.Token, nativeTimeout);

        MainThread.BeginInvokeOnMainThread(() =>
            {
                // Only the NEWEST request owns the result labels: an abandoned faulted request
                // that settles after minutes of background retries must not clobber the outcome
                // the current scenario is waiting on.
                if (Volatile.Read(ref _requestNumber) == requestNumber)
                {
                    _lastResultLabel.Text = result;
                    _lastBodyLabel.Text = body;
                }
            }
        );
        Append($"#{requestNumber} {result}");
    }

    private async Task SendParallelAsync(int count)
    {
        var uri = BuildUri();
        var cts = CreateFlightCts();
        MainThread.BeginInvokeOnMainThread(() => _summaryLabel.Text = "running");
        Append($"parallel x{count} GET {uri}");

        var results = await Task.WhenAll(Enumerable.Range(0, count).Select(_ => ExecuteAsync(HttpMethod.Get, uri, null, cts.Token)));

        var ok = results.Count(r => r.Result.StartsWith("OK", StringComparison.Ordinal));
        var canceled = results.Count(r => r.Result == "CANCELED");
        var err = results.Length - ok - canceled;

        var summary = FormattableString.Invariant($"done={results.Length} ok={ok} err={err} canceled={canceled}");
        MainThread.BeginInvokeOnMainThread(() => _summaryLabel.Text = summary);
        Append(summary);
    }

    private static async Task<(string Result, string Body)> ExecuteAsync(HttpMethod method, Uri uri, HttpContent? content, CancellationToken cancellationToken, TimeSpan? nativeTimeout = null)
    {
        try
        {
            using var request = new HttpRequestMessage(method, uri) { Content = content };

            // nativeTimeout probes the NATIVE per-request timeout (DefaultTimeout →
            // NSMutableUrlRequest.TimeoutInterval) through the delegate directly — the shared
            // client deliberately runs with the infinite default.
            using var response = nativeTimeout is { } timeout
                ? await MessageHandlerNSUrlSessionDownloadDelegate.Current.SendAsync(request, null, timeout, cancellationToken)
                : await BackgroundHttpClientFactory.Client.SendAsync(request, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            var body = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 400));

            return (FormattableString.Invariant($"OK {(int) response.StatusCode} len={bytes.Length}"), body);
        }
        catch (OperationCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            // The handler's DefaultTimeout (HttpClient.Timeout convention: TaskCanceledException
            // wrapping TimeoutException) — distinct from a plain caller-side cancel.
            return ("TIMEOUT", string.Empty);
        }
        catch (OperationCanceledException)
        {
            return ("CANCELED", string.Empty);
        }
        catch (HttpRequestException ex)
        {
            // The HttpRequestError enum in brackets: SocketsHttpHandler parity that tests
            // assert on without parsing localized messages.
            return ($"ERR HttpRequestException[{ex.HttpRequestError}]: {Flatten(ex)}", string.Empty);
        }
        catch (Exception ex)
        {
            return ($"ERR {ex.GetType().Name}: {Flatten(ex)}", string.Empty);
        }
    }

    private static string Flatten(Exception ex)
    {
        var parts = new List<string>();

        for (var current = ex; current is not null && parts.Count < 4; current = current.InnerException)
        {
            parts.Add(current.Message);
        }

        var message = string.Join(" <- ", parts);

        return message.Length <= 400 ? message : message[..400];
    }

    private static MultipartFormDataContent CreateMultipartContent()
    {
        var payload = new byte[8192];
        Random.Shared.NextBytes(payload);

        return new MultipartFormDataContent
               {
                   { new StringContent("chaos"), "name" },
                   { new ByteArrayContent(payload), "file", "payload.bin" }
               };
    }
}
#endif
