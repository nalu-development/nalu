#if IOS && !MACCATALYST
using System.Diagnostics;
using System.Globalization;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Harness page for the BURST reproduction: fires N simultaneous background-session downloads at
/// the chaos server and reports how many of them lost their downloaded file before the delegate
/// could stage it.
/// </summary>
/// <remarks>
/// <para>
/// The failure under investigation is "Failed to stage downloaded file … [NSPOSIXErrorDomain 2]
/// (source file exists: False)": the temp file nsurlsessiond handed over was ALREADY GONE when
/// <c>DidFinishDownloading</c> ran. Staging is a rename inside the app container, so it is
/// O(1) and payload-size independent — which means the loss happens BEFORE the callback gets its
/// turn on the serial delegate queue, not during the move.
/// </para>
/// <para>
/// Three axes are therefore exposed independently, because only varying them separately says
/// which one drives it: <b>burst size</b> (how deep the queue gets), <b>payload</b> (how much
/// the deferred processing allocates — the GC-pressure hypothesis) and <b>logger cost</b> (how
/// long each callback occupies the queue — see
/// <see cref="BackgroundHttpBurstLoggerProvider" />).
/// </para>
/// <para>
/// Results land in one INVARIANT machine-readable label, <c>BurstSummaryLabel</c>:
/// <c>done=N ok=A staging=B procerr=C err=D canceled=E elapsedMs=… maxMs=… logCalls=…</c>.
/// <c>staging</c> is the count this whole harness exists to move off zero.
/// </para>
/// </remarks>
[UsedImplicitly]
[TestPage("Background Http Burst")]
public class BackgroundHttpBurstTests : ContentPage
{
    // Shared with the chaos page so the LAN URL only has to be typed once per device.
    private const string BaseUrlPreferenceKey = "ChaosServerBaseUrl";

    /// <summary>Thrown by the handler when SecureDownloadedFile could not claim the temp file.</summary>
    private const string StagingMarker = "Failed to secure downloaded file";

    /// <summary>The outer wrapper for any failure of the deferred processing step.</summary>
    private const string ProcessingMarker = "Failed to process downloaded file";

    private readonly Entry _baseUrlEntry;
    private readonly Entry _pathEntry;
    private readonly Entry _countEntry;
    private readonly Entry _logDelayEntry;
    private readonly Label _modeLabel;
    private readonly Label _summaryLabel;
    private readonly Label _firstFailureLabel;
    private CancellationTokenSource? _flightCts;

    public BackgroundHttpBurstTests()
    {
        _baseUrlEntry = new Entry
                        {
                            Placeholder = "http://192.168.1.x:9666",
                            Text = Preferences.Default.Get(BaseUrlPreferenceKey, string.Empty),
                            AutomationId = "BurstBaseUrl",
                            MinimumWidthRequest = 220,
                            Keyboard = Keyboard.Url
                        };
        _baseUrlEntry.TextChanged += (_, e) => Preferences.Default.Set(BaseUrlPreferenceKey, e.NewTextValue ?? string.Empty);

        _pathEntry = new Entry { Placeholder = "/huge?mb=5", Text = "/huge?mb=5", AutomationId = "BurstPath", MinimumWidthRequest = 220, Keyboard = Keyboard.Url };
        _countEntry = new Entry { Placeholder = "burst", Text = "16", AutomationId = "BurstCount", MinimumWidthRequest = 70, Keyboard = Keyboard.Numeric };
        _logDelayEntry = new Entry { Placeholder = "log ms", Text = "0", AutomationId = "BurstLogDelayMs", MinimumWidthRequest = 70, Keyboard = Keyboard.Numeric };

        _modeLabel = new Label { AutomationId = "BurstModeLabel", Text = BackgroundHttpClientFactory.Mode, FontSize = 11 };
        _summaryLabel = new Label { AutomationId = "BurstSummaryLabel", Text = "idle", FontSize = 11, LineBreakMode = LineBreakMode.CharacterWrap };
        _firstFailureLabel = new Label { AutomationId = "BurstFirstFailure", Text = string.Empty, FontSize = 10, LineBreakMode = LineBreakMode.CharacterWrap };

        var controls = new HorizontalWrapLayout
                       {
                           _baseUrlEntry,
                           _pathEntry,
                           _countEntry,
                           _logDelayEntry,
                           MakeButton("Run burst", "BurstRunButton", () => _ = RunBurstAsync()),
                           MakeButton("Cancel", "BurstCancelButton", () => _flightCts?.Cancel())
                       };
        controls.HorizontalSpacing = 8;
        controls.VerticalSpacing = 8;
        controls.Padding = new Thickness(16, 8);

        Content = new VerticalStackLayout
                  {
                      Padding = new Thickness(0, 8),
                      Spacing = 4,
                      Children =
                      {
                          controls,
                          new VerticalStackLayout
                          {
                              Padding = new Thickness(16, 0),
                              Spacing = 2,
                              Children = { _modeLabel, _summaryLabel, _firstFailureLabel }
                          }
                      }
                  };
    }

    private static Button MakeButton(string text, string automationId, Action action)
        => new()
           {
               Text = text,
               AutomationId = automationId,
               Command = new Command(action)
           };

    private Uri BuildUri()
    {
        var baseUrl = (_baseUrlEntry.Text ?? string.Empty).TrimEnd('/');
        var path = _pathEntry.Text ?? "/ok";

        return new Uri($"{baseUrl}{(path.StartsWith('/') ? path : "/" + path)}");
    }

    private static int ParseInt(Entry entry, int fallback)
        => int.TryParse(entry.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value >= 0
            ? value
            : fallback;

    private async Task RunBurstAsync()
    {
        var count = Math.Max(1, ParseInt(_countEntry, 16));
        var logDelayMs = ParseInt(_logDelayEntry, 0);
        var uri = BuildUri();

        var cts = new CancellationTokenSource();
        _flightCts = cts;

        MainThread.BeginInvokeOnMainThread(() =>
            {
                _summaryLabel.Text = "running";
                _firstFailureLabel.Text = string.Empty;
            }
        );

        // The independent variable: how long each of the delegate's ~12 on-queue Log calls takes.
        BackgroundHttpBurstLoggerProvider.DelayMs = logDelayMs;
        BackgroundHttpBurstLoggerProvider.Reset();

        string summary;
        var firstFailure = string.Empty;

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Started together on purpose: the point is to make nsurlsessiond hand over many
            // finished downloads at once, so their callbacks pile up behind one another.
            var results = await Task.WhenAll(Enumerable.Range(0, count).Select(_ => ExecuteAsync(uri, cts.Token)));

            stopwatch.Stop();

            var ok = results.Count(r => r.Outcome == Outcome.Ok);
            var staging = results.Count(r => r.Outcome == Outcome.Staging);
            var procerr = results.Count(r => r.Outcome == Outcome.Processing);
            var canceled = results.Count(r => r.Outcome == Outcome.Canceled);
            var err = results.Count(r => r.Outcome == Outcome.Error);
            var maxMs = results.Length == 0 ? 0 : results.Max(r => r.ElapsedMs);

            firstFailure = results.FirstOrDefault(r => r.Outcome is Outcome.Staging or Outcome.Processing or Outcome.Error).Detail ?? string.Empty;

            summary = FormattableString.Invariant(
                $"done={results.Length} ok={ok} staging={staging} procerr={procerr} err={err} canceled={canceled} elapsedMs={stopwatch.ElapsedMilliseconds} maxMs={maxMs} logCalls={BackgroundHttpBurstLoggerProvider.Calls}"
            );
        }
        catch (Exception ex)
        {
            summary = $"FAILED {ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            // Never leave the app running with a deliberately slow logger.
            BackgroundHttpBurstLoggerProvider.DelayMs = 0;
        }

        MainThread.BeginInvokeOnMainThread(() =>
            {
                _summaryLabel.Text = summary;
                _firstFailureLabel.Text = firstFailure;
            }
        );
    }

    private enum Outcome
    {
        Ok,
        Staging,
        Processing,
        Error,
        Canceled
    }

    private readonly record struct Result(Outcome Outcome, long ElapsedMs, string Detail);

    private static async Task<Result> ExecuteAsync(Uri uri, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await BackgroundHttpClientFactory.Client.SendAsync(request, cancellationToken);

            // Read to completion: the response stream IS the staged file, and disposing it is
            // what acknowledges the request to the handler.
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            return new Result(Outcome.Ok, stopwatch.ElapsedMilliseconds, FormattableString.Invariant($"len={bytes.Length}"));
        }
        catch (OperationCanceledException)
        {
            return new Result(Outcome.Canceled, stopwatch.ElapsedMilliseconds, string.Empty);
        }
        catch (Exception ex)
        {
            var flattened = Flatten(ex);

            var outcome = flattened.Contains(StagingMarker, StringComparison.Ordinal)
                ? Outcome.Staging
                : flattened.Contains(ProcessingMarker, StringComparison.Ordinal)
                    ? Outcome.Processing
                    : Outcome.Error;

            return new Result(outcome, stopwatch.ElapsedMilliseconds, flattened);
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

        return message.Length <= 300 ? message : message[..300];
    }
}
#endif
