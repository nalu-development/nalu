using System.Diagnostics;
using Microsoft.Maui.DevFlow.Driver;
using Xunit;

namespace Nalu.Maui.UITests.Infrastructure;

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
/// running the tests. For Android emulators run <c>adb reverse tcp:9223 tcp:9223</c> first.
/// Host/port can be overridden with the DEVFLOW_HOST / DEVFLOW_PORT environment variables.
/// </para>
/// </remarks>
public sealed class NaluApp : IAsyncLifetime
{
    private static readonly TimeSpan _defaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _pollInterval = TimeSpan.FromMilliseconds(250);

    private readonly AgentClient _client;

    public NaluApp()
    {
        var host = Environment.GetEnvironmentVariable("DEVFLOW_HOST") ?? "localhost";
        var port = int.TryParse(Environment.GetEnvironmentVariable("DEVFLOW_PORT"), out var p) ? p : 9223;
        _client = new AgentClient(host, port);
    }

    public async ValueTask InitializeAsync()
    {
        var stopwatch = Stopwatch.StartNew();
        var timeout = TimeSpan.FromSeconds(30);

        while (true)
        {
            try
            {
                var status = await _client.GetStatusAsync().ConfigureAwait(false);

                if (status is not null)
                {
                    return;
                }
            }
            catch (Exception) when (stopwatch.Elapsed < timeout)
            {
                // Agent not reachable yet: keep polling.
            }

            if (stopwatch.Elapsed >= timeout)
            {
                throw new InvalidOperationException(
                    $"Cannot reach the DevFlow agent at {_client.BaseUrl}. " +
                    "Make sure Nalu.Maui.TestApp is running in DEBUG on the target platform. " +
                    "For Android emulators run 'adb reverse tcp:9223 tcp:9223' first. " +
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

    /// <summary>Reads a property of the underlying MAUI element (e.g. "Text", "IsVisible").</summary>
    public async Task<string?> GetPropertyAsync(string automationId, string propertyName, TimeSpan? timeout = null)
    {
        var element = await WaitForElementAsync(automationId, timeout).ConfigureAwait(false);

        return await _client.GetPropertyAsync(element.Id, propertyName).ConfigureAwait(false);
    }

    /// <summary>Scrolls (main scrollable when no AutomationId is provided).</summary>
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

    /// <summary>Captures a PNG screenshot (useful when diagnosing failing tests).</summary>
    public Task<byte[]?> ScreenshotAsync() => _client.ScreenshotAsync();

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
