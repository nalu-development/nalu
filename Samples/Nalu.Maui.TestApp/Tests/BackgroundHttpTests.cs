#if IOS && !MACCATALYST
using System.Collections.ObjectModel;
using System.Net.Http.Json;
using JetBrains.Annotations;

namespace Nalu.Maui.TestApp.Tests;

/// <summary>
/// Collects responses of requests that survived the app process (delivered on relaunch
/// through <see cref="INSUrlBackgroundSessionLostMessageHandler"/>) so the manual test
/// page can display them.
/// </summary>
public sealed class BackgroundHttpLostMessageHandler : INSUrlBackgroundSessionLostMessageHandler
{
    public async Task HandleLostMessageAsync(NSUrlBackgroundResponseHandle responseHandle)
    {
        try
        {
            using var response = await responseHandle.GetResponseAsync();
            var content = await response.Content.ReadAsStringAsync();
            BackgroundHttpTests.AppendResult($"LOST {responseHandle.RequestIdentifier} [{(int) response.StatusCode}] {BackgroundHttpTests.Truncate(content)}");
        }
        catch (Exception ex)
        {
            BackgroundHttpTests.AppendResult($"LOST {responseHandle.RequestIdentifier} FAILED: {ex.Message}");
        }
    }
}

/// <summary>
/// MANUAL test page (not driven by automated UI tests) to exercise
/// <c>NSUrlBackgroundSessionHttpMessageHandler</c> / <c>MessageHandlerNSUrlSessionDownloadDelegate</c>
/// on a real iPhone: buffered/stream/multipart bodies, parallelism, cancellation, and
/// background/kill scenarios (background the app or crash it while requests are in flight;
/// lost responses show up on relaunch via the lost-message handler).
/// </summary>
[UsedImplicitly]
[TestPage("Background Http Tests")]
public class BackgroundHttpTests : ContentPage
{
    // Newest-first log shared with the lost-message handler (survives page re-opens).
    private static readonly ObservableCollection<string> _results = [];

    // Lazy: NSUrlBackgroundSessionHttpMessageHandler's constructor THROWS on simulators
    // (background sessions unsupported there), and a throwing static field initializer
    // would fault the whole page type. On the simulator fall back to the default handler
    // so the page still works for layout/flow checks.
    private static readonly Lazy<HttpClient> _httpClient = new(() =>
        {
            var client = DeviceInfo.DeviceType == DeviceType.Virtual
                ? new HttpClient()
                : new HttpClient(new NSUrlBackgroundSessionHttpMessageHandler());

            // https://dummyjson.com/docs#intro-test — ?delay= is capped at 5000ms
            client.BaseAddress = new Uri("https://dummyjson.com/");
            client.Timeout = TimeSpan.FromMinutes(2);

            return client;
        }
    );

    private static HttpClient Client => _httpClient.Value;

    private readonly Entry _delayEntry;
    private int _requestNumber;

    public BackgroundHttpTests()
    {
        _delayEntry = new Entry { Placeholder = "Delay ms", Text = "3000", AutomationId = "DelayEntry", MinimumWidthRequest = 80, Keyboard = Keyboard.Numeric };

        var controlsLayout = new HorizontalWrapLayout
                             {
                                 _delayEntry,
                                 MakeButton("GET", "GetButton", () => RunAsync("GET", () => CreateRequest(HttpMethod.Get))),
                                 MakeButton("POST json", "PostJsonButton", () => RunAsync("POST json", () => CreateRequest(HttpMethod.Post, JsonContent.Create(new { Name = "Dog" })))),
                                 MakeButton("POST stream", "PostStreamButton", () => RunAsync("POST stream", () => CreateRequest(HttpMethod.Post, CreateStreamContent()))),
                                 MakeButton("POST multipart", "PostMultipartButton", () => RunAsync("POST multipart", () => CreateRequest(HttpMethod.Post, CreateMultipartContent()))),
                                 MakeButton("10 parallel", "ParallelButton", RunParallel),
                                 MakeButton("Cancel mid-flight", "CancelButton", RunCanceled),
                                 MakeButton("Long BG", "LongButton", RunLongBackground),
                                 MakeButton("30s chain", "ChainButton", RunChain),
                                 MakeButton("Crash app", "CrashButton", () => throw new InvalidOperationException("Crashing the app on purpose to test lost background responses")),
                                 MakeButton("Clear", "ClearButton", _results.Clear)
                             };
        controlsLayout.HorizontalSpacing = 8;
        controlsLayout.VerticalSpacing = 8;
        controlsLayout.Padding = new Thickness(16, 8);

        var resultsScroll = new VirtualScroll
                            {
                                AutomationId = "BgHttpResults",
                                ItemsSource = _results,
                                ItemTemplate = new DataTemplate(() =>
                                    {
                                        var label = new Label { FontSize = 12, Margin = new Thickness(16, 4), LineBreakMode = LineBreakMode.CharacterWrap };
                                        label.SetBinding(Label.TextProperty, Binding.SelfPath);

                                        return label;
                                    }
                                )
                            };

        var grid = new Grid
                   {
                       RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
                   };
        grid.Add(controlsLayout);
        grid.Add(resultsScroll, 0, 1);

        Content = grid;

        AppendResult("Long BG: tap, then immediately background/lock the phone. Crash app: relaunch to see LOST responses.");
        AppendResult(DeviceInfo.DeviceType == DeviceType.Virtual
            ? "SIMULATOR: using the default HttpClient (background sessions unsupported) — run on a REAL device for meaningful results."
            : "Device: using NSUrlBackgroundSessionHttpMessageHandler.");
    }

    internal static void AppendResult(string message)
        => MainThread.BeginInvokeOnMainThread(() => _results.Insert(0, $"{DateTime.Now:HH:mm:ss.f} {message}"));

    internal static string Truncate(string content)
        => content.Length <= 120 ? content : content[..120] + "…";

    private static Button MakeButton(string text, string automationId, Action onClicked)
    {
        var button = new Button { Text = text, AutomationId = automationId, FontSize = 11 };
        button.Clicked += (_, _) => onClicked();

        return button;
    }

    private int DelayMs => int.TryParse(_delayEntry.Text, out var delay) ? Math.Clamp(delay, 0, 5000) : 3000;

    private HttpRequestMessage CreateRequest(HttpMethod method, HttpContent? content = null, string? identifier = null)
    {
        var request = new HttpRequestMessage(method, new Uri($"test?delay={DelayMs}", UriKind.Relative)) { Content = content };

        if (identifier is not null)
        {
            request.Headers.Add(NSUrlBackgroundSessionHttpMessageHandler.RequestIdentifierHeaderName, identifier);
        }

        return request;
    }

    private static StreamContent CreateStreamContent()
    {
        var payload = new byte[4096];
        Random.Shared.NextBytes(payload);

        return new StreamContent(new MemoryStream(payload));
    }

    private static MultipartFormDataContent CreateMultipartContent()
    {
        var payload = new byte[2048];
        Random.Shared.NextBytes(payload);

        return new MultipartFormDataContent
               {
                   { new StringContent("Dog"), "name" },
                   { new ByteArrayContent(payload), "file", "payload.bin" }
               };
    }

#pragma warning disable VSTHRD100 // async void: manual test page button handlers
    private async void RunAsync(string name, Func<HttpRequestMessage> requestFactory, CancellationToken cancellationToken = default)
#pragma warning restore VSTHRD100
    {
        var requestName = $"#{Interlocked.Increment(ref _requestNumber)} {name}";
        AppendResult($"{requestName} started (delay {DelayMs}ms)");

        try
        {
            using var request = requestFactory();
            using var response = await Client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            AppendResult($"{requestName} [{(int) response.StatusCode}] {Truncate(content)}");
        }
        catch (OperationCanceledException)
        {
            AppendResult($"{requestName} CANCELED");
        }
        catch (Exception ex)
        {
            AppendResult($"{requestName} FAILED: {ex.Message}");
        }
    }

    private void RunParallel()
    {
        for (var i = 0; i < 10; i++)
        {
            RunAsync($"parallel {i + 1}", () => CreateRequest(HttpMethod.Post, JsonContent.Create(new { Name = $"Dog {i + 1}" })));
        }
    }

#pragma warning disable VSTHRD100
    private async void RunCanceled()
#pragma warning restore VSTHRD100
    {
        using var cancellationTokenSource = new CancellationTokenSource(1500);
        RunAsync("cancelable", () => CreateRequest(HttpMethod.Post, JsonContent.Create(new { Name = "Dog" })), cancellationTokenSource.Token);

        // Keep the CTS alive until it has fired.
        await Task.Delay(2000);
    }

    private void RunLongBackground()
        => RunAsync("long-bg", () => CreateRequest(HttpMethod.Post, JsonContent.Create(new { Name = "Dog" }), identifier: $"manual-long-{Guid.NewGuid():N}"));

#pragma warning disable VSTHRD100
    private async void RunChain()
#pragma warning restore VSTHRD100
    {
        // ~30s of continuous traffic: background/lock the phone while it runs.
        for (var i = 0; i < 6; i++)
        {
            var requestName = $"#{Interlocked.Increment(ref _requestNumber)} chain {i + 1}/6";
            AppendResult($"{requestName} started");

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, new Uri("test?delay=5000", UriKind.Relative))
                                    {
                                        Content = JsonContent.Create(new { Name = $"Dog {i + 1}" })
                                    };
                using var response = await Client.SendAsync(request);
                var content = await response.Content.ReadAsStringAsync();
                AppendResult($"{requestName} [{(int) response.StatusCode}] {Truncate(content)}");
            }
            catch (Exception ex)
            {
                AppendResult($"{requestName} FAILED: {ex.Message}");

                break;
            }
        }
    }
}
#endif
