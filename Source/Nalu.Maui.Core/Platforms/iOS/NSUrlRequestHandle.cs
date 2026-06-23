using System.Net;

namespace Nalu;

internal class NSUrlRequestHandle(
    string identifier,
    CookieContainer? cookieContainer,
    string? contentFile,
    bool isLostRequest = false
)
{
    private readonly TaskCompletionSource _completedCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Lock _completionLock = new();
    private bool _completed;
    public TaskCompletionSource<HttpResponseMessage> ResponseCompletionSource { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task CompletedTask => _completedCompletionSource.Task;
    public string Identifier => identifier;
    public bool IsLostRequest => isLostRequest;
    public CookieContainer? CookieContainer => cookieContainer;
    public string? ResponseContentFile { get; set; }

    public CancellationTokenRegistration? CancellationTokenRegistration;

    public void Complete()
    {
        lock (_completionLock)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
        }

        CancellationTokenRegistration?.Dispose();
        _completedCompletionSource.TrySetResult();

        if (File.Exists(contentFile))
        {
            File.Delete(contentFile);
        }

        if (File.Exists(ResponseContentFile))
        {
            File.Delete(ResponseContentFile);
        }
    }
}
