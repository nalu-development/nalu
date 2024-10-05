namespace Nalu;

/// <summary>
/// Manages background HTTP requests.
/// </summary>
public interface IBackgroundHttpRequestManager
{
    /// <summary>
    /// Generates a new request ID.
    /// </summary>
    long NewRequestId();

    /// <summary>
    /// Gets the handle for the specified request name, or <see langword="null"/> if the request is not being tracked.
    /// </summary>
    /// <param name="requestName">The request unique name.</param>
    BackgroundHttpRequestHandle? GetHandle(string requestName);

    /// <summary>
    /// Starts tracking a new handle.
    /// </summary>
    /// <param name="backgroundHttpRequestHandle">The handle.</param>
    void Track(BackgroundHttpRequestHandle backgroundHttpRequestHandle);

    /// <summary>
    /// Stops tracking a handle.
    /// </summary>
    /// <param name="backgroundHttpRequestHandle">The handle.</param>
    void Untrack(BackgroundHttpRequestHandle backgroundHttpRequestHandle);

    /// <summary>
    /// Saves the updated handle.
    /// </summary>
    /// <param name="handle">The handle.</param>
    void Save(BackgroundHttpRequestHandle handle);

    /// <summary>
    /// Enumerates all handles.
    /// </summary>
    IEnumerable<BackgroundHttpRequestHandle> GetHandles();
}
