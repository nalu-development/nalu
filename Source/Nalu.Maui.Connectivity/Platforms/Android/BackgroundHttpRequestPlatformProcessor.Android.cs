namespace Nalu;

using Microsoft.Extensions.Logging;
using Nalu.Internals;

#pragma warning disable VSTHRD103

/// <summary>
/// iOS processor for background HTTP requests.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BackgroundHttpRequestPlatformProcessor"/> class.
/// </remarks>
/// <param name="options">The options.</param>
/// <param name="logger">The logger.</param>
public sealed class BackgroundHttpRequestPlatformProcessor(BackgroundClientHttpOptions options, ILogger<BackgroundHttpRequestPlatformProcessor> logger)
    : IBackgroundHttpRequestPlatformProcessor, IAppBackgroundService
{
    private record HttpCall(BackgroundHttpClient Client, HttpRequestMessage Request, BackgroundHttpRequestHandle Handle);
    private readonly List<HttpCall> _pendingCalls = [];

    public Task StartPlatformRequestAsync(BackgroundHttpClient client, BackgroundHttpRequestMessage request, BackgroundHttpRequestHandle handle)
    {
        lock (_pendingCalls)
        {
            _pendingCalls.Add(new HttpCall(client, request, handle));
        }

        logger.LogInformation("Enqueued background HTTP request '{RequestName}'.", request.RequestName);
        AndroidApp.StartForegroundService(typeof(BackgroundHttpRequestPlatformProcessorForegroundService));
        return Task.CompletedTask;
    }

    public string UserTitle => options.DefaultUserTitle;
    public string UserDescription => options.DefaultUserDescription;

    public Task StartAsync(Action completionHandler)
    {
        logger.LogInformation("Starting background HTTP request processor.");
        _ = RunAsync(completionHandler);
        return Task.CompletedTask;
    }

    public Task StopAsync() => Task.CompletedTask;

    private async Task RunAsync(Action completionHandler)
    {
        await Task.Yield();

        while (true)
        {
            HttpCall[] calls;
            lock (_pendingCalls)
            {
                calls = [.. _pendingCalls];
                _pendingCalls.Clear();
            }

            if (calls.Length == 0)
            {
                completionHandler();
                return;
            }

            await Parallel.ForEachAsync(calls, new ParallelOptions { MaxDegreeOfParallelism = 6 }, async (call, _) =>
            {
                try
                {
                    call.Handle.SetRunning();
                    logger.LogDebug("Sending background HTTP request '{RequestName}'.", call.Handle.RequestName);
                    var response = await call.Client.SendAsync(call.Request, CancellationToken.None).ConfigureAwait(false);
                    var responsePath = await DownloadResponseToFileAsync(call, response).ConfigureAwait(false);
                    var fileStream = new FileStream(responsePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan | FileOptions.DeleteOnClose);
                    var newResponseContent = new StreamContent(fileStream);
                    CopyContentHeaders(response, newResponseContent);
                    response.Content.Dispose();
                    response.Content = newResponseContent;
                    logger.LogDebug("Received background HTTP response '{RequestName}'.", call.Handle.RequestName);
                    call.Handle.SetResult(response);
                }
                catch (HttpRequestException ex)
                {
                    logger.LogError(ex, "An error occurred while processing the request '{RequestName}'.", call.Handle.RequestName);
                    call.Handle.SetError(ex);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred while processing the request '{RequestName}'.", call.Handle.RequestName);
                    call.Handle.SetError(new HttpRequestException($"An error occurred while processing the request '{call.Handle.RequestName}'.", ex));
                }
            }).ConfigureAwait(false);

            var completionTimeoutTask = Task.Delay(TimeSpan.FromSeconds(5));
            var acknowledgeTasks = calls.Select(call => call.Handle.AcknowledgeTask).ToArray();

            await Task.WhenAny(completionTimeoutTask, Task.WhenAll(acknowledgeTasks)).ConfigureAwait(false);
        }
    }

    private static void CopyContentHeaders(HttpResponseMessage response, StreamContent newResponseContent)
    {
        foreach (var header in response.Content.Headers)
        {
            newResponseContent.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private static async Task<string> DownloadResponseToFileAsync(HttpCall call, HttpResponseMessage response)
    {
        var responsePath = Path.Combine(Path.GetTempPath(), $"{call.Handle.RequestName.ToBase64FilenameSafe()}.htresponse");
        await using var fileStream = File.Create(responsePath);
        await response.Content.CopyToAsync(fileStream, CancellationToken.None).ConfigureAwait(false);
        return responsePath;
    }
}
