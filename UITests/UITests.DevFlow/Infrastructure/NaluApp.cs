using System.Diagnostics;
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
/// running the tests. For Android emulators run <c>adb forward tcp:9223 tcp:9223</c> first.
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
    /// DEVFLOW_PORT when set; otherwise 9223 plus 10223 — the port the agent falls back to
    /// when the app is relaunched while 9223 lingers in TIME_WAIT.
    /// </summary>
    private static int[] CandidatePorts()
        => int.TryParse(Environment.GetEnvironmentVariable("DEVFLOW_PORT"), out var p) ? [p] : [9223, 10223];

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
                    "For Android emulators run 'adb forward tcp:9223 tcp:9223' first. " +
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

        while (true)
        {
            var matches = await _client.QueryAsync(text: text).ConfigureAwait(false);
            var element = matches.FirstOrDefault(m => m.IsVisible);

            if (element is not null)
            {
                if (await _client.TapAsync(element.Id).ConfigureAwait(false))
                {
                    return;
                }

                // The matched element is not tappable itself: hit-test its center to get the
                // ancestor stack (innermost first) and tap the first ancestor that accepts it.
                // (The single-element endpoint drops ParentId, so we cannot walk the tree.)
                if (element.WindowBounds is { } wb)
                {
                    var hitTestJson = await _client.HitTestAsync(wb.X + (wb.Width / 2), wb.Y + (wb.Height / 2)).ConfigureAwait(false);
                    using var hitTest = JsonDocument.Parse(hitTestJson);

                    foreach (var ancestor in hitTest.RootElement.GetProperty("elements").EnumerateArray().Take(5))
                    {
                        var ancestorId = ancestor.GetProperty("id").GetString();

                        if (ancestorId is not null && ancestorId != element.Id && await _client.TapAsync(ancestorId).ConfigureAwait(false))
                        {
                            return;
                        }
                    }
                }

                throw new InvalidOperationException($"Tap on element with text '{text}' (element {element.Id}) and its ancestors failed.");
            }

            if (stopwatch.Elapsed >= effectiveTimeout)
            {
                throw new TimeoutException($"Element with text '{text}' did not appear within {effectiveTimeout.TotalSeconds:0.#}s.");
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
    public Task BackAsync() => _client.BackAsync();

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
