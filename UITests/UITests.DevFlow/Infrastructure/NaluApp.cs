using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.DevFlow.Driver;
using SkiaSharp;
using Xunit;

namespace Nalu.Maui.UITests.Infrastructure;

/// <summary>
/// Window-space bounds of an element, in device-independent units.
/// </summary>
/// <remarks>
/// Driver-agnostic equivalent of the DevFlow <c>BoundsInfo</c> so geometry-based tests
/// don't take a dependency on the experimental Driver API surface.
/// </remarks>
public sealed record ElementBounds(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public double CenterX => X + (Width / 2);
    public double CenterY => Y + (Height / 2);

    public override string ToString() => $"(X={X:0.##}, Y={Y:0.##}, W={Width:0.##}, H={Height:0.##})";
}

/// <summary>
/// Thin wrapper around the DevFlow <see cref="AgentClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is the ONLY place allowed to talk to the DevFlow Driver API directly:
/// the Driver is an experimental preview and its API surface will change between releases,
/// so tests must depend on this wrapper instead of on <see cref="AgentClient"/>.
/// </para>
/// <para>
/// The wrapper connects to the DevFlow agent hosted inside the running Nalu.Maui.TestApp
/// (see <c>MauiProgram.AddMauiDevFlowAgent</c>). Start the app on the target platform before
/// running the tests: the agent listens on a PER-PLATFORM port — Android 9223 (run
/// <c>adb forward tcp:9223 tcp:9223</c> first), iOS simulator 9224, Mac Catalyst 9225 — so
/// apps on different platforms can run simultaneously.
/// Host/port can be overridden with the DEVFLOW_HOST / DEVFLOW_PORT environment variables.
/// </para>
/// </remarks>
public sealed class NaluApp : IAsyncLifetime
{
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(250);

    private AgentClient _client;

    public NaluApp()
    {
        var host = Environment.GetEnvironmentVariable("DEVFLOW_HOST") ?? "localhost";
        _client = new AgentClient(host, CandidatePorts()[0]);
    }

    /// <summary>
    /// DEVFLOW_PORT when set; otherwise the per-platform agent ports — Android 9223 (via
    /// adb forward), iOS simulator 9224, Mac Catalyst 9225 — each followed by its +1000
    /// broker fallback (used when the app is relaunched while the port lingers in TIME_WAIT).
    /// With apps on SEVERAL platforms running at once, discovery hits the first that answers:
    /// set DEVFLOW_PORT to target a specific one.
    /// </summary>
    private static int[] CandidatePorts()
        => int.TryParse(Environment.GetEnvironmentVariable("DEVFLOW_PORT"), out var p) ? [p] : [9223, 9224, 9225, 10223, 10224, 10225];

    public async ValueTask InitializeAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(30);
        var host = Environment.GetEnvironmentVariable("DEVFLOW_HOST") ?? "localhost";
        var candidatePorts = CandidatePorts();

        while (true)
        {
            foreach (var port in candidatePorts)
            {
                var client = _client.BaseUrl.EndsWith($":{port}", StringComparison.Ordinal) ? _client : new AgentClient(host, port);

                try
                {
                    var status = await client.GetStatusAsync().ConfigureAwait(false);

                    if (status is not null)
                    {
                        if (!ReferenceEquals(client, _client))
                        {
                            _client.Dispose();
                            _client = client;
                        }

                        return;
                    }
                }
                catch (Exception) when (stopwatch.Elapsed < timeout)
                {
                    // Agent not reachable on this port yet: keep polling.
                }

                if (!ReferenceEquals(client, _client))
                {
                    client.Dispose();
                }
            }

            if (stopwatch.Elapsed >= timeout)
            {
                throw new InvalidOperationException(
                    $"Cannot reach the DevFlow agent at {host} on port(s) {string.Join(", ", candidatePorts)}. " +
                    "Make sure Nalu.Maui.TestApp is running in DEBUG on the target platform. " +
                    "Per-platform ports: Android 9223 (run 'adb forward tcp:9223 tcp:9223' first), " +
                    "iOS simulator 9224, Mac Catalyst 9225. " +
                    "Host/port can be overridden with DEVFLOW_HOST / DEVFLOW_PORT.");
            }

            await Task.Delay(_pollInterval).ConfigureAwait(false);
        }
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();

        return ValueTask.CompletedTask;
    }

    private string? _platform;

    /// <summary>Gets the running app's platform name (e.g. "iOS", "Android"), cached per run.</summary>
    public async Task<string> GetPlatformAsync()
    {
        if (_platform is null)
        {
            var status = await _client.GetStatusAsync().ConfigureAwait(false);
            _platform = status?.Platform ?? "unknown";
        }

        return _platform;
    }

    /// <summary>True when the app runs on iOS or Mac Catalyst.</summary>
    public async Task<bool> IsAppleAsync()
    {
        var platform = await GetPlatformAsync().ConfigureAwait(false);

        return platform.Contains("ios", StringComparison.OrdinalIgnoreCase)
               || platform.Contains("catalyst", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Finds a single element by AutomationId, or null when not present.</summary>
    public async Task<ElementInfo?> FindElementAsync(string automationId)
    {
        var matches = await _client.QueryAsync(automationId: automationId).ConfigureAwait(false);

        return matches.FirstOrDefault();
    }

    /// <summary>Waits until an element with the given AutomationId appears in the visual tree.</summary>
    public async Task<ElementInfo> WaitForElementAsync(string automationId, TimeSpan? timeout = null)
    {
        var element = await WaitForElementOrDefaultAsync(automationId, timeout).ConfigureAwait(false);

        if (element is null)
        {
            var knownIds = await GetKnownAutomationIdsAsync().ConfigureAwait(false);

            throw new TimeoutException(
                $"Element '{automationId}' did not appear within {(timeout ?? _defaultTimeout).TotalSeconds:0.#}s. " +
                $"AutomationIds currently in the visual tree: [{string.Join(", ", knownIds)}]");
        }

        return element;
    }

    /// <summary>Waits for an element to appear, returning null on timeout instead of throwing.</summary>
    public async Task<ElementInfo?> WaitForElementOrDefaultAsync(string automationId, TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var effectiveTimeout = timeout ?? _defaultTimeout;

        while (true)
        {
            var element = await FindElementAsync(automationId).ConfigureAwait(false);

            if (element is not null)
            {
                return element;
            }

            if (stopwatch.Elapsed >= effectiveTimeout)
            {
                return null;
            }

            await Task.Delay(_pollInterval).ConfigureAwait(false);
        }
    }

    /// <summary>Taps the first VISIBLE element whose text matches (e.g. NaluTabBar tab labels).</summary>
    /// <remarks>
    /// Text queries also match abstract Shell elements (e.g. <c>Tab</c> nodes, reported
    /// invisible and unbounded); DevFlow "taps" those by setting <c>Shell.CurrentItem</c>
    /// directly, bypassing the OnNavigating pipeline. Preferring visible matches makes sure
    /// we hit the real on-screen view instead. When the matched element itself is not
    /// tappable (e.g. a Label whose TapGestureRecognizer lives on an ancestor Border, as in
    /// NaluTabBar), the tap is retried on its ancestors.
    /// </remarks>
    public async Task TapByTextAsync(string text, TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var effectiveTimeout = timeout ?? _defaultTimeout;

        var found = false;

        while (true)
        {
            var matches = await _client.QueryAsync(text: text).ConfigureAwait(false);

            // Only on-screen matches: text queries also hit abstract structure elements
            // (Shell's Tab, the Scaffold's ScaffoldRoot Title) that report IsVisible but have
            // no window bounds — tapping those spins until timeout.
            var element = matches.FirstOrDefault(m => m.IsVisible && m.WindowBounds is { Width: > 0, Height: > 0 });

            if (element is not null)
            {
                found = true;

                if (await _client.TapAsync(element.Id).ConfigureAwait(false))
                {
                    return;
                }

                // The matched element is not tappable itself: hit-test its center to get the
                // ancestor stack (innermost first) and tap the first ancestor that accepts it.
                // (The single-element endpoint drops ParentId, so we cannot walk the tree.)
                if (element.WindowBounds is { } wb)
                {
                    // Integral coordinates only: fractional values get culture-mangled somewhere
                    // in the transport (it-IT decimal comma → "249,3" reparsed as 2493 → empty
                    // off-screen hit test). Element centers don't need sub-dp precision anyway.
                    var hitTestJson = await _client.HitTestAsync(
                        Math.Round(wb.X + (wb.Width / 2)),
                        Math.Round(wb.Y + (wb.Height / 2))
                    ).ConfigureAwait(false);
                    using var hitTest = JsonDocument.Parse(hitTestJson);

                    // The hit-test stack is innermost-first but may interleave SIBLING branches
                    // (page content under floating chrome comes before the chrome's own stack):
                    // the matched element's ancestors are the entries AFTER it — start there,
                    // not at the top of the list.
                    var stack = hitTest.RootElement.GetProperty("elements").EnumerateArray().ToList();
                    var selfIndex = stack.FindIndex(e => e.GetProperty("id").GetString() == element.Id);

                    foreach (var ancestor in stack.Skip(selfIndex + 1).Take(5))
                    {
                        var ancestorId = ancestor.GetProperty("id").GetString();

                        if (ancestorId is not null && ancestorId != element.Id && await _client.TapAsync(ancestorId).ConfigureAwait(false))
                        {
                            return;
                        }
                    }
                }

                // A failed tap can mean the click handler THREW (the agent maps handler
                // exceptions to a generic tap failure) — e.g. a tab tap navigating while the
                // previous Shell animation is still committing. Transient: retry until timeout.
            }

            if (stopwatch.Elapsed >= effectiveTimeout)
            {
                throw found
                    ? new InvalidOperationException($"Tap on element with text '{text}' (and its ancestors) kept failing for {effectiveTimeout.TotalSeconds:0.#}s.")
                    : new TimeoutException($"Element with text '{text}' did not appear within {effectiveTimeout.TotalSeconds:0.#}s.");
            }

            await Task.Delay(_pollInterval).ConfigureAwait(false);
        }
    }

    /// <summary>Waits for the element and taps it.</summary>
    public async Task TapAsync(string automationId, TimeSpan? timeout = null)
    {
        var element = await WaitForElementAsync(automationId, timeout).ConfigureAwait(false);

        if (!await _client.TapAsync(element.Id).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Tap on '{automationId}' (element {element.Id}) failed.");
        }
    }

    /// <summary>Programmatically focuses an element (raises the soft keyboard for inputs).</summary>
    public async Task FocusAsync(string automationId, TimeSpan? timeout = null)
    {
        var element = await WaitForElementAsync(automationId, timeout).ConfigureAwait(false);

        if (!await _client.FocusAsync(element.Id).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Focus on '{automationId}' (element {element.Id}) failed.");
        }
    }

    #region Android soft keyboard (host-side adb)

    private double? _androidDisplayScale;

    private static async Task<string> RunAdbAsync(string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo("adb", arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        }) ?? throw new InvalidOperationException("Failed to start adb.");

        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        return output;
    }

    /// <summary>Whether the Android soft keyboard is currently visible (host-side adb dumpsys).</summary>
    public async Task<bool> IsAndroidSoftKeyboardVisibleAsync()
        => (await RunAdbAsync("shell dumpsys input_method").ConfigureAwait(false))
            .Contains("mInputShown=true", StringComparison.Ordinal);

    /// <summary>Polls until the Android soft keyboard reaches the expected visibility.</summary>
    public async Task WaitForAndroidSoftKeyboardAsync(bool visible, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? _defaultTimeout);

        while (DateTime.UtcNow < deadline)
        {
            if (await IsAndroidSoftKeyboardVisibleAsync().ConfigureAwait(false) == visible)
            {
                return;
            }

            await Task.Delay(_pollInterval).ConfigureAwait(false);
        }

        throw new TimeoutException($"Android soft keyboard did not become {(visible ? "visible" : "hidden")} within {timeout ?? _defaultTimeout}.");
    }

    /// <summary>
    /// A REAL input tap at the element's center (adb <c>input tap</c>): required for behaviors
    /// listening to raw window touches (e.g. <c>Page.HideSoftInputOnTapped</c>) — agent taps
    /// invoke handlers programmatically and never travel the platform input pipeline.
    /// </summary>
    public async Task AndroidRealTapAsync(string automationId)
    {
        var bounds = await GetBoundsAsync(automationId).ConfigureAwait(false);
        var scale = await GetAndroidDisplayScaleAsync().ConfigureAwait(false);

        await RunAdbAsync($"shell input tap {(int)(bounds.CenterX * scale)} {(int)(bounds.CenterY * scale)}").ConfigureAwait(false);
    }

    /// <summary>
    /// A REAL directional swipe across an element's center (adb <c>input swipe</c>): drives
    /// platform gesture recognizers (e.g. MAUI pan) with actual touch physics, which the
    /// agent's synthetic gestures lack.
    /// </summary>
    public async Task AndroidRealSwipeAsync(string automationId, double deltaXDp, double deltaYDp, int durationMs = 200)
    {
        var bounds = await GetBoundsAsync(automationId).ConfigureAwait(false);
        await AndroidRealSwipeAtPointAsync(bounds.CenterX, bounds.CenterY, deltaXDp, deltaYDp, durationMs).ConfigureAwait(false);
    }

    /// <summary>
    /// A REAL directional swipe from an explicit window point (dp): for elements whose CENTER
    /// sits offscreen (e.g. a tall scrollable inside a collapsed bottom sheet), anchor the
    /// gesture on a visible landmark instead.
    /// </summary>
    public async Task AndroidRealSwipeAtPointAsync(double startXDp, double startYDp, double deltaXDp, double deltaYDp, int durationMs = 200)
    {
        var scale = await GetAndroidDisplayScaleAsync().ConfigureAwait(false);

        var x1 = (int)Math.Round(startXDp * scale);
        var y1 = (int)Math.Round(startYDp * scale);
        var x2 = (int)Math.Round((startXDp + deltaXDp) * scale);
        var y2 = (int)Math.Round((startYDp + deltaYDp) * scale);

        await RunAdbAsync($"shell input swipe {x1} {y1} {x2} {y2} {durationMs}").ConfigureAwait(false);
    }

    private async Task<double> GetAndroidDisplayScaleAsync()
    {
        if (_androidDisplayScale is not { } scale)
        {
            // "Physical density: 480" (an "Override density" line, when present, is the
            // effective one and comes last) → dp scale = density / 160.
            var output = await RunAdbAsync("shell wm density").ConfigureAwait(false);
            var densityLine = output.Split('\n').Last(line => line.Contains("density:", StringComparison.OrdinalIgnoreCase));
            scale = int.Parse(densityLine.Split(':')[^1].Trim(), System.Globalization.CultureInfo.InvariantCulture) / 160.0;
            _androidDisplayScale = scale;
        }

        return scale;
    }

    /// <summary>
    /// A REAL long-press drag between two elements' centers, driven by discrete
    /// <c>input motionevent</c> steps in a single adb shell invocation — the only way to
    /// trigger ItemTouchHelper drag&amp;drop: synthetic agent gestures have no touch physics
    /// (no held long-press before the move), and the per-command injection latency provides
    /// the natural drag pacing.
    /// </summary>
    public async Task AndroidLongPressDragAsync(string fromAutomationId, string toAutomationId, int moveSteps = 8)
    {
        var from = await GetBoundsAsync(fromAutomationId).ConfigureAwait(false);
        var to = await GetBoundsAsync(toAutomationId).ConfigureAwait(false);
        var scale = await GetAndroidDisplayScaleAsync().ConfigureAwait(false);

        // Integral device pixels only (the transport decimal-comma-mangles fractions on it-IT hosts).
        var fromX = (int)Math.Round(from.CenterX * scale);
        var fromY = (int)Math.Round(from.CenterY * scale);
        var toX = (int)Math.Round(to.CenterX * scale);
        var toY = (int)Math.Round(to.CenterY * scale);

        var script = new System.Text.StringBuilder($"shell input motionevent DOWN {fromX} {fromY} ; sleep 0.8");

        for (var step = 1; step <= moveSteps; step++)
        {
            var x = fromX + ((toX - fromX) * step / moveSteps);
            var y = fromY + ((toY - fromY) * step / moveSteps);
            script.Append($" ; input motionevent MOVE {x} {y}");
        }

        // Settle at the destination before releasing so ItemTouchHelper commits the last move.
        script.Append($" ; sleep 0.3 ; input motionevent UP {toX} {toY}");

        await RunAdbAsync(script.ToString()).ConfigureAwait(false);
    }

    #endregion

    /// <summary>Waits for the (input) element and replaces its text.</summary>
    public async Task FillAsync(string automationId, string text, TimeSpan? timeout = null)
    {
        var element = await WaitForElementAsync(automationId, timeout).ConfigureAwait(false);

        if (!await _client.FillAsync(element.Id, text).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Fill on '{automationId}' (element {element.Id}) failed.");
        }
    }

    /// <summary>Waits until no element with the given AutomationId is present in the visual tree.</summary>
    public async Task WaitForElementGoneAsync(string automationId, TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var effectiveTimeout = timeout ?? _defaultTimeout;

        while (true)
        {
            if (await FindElementAsync(automationId).ConfigureAwait(false) is null)
            {
                return;
            }

            if (stopwatch.Elapsed >= effectiveTimeout)
            {
                throw new TimeoutException($"Element '{automationId}' was still present after {effectiveTimeout.TotalSeconds:0.#}s.");
            }

            await Task.Delay(_pollInterval).ConfigureAwait(false);
        }
    }

    /// <summary>Waits until the element's text equals the expected value.</summary>
    public async Task WaitForTextAsync(string automationId, string expectedText, TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var effectiveTimeout = timeout ?? _defaultTimeout;

        while (true)
        {
            var element = await FindElementAsync(automationId).ConfigureAwait(false);

            if (element?.Text == expectedText)
            {
                return;
            }

            if (stopwatch.Elapsed >= effectiveTimeout)
            {
                throw new TimeoutException(
                    $"Element '{automationId}' text did not become '{expectedText}' within {effectiveTimeout.TotalSeconds:0.#}s. " +
                    $"Last value: '{element?.Text ?? "<element not found>"}'");
            }

            await Task.Delay(_pollInterval).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits until the element's text stops changing (two identical consecutive reads),
    /// e.g. for scroll-event counters to settle before asserting on them.
    /// </summary>
    public async Task<string?> WaitForStableTextAsync(string automationId, TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var effectiveTimeout = timeout ?? _defaultTimeout;
        var previous = (await FindElementAsync(automationId).ConfigureAwait(false))?.Text;

        while (true)
        {
            await Task.Delay(_pollInterval).ConfigureAwait(false);
            var current = (await FindElementAsync(automationId).ConfigureAwait(false))?.Text;

            if (current is not null && current == previous)
            {
                return current;
            }

            if (stopwatch.Elapsed >= effectiveTimeout)
            {
                throw new TimeoutException(
                    $"Text of '{automationId}' did not stabilize within {effectiveTimeout.TotalSeconds:0.#}s. Last value: '{current}'");
            }

            previous = current;
        }
    }

    /// <summary>Waits until the element's text satisfies the given predicate.</summary>
    public async Task<string?> WaitForTextMatchAsync(string automationId, Func<string?, bool> predicate, TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var effectiveTimeout = timeout ?? _defaultTimeout;
        string? lastText = null;

        while (true)
        {
            var element = await FindElementAsync(automationId).ConfigureAwait(false);
            lastText = element?.Text ?? lastText;

            if (element is not null && predicate(element.Text))
            {
                return element.Text;
            }

            if (stopwatch.Elapsed >= effectiveTimeout)
            {
                throw new TimeoutException(
                    $"Element '{automationId}' text did not satisfy the predicate within {effectiveTimeout.TotalSeconds:0.#}s. " +
                    $"Last value: '{lastText ?? "<element not found>"}'");
            }

            await Task.Delay(_pollInterval).ConfigureAwait(false);
        }
    }

    /// <summary>Reads a property of the underlying MAUI element (e.g. "Text", "IsVisible").</summary>
    public async Task<string?> GetPropertyAsync(string automationId, string propertyName, TimeSpan? timeout = null)
    {
        var element = await WaitForElementAsync(automationId, timeout).ConfigureAwait(false);

        return await _client.GetPropertyAsync(element.Id, propertyName).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads a NUMERIC property of the underlying MAUI element (e.g. a ScrollView's "ScrollY").
    /// </summary>
    /// <remarks>
    /// The transport formats doubles with the HOST culture ("284,6666" on it-IT), so the raw
    /// string cannot be parsed invariantly: the decimal separator is normalized first.
    /// </remarks>
    public async Task<double> GetDoublePropertyAsync(string automationId, string propertyName, TimeSpan? timeout = null)
    {
        var raw = await GetPropertyAsync(automationId, propertyName, timeout).ConfigureAwait(false);

        if (raw is null)
        {
            throw new InvalidOperationException($"Property '{propertyName}' of '{automationId}' returned no value.");
        }

        return double.Parse(raw.Replace(',', '.'), CultureInfo.InvariantCulture);
    }

    /// <summary>Scrolls (main scrollable when no AutomationId is provided).</summary>
    /// <remarks>
    /// Does NOT work on <c>VirtualScroll</c>: its platform root view is a container, not the
    /// native scroll view, and the DevFlow delta-scroll silently no-ops on it.
    /// Use <see cref="SwipeAsync"/> (or a page-side ScrollTo control) instead.
    /// </remarks>
    public async Task ScrollAsync(string? automationId = null, double deltaX = 0, double deltaY = 0)
    {
        string? elementId = null;

        if (automationId is not null)
        {
            var element = await WaitForElementAsync(automationId).ConfigureAwait(false);
            elementId = element.Id;
        }

        await _client.ScrollAsync(elementId, deltaX, deltaY).ConfigureAwait(false);
    }

    /// <summary>
    /// Performs a swipe gesture on the element ("up"/"down"/"left"/"right").
    /// This is the way to scroll a <c>VirtualScroll</c> by a delta (see <see cref="ScrollAsync"/> remarks).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Direction semantics (verified on iOS DevFlow preview.12): vertically, "up" scrolls FORWARD
    /// (reveals content below); horizontally, "right" scrolls FORWARD (reveals content to the right).
    /// </para>
    /// <para>
    /// Synthetic swipes move the scroll position and raise Scrolled events, but do NOT emulate
    /// real touch physics: they cannot trigger pull-to-refresh, carousel paging snap, or
    /// dragging started/ended platform callbacks.
    /// </para>
    /// </remarks>
    public async Task SwipeAsync(string automationId, string direction, double? distance = null, int? durationMs = null)
    {
        var element = await WaitForElementAsync(automationId).ConfigureAwait(false);

        if (!await _client.GestureAsync("swipe", element.Id, direction, distance, durationMs).ConfigureAwait(false))
        {
            throw new InvalidOperationException($"Swipe {direction} on '{automationId}' (element {element.Id}) failed.");
        }
    }

    /// <summary>Captures a PNG screenshot (useful when diagnosing failing tests).</summary>
    public Task<byte[]?> ScreenshotAsync() => _client.ScreenshotAsync();

    /// <summary>
    /// Samples several points of ONE screenshot, addressed as fractions (0..1) of the window —
    /// the only way to observe a page while it MOVES: the visual tree reports layout geometry,
    /// which a platform transform (a sliding page) never changes.
    /// One capture per call on purpose: points sampled from the same frame are comparable, points
    /// sampled from separate captures are not (the animation advances between them).
    /// </summary>
    public async Task<IReadOnlyList<(byte R, byte G, byte B)>> SampleWindowPixelsAsync(params (double X, double Y)[] points)
    {
        var png = await _client.ScreenshotAsync().ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Screenshot capture failed.");

        using var bitmap = SKBitmap.Decode(png)
                           ?? throw new InvalidOperationException("Could not decode the screenshot PNG.");

        var samples = new (byte R, byte G, byte B)[points.Length];

        for (var i = 0; i < points.Length; i++)
        {
            var pixelX = Math.Clamp((int) Math.Round(points[i].X * (bitmap.Width - 1)), 0, bitmap.Width - 1);
            var pixelY = Math.Clamp((int) Math.Round(points[i].Y * (bitmap.Height - 1)), 0, bitmap.Height - 1);
            var color = bitmap.GetPixel(pixelX, pixelY);
            samples[i] = (color.Red, color.Green, color.Blue);
        }

        return samples;
    }

    /// <summary>
    /// Waits until the element is DISPLAYED and still is once a page transition would have
    /// finished — the honest question to ask after a navigation tap.
    /// A page that is LEAVING stays on screen for the whole of its motion (both platforms hold it
    /// there; Android used to tear it down at commit, which is what made a single sample look
    /// reliable), so "is the target displayed?" asked immediately can be answered by the page
    /// being navigated AWAY from — and a navigation that was silently dropped for arriving mid
    /// transition then reads as successful.
    /// </summary>
    public async Task WaitForSettledDisplayAsync(string automationId, TimeSpan? timeout = null)
    {
        await WaitForBoundsAsync(automationId, b => b.Y > 0, timeout).ConfigureAwait(false);

        // Longer than any stock transition: what is still displayed after this is presented.
        await Task.Delay(400).ConfigureAwait(false);

        await WaitForBoundsAsync(automationId, b => b.Y > 0, TimeSpan.FromSeconds(2)).ConfigureAwait(false);
    }

    /// <summary>Gets the window-space bounds of an element (device-independent units).</summary>
    public async Task<ElementBounds> GetBoundsAsync(string automationId, TimeSpan? timeout = null)
    {
        var element = await WaitForElementAsync(automationId, timeout).ConfigureAwait(false);

        // Query results may carry stale/partial geometry: fetch the detailed element info.
        var detail = await _client.GetElementAsync(element.Id).ConfigureAwait(false) ?? element;
        var bounds = detail.WindowBounds ?? detail.Bounds;

        if (bounds is null)
        {
            throw new InvalidOperationException($"Element '{automationId}' has no bounds information.");
        }

        return new ElementBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    }

    /// <summary>Waits until the element's bounds satisfy the given predicate.</summary>
    public async Task<ElementBounds> WaitForBoundsAsync(
        string automationId,
        Func<ElementBounds, bool> predicate,
        TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var effectiveTimeout = timeout ?? _defaultTimeout;

        while (true)
        {
            var bounds = await GetBoundsAsync(automationId).ConfigureAwait(false);

            if (predicate(bounds))
            {
                return bounds;
            }

            if (stopwatch.Elapsed >= effectiveTimeout)
            {
                throw new TimeoutException(
                    $"Bounds of '{automationId}' did not satisfy the predicate within {effectiveTimeout.TotalSeconds:0.#}s. Last bounds: {bounds}");
            }

            await Task.Delay(_pollInterval).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits until the element's bounds stop changing (two identical consecutive reads),
    /// e.g. for size/position animations to settle.
    /// </summary>
    public async Task<ElementBounds> WaitForStableBoundsAsync(string automationId, TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var effectiveTimeout = timeout ?? _defaultTimeout;
        var previous = await GetBoundsAsync(automationId).ConfigureAwait(false);

        while (true)
        {
            await Task.Delay(_pollInterval).ConfigureAwait(false);
            var current = await GetBoundsAsync(automationId).ConfigureAwait(false);

            if (current == previous)
            {
                return current;
            }

            if (stopwatch.Elapsed >= effectiveTimeout)
            {
                throw new TimeoutException(
                    $"Bounds of '{automationId}' did not stabilize within {effectiveTimeout.TotalSeconds:0.#}s. Last bounds: {current}");
            }

            previous = current;
        }
    }

    /// <summary>
    /// Samples the pixel color at element-relative coordinates (device-independent units)
    /// from a FULL screenshot of the app window.
    /// </summary>
    /// <remarks>
    /// Element-scoped screenshots re-draw the view offscreen and can miss composited visual
    /// effects (verified: Android fading edges only render in the real frame), so sampling
    /// happens on the full capture using the element's window-space bounds.
    /// </remarks>
    public async Task<(byte R, byte G, byte B)> GetPixelColorAsync(string automationId, double x, double y)
    {
        var bounds = await GetBoundsAsync(automationId).ConfigureAwait(false);

        var png = await _client.ScreenshotAsync().ConfigureAwait(false)
                  ?? throw new InvalidOperationException("Screenshot capture failed.");

        using var bitmap = SKBitmap.Decode(png)
                           ?? throw new InvalidOperationException("Could not decode the screenshot PNG.");

        // Screenshots may be scaled: derive the factor from the window root width.
        var windowWidth = await GetWindowWidthAsync().ConfigureAwait(false);
        var scale = bitmap.Width / windowWidth;
        var pixelX = Math.Clamp((int) Math.Round((bounds.X + x) * scale), 0, bitmap.Width - 1);
        var pixelY = Math.Clamp((int) Math.Round((bounds.Y + y) * scale), 0, bitmap.Height - 1);
        var color = bitmap.GetPixel(pixelX, pixelY);

        return (color.Red, color.Green, color.Blue);
    }

    private async Task<double> GetWindowWidthAsync()
    {
        // The tree root may carry no bounds (observed on Android): use the first bounded element.
        var tree = await _client.GetTreeAsync(3).ConfigureAwait(false);

        static double? FindWidth(IEnumerable<ElementInfo> elements)
        {
            foreach (var element in elements)
            {
                var bounds = element.WindowBounds ?? element.Bounds;

                if (bounds is { Width: > 0 })
                {
                    return bounds.Width;
                }

                if (element.Children is { } children && FindWidth(children) is { } width)
                {
                    return width;
                }
            }

            return null;
        }

        return FindWidth(tree)
               ?? throw new InvalidOperationException("Could not determine the window width for pixel sampling.");
    }

    /// <summary>Window size in DIPs, derived from the first fully bounded element in the tree
    /// (the tree root may carry no bounds — same caveat as <see cref="GetWindowWidthAsync"/>).</summary>
    public async Task<(double Width, double Height)> GetWindowSizeAsync()
    {
        var tree = await _client.GetTreeAsync(3).ConfigureAwait(false);

        static (double Width, double Height)? FindSize(IEnumerable<ElementInfo> elements)
        {
            foreach (var element in elements)
            {
                var bounds = element.WindowBounds ?? element.Bounds;

                if (bounds is { Width: > 0, Height: > 0 })
                {
                    return (bounds.Width, bounds.Height);
                }

                if (element.Children is { } children && FindSize(children) is { } size)
                {
                    return size;
                }
            }

            return null;
        }

        return FindSize(tree)
               ?? throw new InvalidOperationException("Could not determine the window size.");
    }

    /// <summary>Waits until the sampled pixel color satisfies the given predicate (e.g. after a re-render).</summary>
    public async Task<(byte R, byte G, byte B)> WaitForPixelColorAsync(
        string automationId,
        double x,
        double y,
        Func<(byte R, byte G, byte B), bool> predicate,
        TimeSpan? timeout = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var effectiveTimeout = timeout ?? _defaultTimeout;

        while (true)
        {
            var color = await GetPixelColorAsync(automationId, x, y).ConfigureAwait(false);

            if (predicate(color))
            {
                return color;
            }

            if (stopwatch.Elapsed >= effectiveTimeout)
            {
                throw new TimeoutException(
                    $"Pixel ({x:0.#},{y:0.#}) of '{automationId}' did not satisfy the predicate within " +
                    $"{effectiveTimeout.TotalSeconds:0.#}s. Last color: RGB({color.R},{color.G},{color.B})");
            }

            await Task.Delay(_pollInterval).ConfigureAwait(false);
        }
    }

    /// <summary>Navigates back (also closes the top-most modal page, e.g. a popup).</summary>
    /// <remarks>
    /// The agent's Back command only understands NavigationPage/Shell stacks: it refuses
    /// ("stack may be empty") on custom hosts like the Nalu Scaffold. Use
    /// <see cref="SystemBackAsync"/> to exercise the platform's real back channel.
    /// </remarks>
    public Task BackAsync() => _client.BackAsync();

    /// <summary>
    /// Presses the platform's system back button. On Android this injects a real key event via
    /// adb — exercising the OnBackPressedDispatcher exactly like a user would, host-agnostic.
    /// On other platforms it falls back to the agent's Back command.
    /// </summary>
    public async Task SystemBackAsync()
    {
        var platform = await GetPlatformAsync().ConfigureAwait(false);

        if (platform.Contains("android", StringComparison.OrdinalIgnoreCase))
        {
            var startInfo = new ProcessStartInfo("adb")
            {
                ArgumentList = { "shell", "input", "keyevent", "4" },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = Process.Start(startInfo)
                                ?? throw new InvalidOperationException("Could not start 'adb' to send the system back key.");

            await process.WaitForExitAsync().ConfigureAwait(false);

            return;
        }

        await _client.BackAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Whether the Android device navigates by gestures (navigation_mode 2) — the predictive
    /// back gesture only exists there. False on other platforms.
    /// </summary>
    public async Task<bool> IsAndroidGestureNavigationAsync()
    {
        var platform = await GetPlatformAsync().ConfigureAwait(false);

        if (!platform.Contains("android", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var mode = await RunAdbAsync("shell", "settings", "get", "secure", "navigation_mode").ConfigureAwait(false);

        return mode.Trim() == "2";
    }

    /// <summary>
    /// Performs a SLOW committed predictive-back edge scrub (Android, gesture navigation) via
    /// discrete adb motion events. A plain 'input swipe' commits as a canned fling (and can even
    /// dispatch a second back); the stepped scrub is what exercises the peek-mount path.
    /// </summary>
    public async Task PredictiveBackScrubAsync()
    {
        var size = await RunAdbAsync("shell", "wm", "size").ConfigureAwait(false);
        var dimensions = size[(size.IndexOf(':') + 1)..].Trim().Split('x');
        var width = int.Parse(dimensions[0], CultureInfo.InvariantCulture);
        var height = int.Parse(dimensions[1], CultureInfo.InvariantCulture);

        var y = height / 2;
        var stops = new[] { 0.04, 0.1, 0.18, 0.28, 0.38, 0.48, 0.58 };

        var script = $"input motionevent DOWN 5 {y}; "
                     + string.Join("; ", stops.Select(s => $"input motionevent MOVE {(int) (width * s)} {y}"))
                     + $"; input motionevent UP {(int) (width * 0.58)} {y}";

        await RunAdbAsync("shell", script).ConfigureAwait(false);
    }

    private static async Task<string> RunAdbAsync(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("adb")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("Could not start 'adb'.");

        var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        return output;
    }

    /// <summary>
    /// Brings the app back to the test-selection page.
    /// Uses the "ResetButton" overlay added by the TestApp to every test page.
    /// </summary>
    public async Task ResetAsync()
    {
        // Already on the main page?
        if (await FindElementAsync("TestName").ConfigureAwait(false) is not null)
        {
            return;
        }

        var resetButton = await WaitForElementOrDefaultAsync("ResetButton", TimeSpan.FromSeconds(2)).ConfigureAwait(false);

        if (resetButton is null)
        {
            // Shell-based test pages (e.g. "Navigation Tests") have no decorated ResetButton:
            // they expose an app-reset button with text "Exit" on every page instead.
            var exitButton = (await _client.QueryAsync(text: "Exit").ConfigureAwait(false)).FirstOrDefault(e => e.IsVisible);

            if (exitButton is not null)
            {
                await _client.TapAsync(exitButton.Id).ConfigureAwait(false);
                await WaitForElementAsync("TestName").ConfigureAwait(false);

                return;
            }

            // A modal page (e.g. a popup left open by a failed test) may be covering the test page.
            await _client.BackAsync().ConfigureAwait(false);
            resetButton = await WaitForElementOrDefaultAsync("ResetButton", TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }

        if (resetButton is not null)
        {
            await _client.TapAsync(resetButton.Id).ConfigureAwait(false);
        }

        await WaitForElementAsync("TestName").ConfigureAwait(false);
    }

    /// <summary>Resets the app and opens the test page registered with the given [TestPage] name.</summary>
    public async Task OpenTestPageAsync(string testPageName)
    {
        await ResetAsync().ConfigureAwait(false);
        await FillAsync("TestName", testPageName).ConfigureAwait(false);
        await TapAsync("RunTestButton").ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> GetKnownAutomationIdsAsync()
    {
        try
        {
            var tree = await _client.GetTreeAsync().ConfigureAwait(false);
            var ids = new List<string>();
            CollectAutomationIds(tree, ids);

            return ids.Distinct().Take(50).ToList();
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static void CollectAutomationIds(IEnumerable<ElementInfo> elements, List<string> ids)
    {
        foreach (var element in elements)
        {
            if (!string.IsNullOrEmpty(element.AutomationId))
            {
                ids.Add(element.AutomationId);
            }

            if (element.Children is { } children)
            {
                CollectAutomationIds(children, ids);
            }
        }
    }
}
