namespace Nalu;

/// <summary>
/// A background HTTP client that can be used to send HTTP requests which continue even when the app is in the background.
/// </summary>
public interface IBackgroundHttpClient
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
    Task<BackgroundHttpRequestHandle> StartAsync(BackgroundHttpRequestMessage request);
}
