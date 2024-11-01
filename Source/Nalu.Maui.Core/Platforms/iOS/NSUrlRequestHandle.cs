namespace Nalu;

using System.Net;

internal class NSUrlRequestHandle(
    string identifier,
    CookieContainer? cookieContainer,
    string? contentFile,
    CancellationTokenRegistration cancellationTokenRegistration,
    bool isLostRequest = false)
{
    private readonly TaskCompletionSource _completedCompletionSource = new();
    private bool _completed;
    public TaskCompletionSource<HttpResponseMessage> ResponseCompletionSource { get; } = new();
    public Task CompletedTask => _completedCompletionSource.Task;
    public string Identifier => identifier;
    public bool IsLostRequest => isLostRequest;
    public CookieContainer? CookieContainer => cookieContainer;
    public string? ResponseContentFile { get; set; }

    public void Complete()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        cancellationTokenRegistration.Dispose();
        _completedCompletionSource.SetResult();

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
