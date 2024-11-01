namespace Nalu;

/// <summary>
/// Holds the response of a background request.
/// </summary>
public class NSUrlBackgroundResponseHandle
{
    private readonly Task<HttpResponseMessage> _responseTask;

    /// <summary>
    /// Gets the request identifier.
    /// </summary>
    public string RequestIdentifier { get; }

    internal NSUrlBackgroundResponseHandle(string requestIdentifier, Task<HttpResponseMessage> responseTask)
    {
        RequestIdentifier = requestIdentifier;
        _responseTask = responseTask;
    }

    /// <summary>
    /// Gets the response task.
    /// </summary>
#pragma warning disable VSTHRD003
    public Task<HttpResponseMessage> GetResponseAsync() => _responseTask;
#pragma warning restore VSTHRD003
}
