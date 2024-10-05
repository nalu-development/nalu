namespace Nalu;

/// <summary>
/// A background HTTP client that can be used to send HTTP requests which continue even when the app is in the background.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="BackgroundHttpClient"/> class.
/// </remarks>
/// <param name="manager">The manager used to process/store requests.</param>
/// <param name="processor">The platform-specific processor used to send requests.</param>
/// <param name="dispatcher">The dispatcher.</param>
public class BackgroundHttpClient(
    IBackgroundHttpRequestManager manager,
    IBackgroundHttpRequestPlatformProcessor processor,
    IDispatcher dispatcher)
    : IBackgroundHttpClient
{
    /// <summary>
    /// Gets or sets the base address of the <see cref="BackgroundHttpClient"/>.
    /// </summary>
    public Uri? BaseAddress { get; set; }

    /// <summary>
    /// Starts a background HTTP request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <exception cref="InvalidOperationException">a request with the same name is already in progress.</exception>
    public async Task<BackgroundHttpRequestHandle> StartAsync(BackgroundHttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RequestName);

        if (manager.GetHandle(request.RequestName) is { State: not BackgroundHttpRequestState.Completed })
        {
            throw new InvalidOperationException($"A request with the name '{request.RequestName}' is already in progress.");
        }

        var descriptor = new BackgroundHttpRequestDescriptor
        {
            RequestId = manager.NewRequestId(),
            RequestName = request.RequestName,
            IsMultiPart = request.Content is MultipartContent,
        };

        var handler = new BackgroundHttpRequestHandle(manager, descriptor);

        if (dispatcher.IsDispatchRequired)
        {
            await dispatcher.DispatchAsync(async () =>
            {
                manager.Track(handler);
                await processor.StartPlatformRequestAsync(this, request, handler).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }
        else
        {
            manager.Track(handler);
            await processor.StartPlatformRequestAsync(this, request, handler).ConfigureAwait(true);
        }

        return handler;
    }
}
