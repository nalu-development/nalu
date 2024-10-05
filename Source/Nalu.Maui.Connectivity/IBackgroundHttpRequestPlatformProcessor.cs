namespace Nalu;

/// <summary>
/// Platform-specific processor for background HTTP requests.
/// </summary>
public interface IBackgroundHttpRequestPlatformProcessor
{
    /// <summary>
    /// Starts a platform-specific background HTTP request.
    /// </summary>
    /// <param name="client">The background HTTP client.</param>
    /// <param name="request">The request.</param>
    /// <param name="handle">The handle.</param>
    Task StartPlatformRequestAsync(BackgroundHttpClient client, BackgroundHttpRequestMessage request, BackgroundHttpRequestHandle handle);
}
